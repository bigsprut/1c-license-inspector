using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.ServiceProcess;
using Microsoft.Win32;

namespace OneCLicenseInspector
{
    internal static class CollectHasp4
    {
        public static void Run(ScanResult r)
        {
            Hasp4Info h = r.Hasp4;
            h.Port = "475";

            ServiceController target = null;
            try
            {
                foreach (ServiceController sc in ServiceController.GetServices())
                {
                    string dn = sc.DisplayName ?? "";
                    string n = sc.ServiceName ?? "";
                    if (n.IndexOf("hasp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("HASP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("Aladdin", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (dn.IndexOf("HASP Loader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("haspldr", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            target = sc;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Warn, "HASP4", "Не удалось перечислить службы: " + ex.Message);
            }

            if (target != null)
            {
                h.ServiceInstalled = true;
                h.ServiceName = string.Equals(target.DisplayName, target.ServiceName, StringComparison.OrdinalIgnoreCase) ? target.DisplayName : target.DisplayName + " (" + target.ServiceName + ")";
                try { h.ServiceRunning = target.Status == ServiceControllerStatus.Running; }
                catch { }
                r.AddLog(LogLevel.Info, "HASP4", "Служба HASP License Manager: " + (h.ServiceRunning ? "работает" : "остановлена"));
            }
            else
            {
                r.AddLog(LogLevel.Info, "HASP4", "Служба HASP License Manager (nhsrvice) не найдена");
            }

            string servicePath = ReadServicePath("HASP Loader");
            h.ServicePath = servicePath;

            string yang = Path.Combine(Paths.WindowsDir, "SysWOW64", "nhsrvice.exe");
            string yang64 = Path.Combine(Paths.WindowsDir, "System32", "nhsrvice.exe");
            string exe = File.Exists(yang) ? yang : (File.Exists(yang64) ? yang64 : "");
            if (!string.IsNullOrEmpty(exe))
            {
                if (string.IsNullOrEmpty(h.ServicePath)) h.ServicePath = exe;
                h.Version = Files.Version(exe);
                if (string.IsNullOrEmpty(h.Version)) h.Version = "HASP LM";
                r.AddLog(LogLevel.Info, "HASP4", "Менеджер лицензий: " + exe + " v" + h.Version);
            }

            h.InstallDate = ReadInstallDate();

            string lmDirX86 = Path.Combine(Paths.ProgramFilesX86, "Aladdin", "HASP LM");
            string lmDir = Path.Combine(Paths.ProgramFiles, "Aladdin", "HASP LM");
            string iniPath = File.Exists(Path.Combine(lmDirX86, "nhsrv.ini")) ? Path.Combine(lmDirX86, "nhsrv.ini") :
                             (File.Exists(Path.Combine(lmDir, "nhsrv.ini")) ? Path.Combine(lmDir, "nhsrv.ini") : "");
            if (!string.IsNullOrEmpty(iniPath))
            {
                h.IniSummary = SummarizeIni(iniPath);
            }

            string confNethasp = Path.Combine(Paths.ProgramFiles, "1cv8", "conf", "nethasp.ini");
            if (File.Exists(confNethasp)) h.NethaspIni = confNethasp;
            else
            {
                string confX86 = Path.Combine(Paths.ProgramFilesX86, "1cv8", "conf", "nethasp.ini");
                if (File.Exists(confX86)) h.NethaspIni = confX86;
            }

            h.AddressFile = Path.Combine(Paths.WindowsDir, "SysWOW64", "haspaddr.dat");
            if (!File.Exists(h.AddressFile)) h.AddressFile = "";

            h.PortListening = IsListening(475);
            if (h.PortListening)
            {
                r.AddLog(LogLevel.Info, "HASP4", "Порт 475 (HASP4 Net) прослушивается");
            }

            // Поиск утилит HASP: HASPInfo (консольная) или HASP Monitor (графическая).
            string toolPath;
            string toolKind;
            FindHaspTool(out toolPath, out toolKind);
            if (!string.IsNullOrEmpty(toolPath))
            {
                h.ToolPath = toolPath;
                h.ToolKind = toolKind;
                r.AddLog(LogLevel.Info, "HASP4", "Утилита HASP: " + toolPath + " (" + toolKind + ")");
                if (string.Equals(toolKind, "info", StringComparison.OrdinalIgnoreCase))
                {
                    string so, se;
                    int code = Exec.Run(toolPath, "-d", 8000, out so, out se);
                    if (string.IsNullOrEmpty(so.Trim()))
                    {
                        int code2 = Exec.Run(toolPath, "", 8000, out so, out se);
                        code = code2;
                    }
                    h.ToolOutput = (so ?? "").Trim();
                    if (h.ToolOutput.Length == 0 && !string.IsNullOrEmpty(se)) h.ToolOutput = se.Trim();
                    ParseHaspInfo(h);
                    r.AddLog(LogLevel.Info, "HASP4", "HASPInfo: код " + code + ", строк " + CountLines(h.ToolOutput));
                }
            }
            if (h.ServiceInstalled || h.PortListening)
            {
                h.Comment = "Обнаружен устаревший менеджер лицензий HASP4 Net (для 1С 7.7 и 8.2). " +
                            "Состав лицензий в ключах HASP4 не публикуется производителем: доступны только факт работы менеджера, " +
                            "версия и параметры. Количество мест по таким ключам смотрите в утилите HASP Monitor.";
            }
            else
            {
                h.Comment = "Менеджер HASP4 Net не обнаружен. Если 1С 7.7 использует локальный USB-ключ, он виден только через драйвер HASP.";
            }
        }

        private static string ReadServicePath(string serviceName)
        {
            try
            {
                using (RegistryKey k = Reg.Open(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false))
                {
                    return Reg.Str(k, "ImagePath");
                }
            }
            catch { return ""; }
        }

        private static string ReadInstallDate()
        {
            try
            {
                RegistryView[] views = new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 };
                foreach (RegistryView view in views)
                {
                    using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey un = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HASP License Manager", false))
                    {
                        if (un == null) continue;
                        string v = Reg.Str(un, "DisplayVersion");
                        if (string.IsNullOrEmpty(v)) v = Reg.Str(un, "Version");
                        return v;
                    }
                }
            }
            catch { }
            return "";
        }

        private static string SummarizeIni(string path)
        {
            try
            {
                List<string> parts = new List<string>();
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;
                    string up = line.ToUpperInvariant();
                    if (up.StartsWith("NHS_USERLIST"))
                    {
                        parts.Add("максимум подключений: " + AfterEquals(line));
                    }
                    else if (up.StartsWith("NHS_IP_PORTNUM"))
                    {
                        parts.Add("порт: " + AfterEquals(line));
                    }
                    else if (up.StartsWith("NHS_USE_TCP"))
                    {
                        parts.Add("TCP: " + AfterEquals(line));
                    }
                    else if (up.StartsWith("NHS_USE_UDP"))
                    {
                        parts.Add("UDP: " + AfterEquals(line));
                    }
                    else if (up.StartsWith("NHS_USE_NETBIOS"))
                    {
                        parts.Add("NetBIOS: " + AfterEquals(line));
                    }
                }
                return string.Join("; ", parts.ToArray());
            }
            catch { return ""; }
        }

        private static string AfterEquals(string line)
        {
            int i = line.IndexOf('=');
            if (i < 0) return line;
            string v = line.Substring(i + 1).Trim();
            int c = v.IndexOf(';');
            if (c >= 0) v = v.Substring(0, c).Trim();
            return v;
        }

        private static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Replace("\r", "").Split('\n').Length;
        }

        private static void FindHaspTool(out string path, out string kind)
        {
            path = "";
            kind = "";
            if (!string.IsNullOrEmpty(Program.HaspInfoOverride) && File.Exists(Program.HaspInfoOverride))
            {
                path = Program.HaspInfoOverride;
                kind = "info";
                return;
            }

            List<string> dirs = new List<string>();
            foreach (string baseDir in new string[] { Paths.ProgramFiles, Paths.ProgramFilesX86 })
            {
                dirs.Add(Path.Combine(baseDir, "Aladdin", "HASP LM"));
                dirs.Add(Path.Combine(baseDir, "Aladdin"));
                dirs.Add(Path.Combine(baseDir, "SafeNet"));
                dirs.Add(Path.Combine(baseDir, "SafeNet Sentinel", "Sentinel LDK"));
                dirs.Add(Path.Combine(baseDir, "1C", "1CE"));
                dirs.Add(Path.Combine(baseDir, "1cv8", "common"));
            }
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string p in pathVar.Split(';'))
            {
                if (!string.IsNullOrEmpty(p)) dirs.Add(p);
            }

            string[] infoNames = new string[] { "haspinfo.exe", "haspinfo64.exe" };
            string[] monitorNames = new string[] { "haspmntr.exe", "haspmon.exe", "hasp monitor.exe" };

            foreach (string dir in dirs)
            {
                try
                {
                    foreach (string n in infoNames)
                    {
                        string f = Path.Combine(dir, n);
                        if (File.Exists(f)) { path = f; kind = "info"; return; }
                    }
                    foreach (string n in monitorNames)
                    {
                        string f = Path.Combine(dir, n);
                        if (File.Exists(f)) { path = f; kind = "monitor"; return; }
                    }
                }
                catch { }
            }
        }

        public static void ParseToolOutput(Hasp4Info h)
        {
            ParseHaspInfo(h);
        }

        private static void ParseHaspInfo(Hasp4Info h)
        {
            if (string.IsNullOrEmpty(h.ToolOutput)) return;
            foreach (string raw in h.ToolOutput.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string low = line.ToLowerInvariant();
                if (h.ReportedKeyId.Length == 0 && (low.IndexOf("hasp id") >= 0 || low.IndexOf("hasp-id") >= 0))
                {
                    h.ReportedKeyId = AfterColon(line);
                }
                else if (h.ReportedKeyType.Length == 0 && (low.IndexOf("key type") >= 0 || low.IndexOf("тип ключа") >= 0))
                {
                    h.ReportedKeyType = AfterColon(line);
                }
            }
        }

        private static string AfterColon(string line)
        {
            int i = line.IndexOf(':');
            if (i < 0) i = line.IndexOf('=');
            if (i < 0) return "";
            return line.Substring(i + 1).Trim();
        }
        public static bool IsListening(int port)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(350)) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
