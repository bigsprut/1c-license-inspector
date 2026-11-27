using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
                    // Читаем сырые байты: консольные утилиты (rac, ring) пишут в OEM-кодировке,
                    // а не в UTF-8 — иначе в журнале «кракозябры».
                    MemoryStream soMs = new MemoryStream();
                    MemoryStream seMs = new MemoryStream();
                    Thread tOut = new Thread(delegate () { try { p.StandardOutput.BaseStream.CopyTo(soMs); } catch { } });
                    Thread tErr = new Thread(delegate () { try { p.StandardError.BaseStream.CopyTo(seMs); } catch { } });
                    tOut.IsBackground = true;
                    tErr.IsBackground = true;
                    tOut.Start();
                    tErr.Start();

                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited) { try { p.Kill(); } catch { } }
                    tOut.Join(3000);
                    tErr.Join(3000);

                    Encoding enc = OemEncoding();
                    stdout = enc.GetString(soMs.ToArray());
                    stderr = enc.GetString(seMs.ToArray());
                    return exited ? p.ExitCode : -2;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return -1;
            }
        }

        private static Encoding OemEncoding()
        {
            try
            {
                int cp = System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
                if (cp > 0) return Encoding.GetEncoding(cp);
            }
            catch { }
            try { return Encoding.GetEncoding(866); } catch { }
            return Encoding.Default;
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

            // частая ошибка: указывают порт кластера 1541 вместо ras 1545
            if (endpoint.EndsWith(":1541", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.Substring(0, endpoint.Length - 5) + ":1545";
            string output, errorText;
            int code = Exec.Run(c.RacPath, "cluster list " + endpoint, 15000, out output, out errorText);
            if (code != 0 || LooksLikeError(output, errorText))
            {
                // старый синтаксис (<host> <command>) для платформ до 8.3.24
                code = Exec.Run(c.RacPath, endpoint + " cluster list", 15000, out output, out errorText);
            }
            c.Raw = output;
            Diag.Log("rac cluster list: rc=" + code + " out=" + (output ?? "").Trim());
            if (!string.IsNullOrEmpty(errorText)) Diag.Log("rac cluster list err: " + errorText.Trim());
            if (code != 0 || LooksLikeError(output, errorText))
            {
                c.Error = ShortError(output, errorText, code);
                r.AddLog(LogLevel.Warn, "Кластер", "cluster list: " + c.Error);
                return;
            }

            List<Record> clusters = ParseRecords(output, "cluster");
            Diag.Log("rac clusters parsed=" + clusters.Count);
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

            // Документация 1С: версии сервера администрирования (ras), кластера и утилиты rac должны
            // совпадать. При расхождении rac отвечает успешно, но ПУСТО, поэтому перебираем rac
            // от разных установленных платформ, начиная с той, что совпадает с работающим ras/агентом.
            List<string> racs = RacCandidates(r, c);
            if (racs.Count == 0 && !string.IsNullOrEmpty(c.RacPath)) racs.Add(c.RacPath);
            Diag.Log("rac candidates: " + string.Join("; ", racs.ToArray()));

            foreach (ClusterEntry e in c.Clusters)
            {
                string so = "", se = "";
                int rc = -1;
                string loginUsed = "";
                string racUsed = "";

                // Порядок попыток: новый синтаксис rac (8.3.24+: режим и команда первыми, адрес в конце),
                // затем старый, затем логины из списка администраторов кластера.
                // Важно: успешный ответ с пустым stdout — это тоже успех («сеансов нет»), раньше он
                // затирался ошибкой следующей попытки и на экран выходило «не аутентифицирован».
                string firstErrOut = null, firstErrText = null;
                int firstErrCode = 0;
                bool got = false;      // был хотя бы один успешный (rc=0) ответ
                bool hasData = false;  // в успешном ответе были сеансы
                string licNote = "";

                string[][] creds = new string[][]
                {
                    new string[] { user ?? "", password ?? "" },
                    new string[] { "", "" },
                    new string[] { "Администратор", "" },
                    new string[] { "Administrator", "" }
                };

                for (int ri = 0; ri < racs.Count && !hasData; ri++)
                {
                    string racPath = racs[ri];
                    for (int pass = 0; pass < 4 && !hasData; pass++)
                    {
                        bool newStyle = pass == 0 || pass == 2;
                        string[][] variants = creds;
                        if (pass >= 2)
                        {
                            variants = AdminNameCreds(r, c, e.Id, user, password, racPath);
                            if (variants.Length == 0) continue;
                        }

                        foreach (string[] cr in variants)
                        {
                            // --licenses на части кластеров возвращает успешный, но ПУСТОЙ ответ.
                            // Тогда повторяем без него: список сеансов важнее сведений о лицензиях.
                            foreach (bool licenses in new bool[] { true, false })
                            {
                                string a = SessionArgs(endpoint, e.Id, cr[0], cr[1], newStyle, licenses);
                                string so2, se2;
                                int rc2 = Exec.Run(racPath, a, 15000, out so2, out se2);
                                Diag.Log("rac " + VersionOf(racPath) + " session list [" + (newStyle ? "new" : "old") + "," +
                                         (cr[0].Length == 0 ? "no-auth" : cr[0]) + (licenses ? ",lic" : ",no-lic") +
                                         "]: rc=" + rc2 + " out=" + (so2 ?? "").Trim());
                                if (!string.IsNullOrEmpty(se2)) Diag.Log("  err: " + se2.Trim());

                                if (rc2 == 0 && !LooksLikeError(so2, se2))
                                {
                                    if (!got)
                                    {
                                        got = true;
                                        so = so2; se = se2; rc = rc2; loginUsed = cr[0]; racUsed = racPath;
                                    }
                                    if ((so2 ?? "").Trim().Length > 0)
                                    {
                                        so = so2; se = se2; rc = rc2; loginUsed = cr[0]; racUsed = racPath;
                                        hasData = true;
                                        if (!licenses)
                                            licNote = "Кластер не отдаёт сведения о лицензиях через rac (запрос с --licenses вернул пусто) — сеансы прочитаны без них.";
                                        break;
                                    }
                                }
                                else if (firstErrOut == null)
                                {
                                    firstErrOut = so2; firstErrText = se2; firstErrCode = rc2;
                                }
                            }
                            if (hasData) break;
                        }
                    }
                }

                if (!got)
                {
                    c.Error = ShortError(firstErrOut, firstErrText, firstErrCode);
                    r.AddLog(LogLevel.Warn, "Кластер", "session list: " + c.Error);
                    continue;
                }

                List<string> notes = new List<string>();
                if (loginUsed.Length > 0 && loginUsed != user)
                    notes.Add("Сеансы прочитаны логином «" + loginUsed + "».");
                if (licNote.Length > 0) notes.Add(licNote);
                if (racUsed.Length > 0 && racs.Count > 1 && !string.Equals(racUsed, racs[0], StringComparison.OrdinalIgnoreCase))
                {
                    c.RacPath = racUsed;
                    notes.Add("Сеансы читаются rac " + VersionOf(racUsed) + " — версия rac должна совпадать с ras и кластером; " +
                              "rac " + VersionOf(racs[0]) + " вернул пустой список.");
                }
                c.Note = string.Join(" ", notes.ToArray());
                if (c.Note.Length > 0) r.AddLog(LogLevel.Info, "Кластер", c.Note);
                try
                {
                    string diagDir = Path.Combine(Paths.ProgramData, "1C-License-Inspector", "diag");
                    try { if (!Directory.Exists(diagDir)) Directory.CreateDirectory(diagDir); } catch { diagDir = Path.GetTempPath(); }
                    File.WriteAllText(Path.Combine(diagDir, "rac-sessions.txt"), so ?? "", new UTF8Encoding(false));
                    try { File.WriteAllText(Path.Combine(diagDir, "rac-cluster.txt"), c.Raw ?? "", new UTF8Encoding(false)); } catch { }
                    if (c.SessionRaw.Length < 200000) c.SessionRaw += (so ?? "") + "\n";
                    if (!string.IsNullOrEmpty(se)) File.WriteAllText(Path.Combine(Path.GetTempPath(), "rac-sessions-err.txt"), se, new UTF8Encoding(false));
                }
                catch { }

                List<Record> sessions = ParseRecords(so, "session");
                Diag.Log("rac sessions parsed=" + sessions.Count);
                foreach (Record rec in sessions)
                {
                    SessionEntry s = new SessionEntry();
                    s.ClusterName = e.Name;
                    s.SessionId = rec.Get("session");
                    s.User = rec.Get("user-name");
                    s.Host = rec.Get("host-name");
                    if (string.IsNullOrEmpty(s.Host)) s.Host = rec.Get("host");
                    s.AppId = rec.Get("app-id");
                    s.Infobase = rec.Get("infobase");
                    s.StartedAt = rec.Get("started-at");
                    s.LicenseType = rec.Get("license-type");
                    s.LicenseSeries = rec.Get("series");
                    s.LicenseFullName = rec.Get("full-name");
                    s.IssuedByServer = rec.Get("issued-by-server");
                    s.MaxUsersAll = rec.Get("max-users-all");
                    s.MaxUsersCur = rec.Get("max-users-cur");
                    s.LicenseShort = StripQuotes(rec.Get("short-presentation"));
                    s.LicenseFull = StripQuotes(rec.Get("full-presentation"));
                    if (string.IsNullOrEmpty(s.LicenseShort) && !string.IsNullOrEmpty(s.LicenseSeries))
                        s.LicenseShort = s.LicenseSeries + " " + s.MaxUsersCur + " " + s.MaxUsersAll;
                    c.Sessions.Add(s);
                }

                // Список сеансов без --licenses не содержит сведений о лицензиях, а сам --licenses
                // на части кластеров отдаёт пусто. Пробуем добрать лицензию по каждому сеансу:
                // session info --session=<uuid> --licenses.
                if (licNote.Length > 0 && c.Sessions.Count > 0)
                {
                    string racForInfo = racUsed.Length > 0 ? racUsed : c.RacPath;
                    int filled = 0, asked = 0;
                    foreach (SessionEntry s in c.Sessions)
                    {
                        if (++asked > 15) break;
                        if (string.IsNullOrEmpty(s.SessionId)) continue;
                        if (!string.IsNullOrEmpty(s.LicenseType) || !string.IsNullOrEmpty(s.LicenseSeries)) continue;
                        StringBuilder q = new StringBuilder("session info --cluster=").Append(e.Id)
                            .Append(" --session=").Append(s.SessionId).Append(" --licenses");
                        if (loginUsed.Length > 0)
                        {
                            q.Append(" --cluster-user=\"").Append(loginUsed).Append("\"");
                            if (!string.IsNullOrEmpty(password)) q.Append(" --cluster-pwd=\"").Append(password).Append("\"");
                        }
                        q.Append(' ').Append(endpoint);
                        string io, ie;
                        int irc = Exec.Run(racForInfo, q.ToString(), 8000, out io, out ie);
                        if (!string.IsNullOrEmpty(ie)) Diag.Log("rac session info err: " + ie.Trim());
                        if (irc != 0) continue;
                        List<Record> one = ParseRecords(io, "session");
                        if (one.Count == 0) continue;
                        Record r2 = one[0];
                        s.LicenseType = r2.Get("license-type");
                        s.LicenseSeries = r2.Get("series");
                        s.LicenseFullName = r2.Get("full-name");
                        s.IssuedByServer = r2.Get("issued-by-server");
                        s.MaxUsersAll = r2.Get("max-users-all");
                        s.MaxUsersCur = r2.Get("max-users-cur");
                        s.LicenseShort = StripQuotes(r2.Get("short-presentation"));
                        s.LicenseFull = StripQuotes(r2.Get("full-presentation"));
                        if (string.IsNullOrEmpty(s.LicenseShort) && !string.IsNullOrEmpty(s.LicenseSeries))
                            s.LicenseShort = s.LicenseSeries + " " + s.MaxUsersCur + " " + s.MaxUsersAll;
                        if (!string.IsNullOrEmpty(s.LicenseType) || !string.IsNullOrEmpty(s.LicenseSeries)) filled++;
                    }
                    Diag.Log("rac session info: опрошено " + asked + ", с лицензией " + filled);
                    if (filled > 0)
                    {
                        c.Note = (c.Note + " Сведения о лицензиях добраны по каждому сеансу (session info --licenses).").Trim();
                        r.AddLog(LogLevel.Info, "Кластер", "Лицензии получены по сеансам: " + filled);
                    }
                }
            }
            r.AddLog(LogLevel.Info, "Кластер", "Сеансов: " + c.Sessions.Count);
        }

        private static readonly Dictionary<string, string[]> AdminNamesCache =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Кандидаты rac в порядке предпочтения. По документации 1С версии сервера администрирования
        /// (ras) и кластера должны совпадать, а rac обязан соответствовать ras: иначе rac может
        /// отвечать успешно, но ПУСТО. Поэтому первым идёт rac из той же платформы, что у работающего
        /// ras/агента, затем остальные платформы от новой к старой.
        /// </summary>
        internal static List<string> RacCandidates(ScanResult r, ClusterInfo c)
        {
            List<string> list = new List<string>();
            if (!string.IsNullOrEmpty(Program.RacOverride) && File.Exists(Program.RacOverride))
            {
                list.Add(Program.RacOverride);
                return list;
            }

            string aligned = RunningServerBinDir();
            if (aligned.Length > 0)
            {
                string p = Path.Combine(aligned, "rac.exe");
                if (File.Exists(p)) list.Add(p);
            }

            List<PlatformInfo> plats = new List<PlatformInfo>();
            foreach (PlatformInfo p in r.Platforms)
            {
                if (p.Family.IndexOf("8", StringComparison.Ordinal) < 0) continue;
                if (string.IsNullOrEmpty(p.BinPath)) continue;
                plats.Add(p);
            }
            // от новой версии к старой
            for (int i = 1; i < plats.Count; i++)
            {
                PlatformInfo key = plats[i];
                int j = i - 1;
                while (j >= 0 && CompareVersions(plats[j].Version, key.Version) < 0)
                {
                    plats[j + 1] = plats[j];
                    j--;
                }
                plats[j + 1] = key;
            }
            foreach (PlatformInfo p in plats)
            {
                string pth = Path.Combine(p.BinPath, "rac.exe");
                if (File.Exists(pth) && !list.Contains(pth)) list.Add(pth);
                if (list.Count >= 4) break;
            }
            if (!string.IsNullOrEmpty(c.RacPath) && File.Exists(c.RacPath) && !list.Contains(c.RacPath)) list.Add(c.RacPath);
            return list;
        }

        /// <summary>Версия платформы из пути («…\8.3.27.2342\bin\rac.exe» → «8.3.27.2342»).</summary>
        internal static string VersionOf(string path)
        {
            try
            {
                System.Text.RegularExpressions.Match m =
                    System.Text.RegularExpressions.Regex.Match(path ?? "", @"\d+\.\d+\.\d+\.\d+");
                if (m.Success) return m.Value;
                return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
            }
            catch { return ""; }
        }

        private static int CompareVersions(string a, string b)
        {
            string[] pa = (a ?? "").Split('.');
            string[] pb = (b ?? "").Split('.');
            int n = pa.Length > pb.Length ? pa.Length : pb.Length;
            for (int i = 0; i < n; i++)
            {
                int va = 0, vb = 0;
                if (i < pa.Length) int.TryParse(pa[i], out va);
                if (i < pb.Length) int.TryParse(pb[i], out vb);
                if (va != vb) return va > vb ? 1 : -1;
            }
            return 0;
        }

        /// <summary>Каталог bin работающего ras.exe или агента сервера — эталон версии для rac.</summary>
        internal static string RunningServerBinDir()
        {
            string result = "";
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT Name,CommandLine,ExecutablePath FROM Win32_Process WHERE Name='ras.exe' OR Name='ragent.exe'"))
                {
                    foreach (System.Management.ManagementBaseObject o in s.Get())
                    {
                        string path = Convert.ToString(o["ExecutablePath"]);
                        if (path.Length == 0)
                        {
                            string cmd = Convert.ToString(o["CommandLine"]).Trim();
                            if (cmd.Length > 0)
                            {
                                if (cmd[0] == '"')
                                {
                                    int close = cmd.IndexOf('"', 1);
                                    if (close > 1) path = cmd.Substring(1, close - 1);
                                }
                                else
                                {
                                    int sp = cmd.IndexOf(' ');
                                    path = sp > 0 ? cmd.Substring(0, sp) : cmd;
                                }
                            }
                        }
                        if (path.Length == 0) continue;
                        string dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir)) { result = dir; if (CanAlign(result)) return result; }
                    }
                }
            }
            catch { }

            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT Name,DisplayName,PathName,State FROM Win32_Service WHERE State='Running'"))
                {
                    foreach (System.Management.ManagementBaseObject o in s.Get())
                    {
                        string both = Convert.ToString(o["Name"]) + " " + Convert.ToString(o["DisplayName"]);
                        bool agent = both.IndexOf("Server Agent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     both.IndexOf("Агент сервера", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!agent) continue;
                        string path = Convert.ToString(o["PathName"]).Trim().Trim('"');
                        int exe = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (exe > 0) path = path.Substring(0, exe + 4);
                        string dir = Path.GetDirectoryName(path.Trim('"'));
                        if (!string.IsNullOrEmpty(dir) && CanAlign(dir)) return dir;
                    }
                }
            }
            catch { }
            return result;
        }

        private static bool CanAlign(string binDir)
        {
            try { return binDir.Length > 0 && File.Exists(Path.Combine(binDir, "rac.exe")); }
            catch { return false; }
        }

        /// <summary>
        /// Логины администраторов кластера — из «cluster admin list». Нужны, когда в приложении
        /// ввели не то имя: rac сам подсказывает, какие имена вообще существуют. Пароль берём
        /// только тот, что ввёл пользователь, — чужих паролей у нас нет.
        /// </summary>
        private static string[][] AdminNameCreds(ScanResult r, ClusterInfo c, string clusterId, string user, string password, string racPath)
        {
            if (string.IsNullOrEmpty(racPath)) racPath = c.RacPath;
            string key = c.Endpoint + "|" + clusterId + "|" + racPath;
            string[] names;
            if (!AdminNamesCache.TryGetValue(key, out names))
            {
                List<string> found = new List<string>();
                string[] logins = new string[] { user, "", "Администратор", "Administrator" };
                foreach (string login in logins)
                {
                    StringBuilder a = new StringBuilder("cluster admin list --cluster=").Append(clusterId);
                    if (!string.IsNullOrEmpty(login))
                    {
                        a.Append(" --cluster-user=\"").Append(login).Append("\"");
                        if (!string.IsNullOrEmpty(password)) a.Append(" --cluster-pwd=\"").Append(password).Append("\"");
                    }
                    a.Append(' ').Append(c.Endpoint);
                    string so, se;
                    int rc = Exec.Run(racPath, a.ToString(), 15000, out so, out se);
                    Diag.Log("rac cluster admin list [" + (login.Length == 0 ? "no-auth" : login) + "]: rc=" + rc +
                             " out=" + (so ?? "").Trim());
                    if (rc != 0 || LooksLikeError(so, se)) continue;
                    foreach (Record rec in ParseRecords(so, "name"))
                    {
                        string nm = rec.Get("name");
                        string auth = rec.Get("auth");
                        if (nm.Length == 0) continue;
                        // без пароля администратор не войдёт — при auth=os/publickey пропускаем
                        if (auth.Length > 0 && auth.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (!found.Contains(nm)) found.Add(nm);
                    }
                    if (found.Count > 0) break;
                }
                names = found.ToArray();
                AdminNamesCache[key] = names;
            }

            List<string[]> creds = new List<string[]>();
            foreach (string nm in names)
            {
                if (string.Equals(nm, user, StringComparison.Ordinal)) continue;
                creds.Add(new string[] { nm, password ?? "" });
            }
            return creds.ToArray();
        }

        internal static string SessionArgs(string endpoint, string clusterId, string login, string pwd, bool newStyle, bool licenses)
        {
            StringBuilder a = new StringBuilder();
            if (!newStyle) a.Append(endpoint).Append(' ');
            a.Append("session list --cluster=").Append(clusterId);
            if (licenses) a.Append(" --licenses");
            if (!string.IsNullOrEmpty(login)) a.Append(" --cluster-user=\"").Append(login).Append("\"");
            if (!string.IsNullOrEmpty(pwd)) a.Append(" --cluster-pwd=\"").Append(pwd).Append("\"");
            if (newStyle) a.Append(' ').Append(endpoint);
            return a.ToString();
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

        private static string StripQuotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"') s = s.Substring(1);
            if (s.Length >= 1 && s[s.Length - 1] == '"') s = s.Substring(0, s.Length - 1);
            return s.Trim();
        }

        private static bool LooksLikeError(string stdout, string stderr)
        {
            string err = (stderr ?? "").ToLowerInvariant();
            if (err.IndexOf("access denied", StringComparison.Ordinal) >= 0) return true;
            if (err.IndexOf("not authorized", StringComparison.Ordinal) >= 0) return true;
            if (err.IndexOf("отказано", StringComparison.Ordinal) >= 0) return true;
            if (err.IndexOf("недостаточно прав", StringComparison.Ordinal) >= 0) return true;
            if (err.IndexOf("ошибка", StringComparison.Ordinal) >= 0) return true;
            if (err.IndexOf("error", StringComparison.Ordinal) >= 0) return true;

            // stdout может содержать служебные поля со словом error (errors-count-threshold),
            // поэтому ищем только явные признаки ошибки в начале строки.
            string[] lines = (stdout ?? "").Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim().ToLowerInvariant();
                if (line.StartsWith("ошибка") || line.StartsWith("error:") ||
                    line.StartsWith("не удалось") || line.StartsWith("failed"))
                    return true;
            }
            return false;
        }

        private static string ShortError(string stdout, string stderr, int code)
        {
            string all = (stderr ?? "").Trim();
            if (all.Length == 0) all = (stdout ?? "").Trim();
            if (all.Length == 0) all = "код возврата " + code;
            string[] lines = all.Replace("\r", "").Split('\n');
            string s = "";
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                string lowLine = line.ToLowerInvariant();
                if (lowLine.StartsWith("ошибка") || lowLine.StartsWith("error:") ||
                    lowLine.StartsWith("не удалось") || lowLine.StartsWith("failed") ||
                    lowLine.IndexOf("access denied", StringComparison.Ordinal) >= 0 ||
                    lowLine.IndexOf("отказано", StringComparison.Ordinal) >= 0)
                {
                    s = line;
                    break;
                }
            }
            if (s.Length == 0) s = lines.Length > 0 ? lines[0].Trim() : all;
            if (s.Length > 300) s = s.Substring(0, 300) + "…";
            string low = all.ToLowerInvariant();
            if (low.IndexOf("access denied", StringComparison.Ordinal) >= 0 || low.IndexOf("отказано", StringComparison.Ordinal) >= 0)
                s += " · нужны логин и пароль администратора кластера";
            return s;
        }

        internal class Record
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

        internal static List<Record> ParseRecords(string text, string startKey)
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
