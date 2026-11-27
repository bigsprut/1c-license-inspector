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
