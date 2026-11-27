using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace OneCLicenseInspector
{
    internal static class Exec
    {
        public static int Run(string exe, string arguments, int timeoutMs, out string stdout, out string stderr)
        {
            stdout = "";
            stderr = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                bool shell = exe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                             exe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
                if (shell)
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = "/c \"\"" + exe + "\" " + arguments + "\"";
                }
                else
                {
                    psi.FileName = exe;
                    psi.Arguments = arguments;
                }
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;

                using (Process p = Process.Start(psi))
                {
                    StringBuilder so = new StringBuilder();
                    StringBuilder se = new StringBuilder();
                    p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) so.AppendLine(e.Data); };
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) se.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        stdout = so.ToString();
                        stderr = se.ToString();
                        return -2;
                    }
                    p.WaitForExit(500);
                    stdout = so.ToString();
                    stderr = se.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return -1;
            }
        }
    }

    internal static class CollectCluster
    {
        public static void Run(ScanResult r)
        {
            Run(r, "", "", "");
        }

        private static string RunningServerAgents()
        {
            List<string> names = new List<string>();
            try
            {
                using (System.Management.ManagementObjectSearcher searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name,DisplayName,State FROM Win32_Service WHERE State='Running'"))
                {
                    foreach (System.Management.ManagementBaseObject o in searcher.Get())
                    {
                        string d = (Convert.ToString(o["Name"]) + " " + Convert.ToString(o["DisplayName"])).Trim();
                        if (d.Length == 0) continue;
                        bool agent = d.IndexOf("Server Agent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     d.IndexOf("Агент сервера", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!agent) continue;
                        if (d.IndexOf("8.2", StringComparison.Ordinal) >= 0 && !names.Contains("8.2")) names.Add("8.2");
                        else if (d.IndexOf("8.3", StringComparison.Ordinal) >= 0 && !names.Contains("8.3")) names.Add("8.3");
                    }
                }
            }
            catch { }
            return string.Join(", ", names.ToArray());
        }

        public static void Run(ScanResult r, string endpoint, string user, string password)
        {
            ClusterInfo c = r.Cluster;
            if (string.IsNullOrEmpty(endpoint)) endpoint = "localhost:1545";
            if (endpoint.IndexOf(':') < 0) endpoint += ":1545";
            c.Endpoint = endpoint;

            c.RacPath = FindRac(r);
            string agents = RunningServerAgents();
            if (string.IsNullOrEmpty(c.RacPath))
            {
                c.Available = false;
                c.Error = "rac.exe не найден: нужна установленная платформа 1С 8.3";
                if (agents.IndexOf("8.2", StringComparison.Ordinal) >= 0 && agents.IndexOf("8.3", StringComparison.Ordinal) < 0)
                {
                    c.Error = "на сервере работает агент 1С 8.2 — в 8.2 утилиты rac нет";
                    c.Note = "Сеансы и лицензии 8.2 показывает только консоль администрирования сервера 1С (MMC). " +
                             "Для автоматического чтения нужен HASP Monitor (ключи HASP4, порт 475) либо переход баз на 8.3.";
                }
                else
                {
                    c.Note = "Список сеансов и выданных лицензий читается утилитой rac из комплекта 1С 8.3." +
                             (agents.Length > 0 ? " Работающие агенты: " + agents + "." : "");
                }
                r.AddLog(LogLevel.Info, "Кластер", c.Error);
                return;
            }

            int port = PortOf(endpoint);
            bool forced = !string.IsNullOrEmpty(Program.RacOverride);
            if (!forced && !IsOpen(endpoint, port))
            {
                // Пытаемся сами поднять ras.exe, если платформа 8.3 локальная.
                string ras = FindRas(r);
                if (ras.Length > 0 && IsLocalEndpoint(endpoint) && agents.IndexOf("8.3", StringComparison.Ordinal) >= 0)
                {
                    if (TryStartRas(ras, port))
                    {
                        c.RasStartedByUs = true;
                        c.RasPath = ras;
                        r.AddLog(LogLevel.Info, "Кластер", "Запущен ras: " + ras + " --port=" + port);
                    }
                }
            }

            if (!forced && !IsOpen(endpoint, port))
            {
                c.Available = false;
                c.Error = "сервер администрирования не отвечает на " + endpoint;
                c.Note = "Служба «1С:Предприятие. Сервер администрирования» (ras) не запущена или порт другой. " +
                         "Запускается как ras.exe --port=1545 или службой Windows." +
                         (agents.IndexOf("8.3", StringComparison.Ordinal) >= 0
                             ? " Агент 8.3 работает, но ras не поднялся: запустите ras.exe --port=1545 вручную (нужны права администратора)."
                             : agents.IndexOf("8.2", StringComparison.Ordinal) >= 0
                             ? " На сервере работает агент 8.2: его сеансы rac не видит — смотрите консоль администрирования 1С 8.2."
                             : "");
                r.AddLog(LogLevel.Info, "Кластер", c.Error);
                return;
            }
            c.Available = true;

            string output, errorText;
            int code = Exec.Run(c.RacPath, endpoint + " cluster list", 6000, out output, out errorText);
            c.Raw = output;
            if (code != 0 || LooksLikeError(output, errorText))
            {
                c.Error = ShortError(output, errorText, code);
                r.AddLog(LogLevel.Warn, "Кластер", "cluster list: " + c.Error);
                return;
            }

            List<Record> clusters = ParseRecords(output, "cluster");
            foreach (Record rec in clusters)
            {
                ClusterEntry e = new ClusterEntry();
                e.Id = rec.Get("cluster");
                e.Name = rec.Get("name");
                e.Host = rec.Get("host");
                e.Port = rec.Get("port");
                e.Expiration = rec.Get("expiration");
                c.Clusters.Add(e);
            }
            if (c.Clusters.Count == 0)
            {
                c.Error = "кластеры не найдены";
                r.AddLog(LogLevel.Info, "Кластер", c.Error);
                return;
            }

            r.AddLog(LogLevel.Info, "Кластер", "Кластеров: " + c.Clusters.Count + " (" + endpoint + ")");
            foreach (ClusterEntry e in c.Clusters)
            {
                StringBuilder args = new StringBuilder();
                args.Append(endpoint).Append(" session list --cluster=").Append(e.Id).Append(" --licenses");
                if (!string.IsNullOrEmpty(user)) args.Append(" --cluster-user=\"").Append(user).Append('"');
                if (!string.IsNullOrEmpty(password)) args.Append(" --cluster-pwd=\"").Append(password).Append('"');

                string so, se;
                int rc = Exec.Run(c.RacPath, args.ToString(), 10000, out so, out se);
                if (rc != 0 || LooksLikeError(so, se))
                {
                    c.Error = ShortError(so, se, rc);
                    r.AddLog(LogLevel.Warn, "Кластер", "session list: " + c.Error);
                    continue;
                }
                List<Record> sessions = ParseRecords(so, "session");
                foreach (Record rec in sessions)
                {
                    SessionEntry s = new SessionEntry();
                    s.ClusterName = e.Name;
                    s.SessionId = rec.Get("session");
                    s.User = rec.Get("user-name");
                    s.Host = rec.Get("host");
                    s.AppId = rec.Get("app-id");
                    s.Infobase = rec.Get("infobase");
                    s.StartedAt = rec.Get("started-at");
                    s.LicenseType = rec.Get("license-type");
                    s.LicenseSeries = rec.Get("series");
                    s.LicenseFullName = rec.Get("full-name");
                    s.IssuedByServer = rec.Get("issued-by-server");
                    s.MaxUsersAll = rec.Get("max-users-all");
                    s.MaxUsersCur = rec.Get("max-users-cur");
                    s.LicenseShort = rec.Get("short-presentation");
                    s.LicenseFull = rec.Get("full-presentation");
                    c.Sessions.Add(s);
                }
            }
            r.AddLog(LogLevel.Info, "Кластер", "Сеансов: " + c.Sessions.Count);
        }

        public static string FindRas(ScanResult r)
        {
            return FindTool(r, "ras.exe");
        }

        public static string FindRac(ScanResult r)
        {
            if (!string.IsNullOrEmpty(Program.RacOverride) && File.Exists(Program.RacOverride)) return Program.RacOverride;
            return FindTool(r, "rac.exe");
        }

        private static string FindTool(ScanResult r, string fileName)
        {
            string best = "";
            Version bestVer = new Version(0, 0);
            foreach (PlatformInfo p in r.Platforms)
            {
                if (p.Family.IndexOf("8", StringComparison.Ordinal) < 0) continue;
                try
                {
                    string exe = Path.Combine(p.BinPath, fileName);
                    if (!File.Exists(exe)) continue;
                    Version v;
                    if (!Version.TryParse(NormalizeVersion(p.Version), out v)) v = new Version(0, 0);
                    if (best.Length == 0 || v > bestVer)
                    {
                        best = exe;
                        bestVer = v;
                    }
                }
                catch { }
            }
            if (best.Length > 0) return best;

            string[] probe = new string[]
            {
                Path.Combine(Paths.ProgramFiles, "1cv8", fileName),
                Path.Combine(Paths.ProgramFilesX86, "1cv8", fileName)
            };
            foreach (string p in probe) if (File.Exists(p)) return p;
            return "";
        }

        private static string NormalizeVersion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "0.0";
            string[] parts = raw.Split('.');
            if (parts.Length < 2) return "0.0";
            int a, b;
            if (!int.TryParse(parts[0], out a)) a = 0;
            if (!int.TryParse(parts[1], out b)) b = 0;
            return a + "." + b;
        }

        private static int PortOf(string endpoint)
        {
            int idx = endpoint.LastIndexOf(':');
            if (idx < 0) return 1545;
            int port;
            if (int.TryParse(endpoint.Substring(idx + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)) return port;
            return 1545;
        }

        private static string HostOf(string endpoint)
        {
            int idx = endpoint.LastIndexOf(':');
            if (idx < 0) return endpoint;
            string host = endpoint.Substring(0, idx);
            if (host == "*" || host.Length == 0) host = "localhost";
            return host;
        }

        private static bool IsLocalEndpoint(string endpoint)
        {
            string host = HostOf(endpoint).ToLowerInvariant();
            return host == "localhost" || host == "127.0.0.1" || host == "::1" ||
                   host == Environment.MachineName.ToLowerInvariant();
        }

        private static bool TryStartRas(string ras, int port)
        {
            System.Diagnostics.Process proc = null;
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(ras, "--port=" + port);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                try { psi.WorkingDirectory = Path.GetDirectoryName(ras); } catch { }
                proc = System.Diagnostics.Process.Start(psi);
                for (int i = 0; i < 30; i++)
                {
                    System.Threading.Thread.Sleep(300);
                    if (IsOpen("localhost:" + port, port)) return true;
                }
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            }
            catch { }
            return false;
        }
        private static bool IsOpen(string endpoint, int port)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect(HostOf(endpoint), port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(700)) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        private static bool LooksLikeError(string stdout, string stderr)
        {
            string all = (stdout + " " + stderr).ToLowerInvariant();
            if (all.IndexOf("access denied", StringComparison.Ordinal) >= 0) return true;
            if (all.IndexOf("not authorized", StringComparison.Ordinal) >= 0) return true;
            if (all.IndexOf("отказано", StringComparison.Ordinal) >= 0) return true;
            if (all.IndexOf("недостаточно прав", StringComparison.Ordinal) >= 0) return true;
            if (all.IndexOf("error", StringComparison.Ordinal) >= 0 && all.IndexOf("cluster :", StringComparison.Ordinal) < 0) return true;
            if (all.IndexOf("ошибка", StringComparison.Ordinal) >= 0 && all.IndexOf("cluster :", StringComparison.Ordinal) < 0) return true;
            return false;
        }

        private static string ShortError(string stdout, string stderr, int code)
        {
            string all = (stderr ?? "").Trim();
            if (all.Length == 0) all = (stdout ?? "").Trim();
            if (all.Length == 0) all = "код возврата " + code;
            string[] lines = all.Replace("\r", "").Split('\n');
            string s = lines.Length > 0 ? lines[0].Trim() : all;
            if (s.Length > 300) s = s.Substring(0, 300) + "…";
            string low = all.ToLowerInvariant();
            if (low.IndexOf("access denied", StringComparison.Ordinal) >= 0 || low.IndexOf("отказано", StringComparison.Ordinal) >= 0)
                s += " · нужны логин и пароль администратора кластера";
            return s;
        }

        private class Record
        {
            private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private string _last = "";

            public void Set(string key, string value)
            {
                _map[key] = value;
                _last = key;
            }

            public void Append(string text)
            {
                if (_last.Length == 0) return;
                _map[_last] = (_map[_last] ?? "") + "\n" + text;
            }

            public string Get(string key)
            {
                string v;
                return _map.TryGetValue(key, out v) ? (v ?? "").Trim() : "";
            }

            public bool Empty { get { return _map.Count == 0; } }
        }

        private static List<Record> ParseRecords(string text, string startKey)
        {
            List<Record> list = new List<Record>();
            if (string.IsNullOrEmpty(text)) return list;
            Record cur = new Record();
            foreach (string rawLine in text.Replace("\r", "").Split('\n'))
            {
                string line = rawLine;
                if (line.Trim().Length == 0)
                {
                    if (!cur.Empty) { list.Add(cur); cur = new Record(); }
                    continue;
                }
                int colon = line.IndexOf(':');
                bool isField = colon > 0 && line.Substring(0, colon).Trim().Length > 0 &&
                               line.Substring(0, colon).Trim().IndexOf(' ') < 0;
                if (isField)
                {
                    string key = line.Substring(0, colon).Trim();
                    string val = line.Substring(colon + 1).Trim();
                    if (string.Equals(key, startKey, StringComparison.OrdinalIgnoreCase) && !cur.Empty)
                    {
                        list.Add(cur);
                        cur = new Record();
                    }
                    cur.Set(key, val);
                }
                else
                {
                    cur.Append(line.TrimEnd());
                }
            }
            if (!cur.Empty) list.Add(cur);
            return list;
        }
    }
}
