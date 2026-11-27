using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Подробная диагностика кластера 1С 8.3 через rac: кто администратор и каким способом
    /// он аутентифицируется, видит ли rac информационные базы, рабочие процессы и сеансы.
    /// Нужна, когда rac отвечает «Администратор кластера не аутентифицирован» или когда
    /// список сеансов пустой, а в консоли администрирования сеансы есть.
    ///
    /// Отчёт пишется в файл целиком (stdout/stderr каждой команды) — пароль в отчёт
    /// не попадает, печатается только его длина.
    /// </summary>
    internal static class RacProbe
    {
        internal class AdminRow
        {
            public string Name = "";
            public string Auth = "";
            public string OsUser = "";
            public string Descr = "";
        }

        internal class Cmd
        {
            public string Title = "";
            public string Args = "";
            public int Rc;
            public string Out = "";
            public string Err;

            public Cmd() { Err = ""; }

            public bool Ok { get { return Rc == 0 && (Err ?? "").Trim().Length == 0; } }
            public bool HasOut { get { return (Out ?? "").Trim().Length > 0; } }
        }

        private static string _rac = "";
        private static string _endpoint = "localhost:1545";
        private static string _user = "";
        private static string _pwd = "";
        private static string _clusterId = "";
        private static string _clusterName = "";
        private static string _workingUser = "";
        private static int _sessionCount;
        private static int _processCount;

        private static List<Cmd> _cmds;
        private static List<AdminRow> _admins;
        private static List<string> _infobases;
        private static StringBuilder _body;
        private static List<string> _verdict;

        /// <summary>Полный отчёт в виде текста.</summary>
        public static string Build(ScanResult r, string endpoint, string user, string pwd)
        {
            _cmds = new List<Cmd>();
            _admins = new List<AdminRow>();
            _infobases = new List<string>();
            _body = new StringBuilder();
            _verdict = new List<string>();
            _sessionCount = 0;
            _processCount = 0;
            _clusterId = "";
            _clusterName = "";
            _workingUser = "";

            _endpoint = string.IsNullOrEmpty(endpoint) ? "localhost:1545" : endpoint.Trim();
            if (_endpoint.IndexOf(':') < 0) _endpoint += ":1545";
            _user = (user ?? "").Trim();
            _pwd = pwd ?? "";
            _rac = "";
            try { _rac = CollectCluster.FindRac(r); }
            catch { }

            Section("1. Окружение");
            WriteEnvironment(r);

            if (_rac.Length == 0)
            {
                _verdict.Add("rac.exe не найден: нужна установленная платформа 1С 8.3.");
            }
            else
            {
                Section("2. Утилита rac");
                Do("Версия rac", "--version");
                Section("3. Кластеры (cluster list)");
                ProbeClusters();
            }

            return Compose(r);
        }

        public static string DefaultPath()
        {
            string name = "racprobe-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            try { return Path.Combine(AutoDrop.Dir(), name); }
            catch { return Path.Combine(Path.GetTempPath(), name); }
        }

        public static string BuildAndSave(ScanResult r, string endpoint, string user, string pwd, out string path)
        {
            string text = Build(r, endpoint, user, pwd);
            path = DefaultPath();
            try { Program.WriteText(path, text, true); }
            catch { }
            try { Program.WriteText(Path.Combine(Path.GetDirectoryName(path), "latest-racprobe.txt"), text, true); }
            catch { }
            return text;
        }

        // ─────────────────────────── сбор ───────────────────────────

        private static void ProbeClusters()
        {
            Cmd list = Do("Список кластеров (режим и команда первыми — 8.3.24+)", "cluster list " + _endpoint);
            if (!list.Ok)
                list = Do("Список кластеров (старый синтаксис: адрес первым)", _endpoint + " cluster list");

            if (!list.Ok)
            {
                _verdict.Add("cluster list не сработал: сервер администрирования (ras) не отвечает или rac другой версии. Причина: " + Trim(list.Err.Length > 0 ? list.Err : list.Out));
                return;
            }

            List<CollectCluster.Record> clusters = CollectCluster.ParseRecords(list.Out, "cluster");
            if (clusters.Count == 0)
            {
                _verdict.Add("Кластеры не найдены: cluster list вернул пустой список.");
                return;
            }

            int n = 0;
            foreach (CollectCluster.Record rec in clusters)
            {
                if (++n > 4) break;
                _clusterId = rec.Get("cluster");
                _clusterName = rec.Get("name");
                _verdict.Add("Кластер: " + _clusterId + " " + Trim(_clusterName) + " (host=" + rec.Get("host") + ", port=" + rec.Get("port") + ")");
            }
            if (_clusterId.Length == 0) return;

            Section("4. Администраторы кластера (cluster admin list)");
            ProbeAdmins();

            Section("5. Вход в кластер и список сеансов (session list)");
            ProbeSessionLogins();

            if (_workingUser.Length > 0 || _user.Length == 0)
            {
                Section("6. Свойства кластера (cluster info)");
                Do("Свойства кластера", Creds("cluster info --cluster=" + _clusterId, _workingUser));
            }

            Section("7. Информационные базы (infobase list)");
            ProbeInfobases();

            Section("8. Рабочие серверы, процессы и соединения");
            Do("Рабочие серверы (server list)", Creds("server list --cluster=" + _clusterId, _workingUser));
            Cmd procs = Do("Рабочие процессы (process list)", Creds("process list --cluster=" + _clusterId, _workingUser));
            if (procs.Ok)
            {
                _processCount = CollectCluster.ParseRecords(procs.Out, "process").Count;
            }
            Do("Соединения (connection list)", Creds("connection list --cluster=" + _clusterId, _workingUser));

            if (_infobases.Count > 0)
            {
                Section("9. Сеансы по каждой информационной базе");
                int shown = 0;
                int inBases = 0;
                foreach (string ib in _infobases)
                {
                    string uuid = ib;
                    int bar = ib.IndexOf('|');
                    string name = bar > 0 ? ib.Substring(bar + 1) : ib;
                    if (bar > 0) uuid = ib.Substring(0, bar);
                    if (uuid.Trim().Length == 0) continue;
                    if (++shown > 12) break;
                    Cmd one = Do("Сеансы ИБ " + Trim(name),
                        Creds("session list --cluster=" + _clusterId + " --infobase=" + uuid + " --licenses", _workingUser));
                    if (one.Ok) inBases += CollectCluster.ParseRecords(one.Out, "session").Count;
                }
                if (_sessionCount == 0 && inBases > 0)
                {
                    _sessionCount = inBases;
                    _verdict.Add("По инфобазам найдено сеансов: " + inBases + " (общий список их не показал).");
                }
            }
        }

        private static void ProbeAdmins()
        {
            Cmd anonymous = Do("Администраторы без логина", "cluster admin list --cluster=" + _clusterId);
            Cmd authed = null;
            if (!anonymous.Ok)
                authed = Do("Администраторы с указанным логином", Creds("cluster admin list --cluster=" + _clusterId, _user));

            Cmd src = null;
            if (authed != null && authed.Ok) src = authed;
            else if (anonymous.Ok) src = anonymous;

            if (src == null)
            {
                _verdict.Add("Список администраторов кластера прочитать не удалось — ни без прав, ни с указанным логином.");
                return;
            }

            List<CollectCluster.Record> recs = CollectCluster.ParseRecords(src.Out, "name");
            if (recs.Count == 0)
            {
                _verdict.Add("Список администраторов кластера пуст (администраторы не заданы).");
                return;
            }

            foreach (CollectCluster.Record rec in recs)
            {
                AdminRow a = new AdminRow();
                a.Name = rec.Get("name");
                a.Auth = rec.Get("auth");
                a.OsUser = rec.Get("os-user");
                a.Descr = rec.Get("descr");
                if (a.Name.Length == 0) continue;
                _admins.Add(a);
            }

            List<string> brief = new List<string>();
            foreach (AdminRow a in _admins)
                brief.Add(a.Name + " (" + (a.Auth.Length == 0 ? "способ не указан" : a.Auth) + ")");
            _verdict.Add("Администраторы кластера: " + string.Join(", ", brief.ToArray()));

            foreach (AdminRow a in _admins)
            {
                if (a.Auth.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) < 0)
                    _verdict.Add("У администратора «" + a.Name + "» нет входа по паролю (способы: " + Trim(a.Auth) + "). " +
                                 "rac с --cluster-user/--cluster-pwd подойдёт только при pwd; иначе нужен --cluster-key (открытый ключ) " +
                                 "или вход средствами ОС.");
            }
        }

        private static void ProbeSessionLogins()
        {
            List<string> tried = new List<string>();
            Cmd hit = null;

            if (_user.Length > 0)
            {
                Cmd c = SessionsAttempt("логин из приложения: " + _user, _user);
                tried.Add(_user);
                if (c != null && c.Ok) { hit = c; _workingUser = _user; }
            }

            if (hit == null)
            {
                Cmd anon = Do("Без логина (анонимно)", "session list --cluster=" + _clusterId + " --licenses");
                if (!anon.Ok) Do("Без логина, старый синтаксис", _endpoint + " session list --cluster=" + _clusterId);
                if (anon.Ok && anon.HasOut) { hit = anon; _workingUser = ""; }
            }

            if (hit == null)
            {
                int n = 0;
                foreach (AdminRow a in _admins)
                {
                    if (++n > 4) break;
                    if (a.Auth.Length > 0 && a.Auth.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool already = false;
                    foreach (string t in tried) if (string.Equals(t, a.Name, StringComparison.Ordinal)) already = true;
                    if (already) continue;
                    if (_pwd.Length == 0) continue;
                    Cmd c = SessionsAttempt("логин из списка администраторов: " + a.Name, a.Name);
                    tried.Add(a.Name);
                    if (c != null && c.Ok) { hit = c; _workingUser = a.Name; break; }
                }
            }

            if (hit != null) _sessionCount = CollectCluster.ParseRecords(hit.Out, "session").Count;

            if (hit != null)
            {
                _verdict.Add("Вход в кластер: успешно" + (_workingUser.Length > 0 ? " логином «" + _workingUser + "»" : " без логина") +
                             ". Сеансов: " + _sessionCount + ".");
                if (_workingUser.Length > 0 && _user.Length > 0 && !string.Equals(_workingUser, _user, StringComparison.Ordinal))
                    _verdict.Add("Логин «" + _user + "» не подошёл, а «" + _workingUser + "» подошёл — подставьте его в поле «Логин».");
            }
            else
            {
                _verdict.Add("Вход в кластер не удался ни одним логином (перебрано: " +
                             (tried.Count == 0 ? "без логина" : string.Join(", ", tried.ToArray())) + ").");
                if (_admins.Count == 0)
                    _verdict.Add("Список администраторов прочитать не удалось, поэтому подсказать логин нечем. " +
                                 "Проверьте в консоли администрирования: Кластеры → <кластер> → Администраторы — имена и способ аутентификации.");
            }

            if (_sessionCount == 0 && hit != null)
                _verdict.Add("Сеансов ноль. Если в консоли они видны — опросите именно в тот момент, когда пользователи подключены " +
                             "(сеансы кластера живут, пока клиент на связи).");
        }

        private static Cmd SessionsAttempt(string title, string login)
        {
            Cmd c = Do("Сеансы + лицензии, " + title,
                Creds("session list --cluster=" + _clusterId + " --licenses", login));
            if (!c.Ok)
                c = Do("Сеансы + лицензии, старый синтаксис, " + title,
                    _endpoint + " " + Creds("session list --cluster=" + _clusterId + " --licenses", login));
            if (!c.Ok)
                c = Do("Сеансы без --licenses, " + title,
                    Creds("session list --cluster=" + _clusterId, login));
            return c;
        }

        private static void ProbeInfobases()
        {
            Cmd list = Do("Список ИБ", Creds("infobase list --cluster=" + _clusterId, _workingUser));
            if (!list.Ok || !list.HasOut)
            {
                Cmd summary = Do("Краткий список ИБ", Creds("infobase summary list --cluster=" + _clusterId, _workingUser));
                if (summary.Ok && summary.HasOut) list = summary;
            }
            if (!list.Ok || !list.HasOut)
            {
                _verdict.Add("Инфобазы через rac не читаются — значит прав администратора кластера нет.");
                return;
            }

            foreach (CollectCluster.Record rec in CollectCluster.ParseRecords(list.Out, "infobase"))
            {
                string uuid = rec.Get("infobase");
                string name = rec.Get("name");
                if (uuid.Length == 0) continue;
                _infobases.Add(uuid + "|" + name);
            }
            _verdict.Add("Инфобаз в кластере: " + _infobases.Count + ".");
        }

        // ─────────────────────────── окружение ───────────────────────────

        private static void WriteEnvironment(ScanResult r)
        {
            Line("Компьютер", r.System.ComputerName + " · " + r.System.OsCaption + " " + r.System.OsArch);
            string who = (r.System.Domain ?? "") + "\\" + (r.System.UserName ?? "");
            Line("Пользователь", who.Trim('\\') + " · права администратора: " + (IsElevated() ? "да" : "НЕТ (rac может не увидеть кластер)"));
            Line("Сервер администрирования", _endpoint + " · порт слушается: " + (PortListening(PortOf(_endpoint)) ? "да" : "НЕТ"));
            Line("rac", _rac.Length == 0 ? "не найден" : _rac);
            Line("Логин кластера", _user.Length == 0 ? "(не задан)" : _user);
            Line("Пароль кластера", _pwd.Length == 0 ? "(пустой)" : "задан, " + _pwd.Length + " симв. (значение в отчёт не пишется)");

            List<string> agents = new List<string>();
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT Name,DisplayName,State,StartMode FROM Win32_Service"))
                {
                    foreach (System.Management.ManagementBaseObject o in s.Get())
                    {
                        string name = Convert.ToString(o["Name"]);
                        string disp = Convert.ToString(o["DisplayName"]);
                        string both = name + " " + disp;
                        if (both.IndexOf("1cv8", StringComparison.OrdinalIgnoreCase) < 0 &&
                            both.IndexOf("1C:Enterprise", StringComparison.OrdinalIgnoreCase) < 0 &&
                            both.IndexOf("1С:Предприятие", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        agents.Add(Convert.ToString(o["State"]) + " · " + name + " · " + disp);
                    }
                }
            }
            catch { }
            if (agents.Count == 0) Line("Службы 1С", "не найдены");
            else foreach (string a in agents) Line("Служба 1С", a);

            List<string> ras = RasProcesses();
            if (ras.Count == 0) Line("Процессы ras.exe", "запущенных нет (ras поднимает служба или его стартует приложение)");
            else foreach (string a in ras) Line("Процесс ras.exe", a);

            List<string> ports = new List<string>();
            try
            {
                foreach (IPEndPoint ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (ep.Port == 1545 || ep.Port == 1540 || ep.Port == 1541) ports.Add(ep.Address + ":" + ep.Port);
                }
            }
            catch { }
            Line("Порты 1С", ports.Count == 0 ? "1540/1541/1545 не слушаются" : string.Join(", ", ports.ToArray()));

            List<string> plats = new List<string>();
            foreach (PlatformInfo p in r.Platforms)
            {
                string v = p.Version + (string.IsNullOrEmpty(p.Family) ? "" : " (" + p.Family + ")");
                if (!plats.Contains(v)) plats.Add(v);
            }
            string platsText;
            if (plats.Count == 0) platsText = "не найдены";
            else
            {
                List<string> few = plats.Count > 6 ? plats.GetRange(0, 6) : plats;
                platsText = string.Join(", ", few.ToArray());
                if (plats.Count > 6) platsText += " и ещё " + (plats.Count - 6) + " (всего " + plats.Count + ")";
            }
            Line("Платформы", platsText);

            try
            {
                string srvinfo = Path.Combine(Paths.ProgramFiles, "1cv8", "srvinfo");
                if (Directory.Exists(srvinfo))
                {
                    string[] dirs = Directory.GetDirectories(srvinfo);
                    string[] files = Directory.GetFiles(srvinfo);
                    Line("Реестр кластера", srvinfo + " · каталогов: " + dirs.Length + " · файлов: " + files.Length);
                }
                else Line("Реестр кластера", srvinfo + " — каталога нет (агент сервера не установлен локально)");
            }
            catch { }
        }

        private static List<string> RasProcesses()
        {
            List<string> list = new List<string>();
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='ras.exe'"))
                {
                    foreach (System.Management.ManagementBaseObject o in s.Get())
                    {
                        list.Add("pid=" + Convert.ToString(o["ProcessId"]) + " · " + Convert.ToString(o["CommandLine"]));
                    }
                }
            }
            catch { }
            return list;
        }

        private static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static bool PortListening(int port)
        {
            try
            {
                foreach (IPEndPoint ep in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                    if (ep.Port == port) return true;
            }
            catch { }
            return false;
        }

        private static int PortOf(string endpoint)
        {
            try
            {
                int idx = endpoint.LastIndexOf(':');
                if (idx < 0) return 1545;
                int port;
                if (int.TryParse(endpoint.Substring(idx + 1), out port)) return port;
            }
            catch { }
            return 1545;
        }

        // ─────────────────────────── утилиты ───────────────────────────

        private static string Creds(string command, string login)
        {
            if (string.IsNullOrEmpty(login)) return command;
            StringBuilder a = new StringBuilder(command);
            a.Append(" --cluster-user=\"").Append(login).Append("\"");
            if (_pwd.Length > 0) a.Append(" --cluster-pwd=\"").Append(_pwd).Append("\"");
            a.Append(' ').Append(_endpoint);
            return a.ToString();
        }

        private static Cmd Do(string title, string args)
        {
            Cmd c = new Cmd();
            c.Title = title;
            c.Args = Mask(args);
            string tail = " " + _endpoint;
            if (args.EndsWith(tail, StringComparison.Ordinal)) c.Args = Mask(args.Substring(0, args.Length - tail.Length)) + tail;

            string so, se;
            try { c.Rc = Exec.Run(_rac, args, 20000, out so, out se); }
            catch (Exception ex) { c.Rc = -1; so = ""; se = ex.Message; }
            c.Out = so ?? "";
            c.Err = se ?? "";
            _cmds.Add(c);
            WriteCommand(c);
            return c;
        }

        private static string Mask(string args)
        {
            string s = args ?? "";
            if (_pwd.Length > 0) s = s.Replace(_pwd, "***");
            return s;
        }

        private static void WriteCommand(Cmd c)
        {
            _body.AppendLine();
            _body.AppendLine("── " + c.Title);
            string head = c.Args.StartsWith(_endpoint + " ", StringComparison.Ordinal)
                ? "rac " + c.Args
                : "rac " + c.Args;
            _body.AppendLine("   команда: " + head);
            _body.AppendLine("   код возврата: " + c.Rc +
                             " · stdout: " + (c.HasOut ? CountLines(c.Out) + " строк" : "пусто") +
                             " · stderr: " + ((c.Err ?? "").Trim().Length > 0 ? CountLines(c.Err) + " строк" : "пусто"));
            if (c.HasOut)
            {
                _body.AppendLine("   --- stdout ---");
                AppendBlock(c.Out);
            }
            if ((c.Err ?? "").Trim().Length > 0)
            {
                _body.AppendLine("   --- stderr ---");
                AppendBlock(c.Err);
            }
        }

        private static void AppendBlock(string text)
        {
            string[] lines = (text ?? "").Replace("\r", "").Split('\n');
            int max = lines.Length > 400 ? 400 : lines.Length;
            for (int i = 0; i < max; i++) _body.AppendLine("   | " + lines[i].TrimEnd());
            if (lines.Length > max) _body.AppendLine("   | … ещё " + (lines.Length - max) + " строк");
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Replace("\r", "").Split('\n').Length;
        }

        private static void Section(string title)
        {
            _body.AppendLine();
            _body.AppendLine("════ " + title + " ════");
        }

        private static void Line(string key, string value)
        {
            string pad = key.Length < 24 ? key + new string(' ', 24 - key.Length) : key + " ";
            _body.AppendLine("   " + pad + value);
        }

        private static string Trim(string s)
        {
            s = (s ?? "").Trim().Trim('"');
            return s.Length > 120 ? s.Substring(0, 120) + "…" : s;
        }

        private static string Compose(ScanResult r)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Инспектор лицензий 1С " + Program.Version + " — диагностика кластера 8.3 через rac");
            sb.AppendLine("Отчёт создан: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") +
                          " · " + r.System.ComputerName + " · " + r.System.OsCaption + " " + r.System.OsArch);
            sb.AppendLine("Пароль в отчёт не пишется — только признак «задан» и длина.");
            sb.AppendLine();
            sb.AppendLine("────────────────────────────  ИТОГ  ────────────────────────────");
            if (_verdict.Count == 0) sb.AppendLine("   · данных нет");
            else foreach (string v in _verdict) sb.AppendLine("   · " + v);

            if (r.Cluster != null)
            {
                sb.AppendLine();
                sb.AppendLine("───────────────────  что увидело приложение  ───────────────────");
                sb.AppendLine("   доступен: " + (r.Cluster.Available ? "да" : "нет") +
                              " · сеансов: " + r.Cluster.Sessions.Count +
                              " · процессов: " + r.Cluster.Processes.Count);
                if (!string.IsNullOrEmpty(r.Cluster.Error)) sb.AppendLine("   ошибка: " + r.Cluster.Error);
                if (!string.IsNullOrEmpty(r.Cluster.Note)) sb.AppendLine("   пояснение: " + r.Cluster.Note);
            }

            sb.AppendLine();
            sb.Append(_body.ToString());
            sb.AppendLine();
            sb.AppendLine("───────────────────────────  конец отчёта  ───────────────────────────");
            return sb.ToString();
        }
    }
}
