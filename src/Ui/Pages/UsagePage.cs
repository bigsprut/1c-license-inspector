using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal class UsagePage : PageBase
    {
        private SegmentedTabs _tabs;
        private Panel _host;
        private DataGridView _processes;
        private DataGridView _services;
        private DataGridView _ports;

        private Panel _sessPanel;
        private DataGridView _sessions;
        private DataGridView _procs;
        private TextBox _sessDetails;
        private Label _sessHint;
        private TextBox _sessServer;
        private TextBox _sessUser;
        private TextBox _sessPwd;
        private FlatButton _sessRefresh;
        private FlatButton _sessImport;
        private FlatButton _sessConsole;
        private FlatButton _sessGrab;
        private FlatButton _sessAuto;
        private ScanResult _result;
        private List<SessionEntry> _sessionItems = new List<SessionEntry>();

        public UsagePage()
        {
            AddHeader("Использование", "Процессы 1С, службы защиты, порты и сеансы с выданными лицензиями");

            _tabs = new SegmentedTabs();
            _tabs.Items.Add("Процессы");
            _tabs.Items.Add("Службы");
            _tabs.Items.Add("Порты");
            _tabs.Items.Add("Сеансы и лицензии");
            _tabs.Height = 42;
            _tabs.SelectedIndex = Math.Max(0, Math.Min(3, Program.InitialUsageTab));
            AddRow(_tabs, 44);
            AddSpacer(4);

            _processes = MakeGrid();
            _processes.Columns.Add(Col("Процесс", "Name", 90));
            _processes.Columns.Add(Col("PID", "Pid", 45));
            _processes.Columns.Add(Col("Память", "Memory", 70));
            _processes.Columns.Add(Col("Запущен", "Started", 90));
            _processes.Columns.Add(Col("Путь", "Path", 260));

            _services = MakeGrid();
            _services.Columns.Add(Col("Служба", "Name", 110));
            _services.Columns.Add(Col("Отображаемое имя", "DisplayName", 190));
            _services.Columns.Add(Col("Состояние", "State", 80));
            _services.Columns.Add(Col("Запуск", "StartMode", 80));

            _ports = MakeGrid();
            _ports.Columns.Add(Col("Порт", "PortText", 45));
            _ports.Columns.Add(Col("Протокол", "Protocol", 55));
            _ports.Columns.Add(Col("Процесс", "Owner", 110));
            _ports.Columns.Add(Col("PID", "PidText", 45));
            _ports.Columns.Add(Col("Назначение", "Role", 190));

            _host = new Panel();
            _host.BackColor = Color.White;
            _host.Dock = DockStyle.Fill;
            _host.Padding = new Padding(1);
            foreach (DataGridView g in new DataGridView[] { _processes, _services, _ports })
            {
                g.Dock = DockStyle.Fill;
                g.Visible = false;
                _host.Controls.Add(g);
            }
            _host.Controls.Add(BuildSessionsPanel());
            AddRow(Card(_host, new Padding(1)), 478);

            _tabs.SelectedIndexChanged += delegate { if (IsHandleCreated) ShowTab(); };
            HandleCreated += delegate { ShowTab(); };
            if (IsHandleCreated) ShowTab();
        }

        private Panel BuildSessionsPanel()
        {
            _sessPanel = new Panel();
            _sessPanel.Dock = DockStyle.Fill;
            _sessPanel.BackColor = Color.White;
            _sessPanel.Visible = false;

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 44;
            top.Padding = new Padding(6, 6, 6, 4);
            top.WrapContents = false;
            top.BackColor = Color.White;

            top.Controls.Add(LabelFor("Сервер"));
            _sessServer = new TextBox();
            _sessServer.Width = 150;
            _sessServer.Text = string.IsNullOrEmpty(Program.RasEndpoint) ? "localhost:1545" : Program.RasEndpoint;
            _sessServer.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessServer);

            top.Controls.Add(LabelFor("Логин"));
            _sessUser = new TextBox();
            _sessUser.Width = 110;
            _sessUser.Text = Program.ClusterUser;
            _sessUser.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessUser);

            top.Controls.Add(LabelFor("Пароль"));
            _sessPwd = new TextBox();
            _sessPwd.Width = 110;
            _sessPwd.UseSystemPasswordChar = true;
            _sessPwd.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessPwd);

            _sessRefresh = new FlatButton();
            _sessRefresh.Text = "Опросить";
            _sessRefresh.Icon = "refresh";
            _sessRefresh.Kind = ButtonKind.Primary;
            _sessRefresh.Size = new Size(132, 30);
            _sessRefresh.Margin = new Padding(2, 2, 4, 2);
            _sessRefresh.Click += delegate { RefreshCluster(); };
            top.Controls.Add(_sessRefresh);

            _sessImport = new FlatButton();
            _sessImport.Text = "Из консоли 8.2";
            _sessImport.Icon = "file";
            _sessImport.Kind = ButtonKind.Secondary;
            _sessImport.Size = new Size(160, 30);
            _sessImport.Margin = new Padding(2, 2, 4, 2);
            _sessImport.Click += delegate { ImportConsole82(); };
            top.Controls.Add(_sessImport);

            _sessConsole = new FlatButton();
            _sessConsole.Text = "Открыть консоль";
            _sessConsole.Icon = "key";
            _sessConsole.Kind = ButtonKind.Ghost;
            _sessConsole.Size = new Size(150, 30);
            _sessConsole.Margin = new Padding(2, 2, 4, 2);
            _sessConsole.Click += delegate { OpenConsole82(); };
            top.Controls.Add(_sessConsole);

            _sessGrab = new FlatButton();
            _sessGrab.Text = "Взять из консоли";
            _sessGrab.Icon = "refresh";
            _sessGrab.Kind = ButtonKind.Primary;
            _sessGrab.Size = new Size(170, 30);
            _sessGrab.Margin = new Padding(2, 2, 4, 2);
            _sessGrab.Click += delegate { GrabFromConsole(); };
            top.Controls.Add(_sessGrab);

            _sessAuto = new FlatButton();
            _sessAuto.Text = "Собрать авто";
            _sessAuto.Icon = "refresh";
            _sessAuto.Kind = ButtonKind.Primary;
            _sessAuto.Size = new Size(150, 30);
            _sessAuto.Margin = new Padding(2, 2, 4, 2);
            _sessAuto.Click += delegate { AutoCollect82(); };
            top.Controls.Add(_sessAuto);

            _sessHint = new Label();
            _sessHint.Dock = DockStyle.Bottom;
            _sessHint.Height = 30;
            _sessHint.Padding = new Padding(10, 6, 10, 4);
            _sessHint.ForeColor = Theme.Muted;
            _sessHint.Font = Theme.Small;
            _sessHint.Text = "";

            _sessDetails = new TextBox();
            _sessDetails.Multiline = true;
            _sessDetails.ReadOnly = true;
            _sessDetails.ScrollBars = ScrollBars.Vertical;
            _sessDetails.BorderStyle = BorderStyle.None;
            _sessDetails.BackColor = Color.FromArgb(250, 251, 252);
            _sessDetails.ForeColor = Theme.InkSoft;
            _sessDetails.Font = Theme.MonoFont(8.5f);
            _sessDetails.Dock = DockStyle.Bottom;
            _sessDetails.Height = 96;
            EnableCopy(_sessDetails);

            _procs = MakeGrid();
            _procs.Columns.Add(Col("Компьютер", "Computer", 100));
            _procs.Columns.Add(Col("Порт", "Port", 60));
            _procs.Columns.Add(Col("Использование", "Usage", 100));
            _procs.Columns.Add(Col("Включен", "Enabled", 70));
            _procs.Columns.Add(Col("Активен", "Active", 70));
            _procs.Columns.Add(Col("PID", "Pid", 60));
            _procs.Columns.Add(Col("Память, KB", "Memory", 85));
            _procs.Columns.Add(Col("Лицензия", "License", 80));
            _procs.Columns.Add(Col("PIN", "Pin", 110));
            _procs.Columns.Add(Col("Мест", "Seats", 60));
            _procs.Columns.Add(Col("Занято", "Used", 60));
            _procs.Columns.Add(Col("Свободно", "Free", 60));
            _procs.Dock = DockStyle.Top;
            _procs.Height = 150;

            _sessions = MakeGrid();
            _sessions.Columns.Add(Col("Инф. база", "Infobase", 100));
            _sessions.Columns.Add(Col("Номер", "SessionId", 70));
            _sessions.Columns.Add(Col("Начало", "StartedAt", 140));
            _sessions.Columns.Add(Col("Компьютер", "Host", 110));
            _sessions.Columns.Add(Col("Пользователь", "User", 130));
            _sessions.Columns.Add(Col("Приложение", "AppId", 110));
            _sessions.Columns.Add(Col("Лицензия", "License", 80));
            _sessions.Columns.Add(Col("PIN", "Pin", 110));
            _sessions.Columns.Add(Col("Мест", "Seats", 60));
            _sessions.Columns.Add(Col("Занято", "Used", 60));
            _sessions.Columns.Add(Col("Свободно", "Free", 60));
            _sessions.Dock = DockStyle.Fill;
            _sessions.SelectionChanged += delegate { ShowSessionDetails(); };

            _sessPanel.Controls.Add(_sessions);
            _sessPanel.Controls.Add(_procs);
            _sessPanel.Controls.Add(_sessDetails);
            _sessPanel.Controls.Add(_sessHint);
            _sessPanel.Controls.Add(top);
            return _sessPanel;
        }

        private static Label LabelFor(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Theme.Muted;
            l.Font = Theme.Body;
            l.Margin = new Padding(2, 7, 0, 4);
            return l;
        }

        private DataGridViewColumn Col(string header, string prop, int weight)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.DataPropertyName = prop;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            return c;
        }

        private void ShowTab()
        {
            if (!IsHandleCreated) return;
            bool sessions = _tabs.SelectedIndex == 3;
            _sessPanel.Visible = sessions;
            if (sessions) _sessPanel.BringToFront();
            ShowGrid(_processes, _tabs.SelectedIndex == 0);
            ShowGrid(_services, _tabs.SelectedIndex == 1);
            ShowGrid(_ports, _tabs.SelectedIndex == 2);
        }

        private static void ShowGrid(Control g, bool visible)
        {
            g.Visible = visible;
            if (visible) g.BringToFront();
        }

        public override void Bind(ScanResult r)
        {
            _result = r;

            List<ProcRow> procs = new List<ProcRow>();
            foreach (ProcessInfo p in r.Processes)
            {
                ProcRow row = new ProcRow();
                row.Name = p.Name + (p.IsHasp ? " (защита)" : "");
                row.Pid = p.Pid.ToString();
                row.Memory = Fmt.Bytes(p.WorkingSet);
                row.Started = p.Started;
                row.Path = p.Path;
                procs.Add(row);
            }
            _processes.DataSource = null;
            _processes.DataSource = procs;

            List<SvcRow> svcs = new List<SvcRow>();
            foreach (ServiceInfo s in r.Services)
            {
                SvcRow row = new SvcRow();
                row.Name = s.Name;
                row.DisplayName = s.DisplayName;
                row.State = s.State;
                row.StartMode = s.StartMode;
                svcs.Add(row);
            }
            _services.DataSource = null;
            _services.DataSource = svcs;

            List<PortRow> ports = new List<PortRow>();
            foreach (PortInfo p in r.Ports)
            {
                if (p.Port < 1024 && string.IsNullOrEmpty(p.Role)) continue;
                PortRow row = new PortRow();
                row.PortText = p.Port.ToString();
                row.Protocol = p.Protocol;
                row.Owner = string.IsNullOrEmpty(p.Owner) ? "—" : p.Owner;
                row.PidText = p.Pid.ToString();
                row.Role = p.Role;
                ports.Add(row);
            }
            _ports.DataSource = null;
            _ports.DataSource = ports;

            BindSessions();
        }

        private static string FindMscPath()
        {
            string[] probes = new string[]
            {
                Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers (x86-64).msc"),
                Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers.msc"),
                Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers.msc"),
                Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers (x86-64).msc")
            };
            foreach (string msc in probes) if (File.Exists(msc)) return msc;
            return "";
        }

        private void AutoCollect82()
        {
            string msc = FindMscPath();
            string errProc = "", errSess = "";
            string procTsv = null, sessTsv = null;

            if (msc.Length > 0)
            {
                procTsv = Mmc20Reader.ReadTable(msc, "Рабочие процессы", out errProc);
                if (procTsv == null) procTsv = Mmc20Reader.ReadTable(msc, "Working processes", out errProc);
                sessTsv = Mmc20Reader.ReadTable(msc, "Сеансы", out errSess);
                if (sessTsv == null) sessTsv = Mmc20Reader.ReadTable(msc, "Sessions", out errSess);
            }
            else
            {
                errProc = "оснастка 1CV8 Servers.msc не найдена";
            }

            Diag.Log("auto82(MMC20): proc=" + (procTsv != null ? "ok" : "null") + " (" + errProc + "), sess=" + (sessTsv != null ? "ok" : "null") + " (" + errSess + ")");

            string s1 = "", s2 = "";
            if (!string.IsNullOrEmpty(procTsv)) Console82Apply.TryApply(_result, procTsv, out s1);
            if (!string.IsNullOrEmpty(sessTsv)) Console82Apply.TryApply(_result, sessTsv, out s2);

            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            {
                // Откат: окно консоли + Ctrl+C
                if (Console82Automation.FindConsole() == IntPtr.Zero) OpenConsole82();
                string err;
                string clip = Console82Automation.CopyTable(out err);
                if (!string.IsNullOrEmpty(clip) && Console82Apply.TryApply(_result, clip, out s1))
                {
                    BindSessions();
                    _sessHint.Text = "Взято из консоли 8.2: " + s1;
                    return;
                }
                MessageBox.Show("Не удалось прочитать консоль через MMC 2.0.\r\nПроцессы: " + errProc +
                                "\r\nСеансы: " + errSess + "\r\nОкно/буфер: " + err);
                return;
            }

            BindSessions();
            _sessHint.Text = "Автосбор (MMC 2.0): " + (s1.Length > 0 ? "серверные — " + s1 : "серверные — нет данных") +
                             " · " + (s2.Length > 0 ? "клиентские — " + s2 : "клиентские — нет данных");
        }

        private void GrabFromConsole()
        {
            string error;
            string text = Console82Automation.CopyTable(out error);
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show(string.IsNullOrEmpty(error)
                    ? "Не удалось получить таблицу из консоли."
                    : "Не удалось получить таблицу: " + error);
                return;
            }
            string summary;
            if (_result == null || !Console82Apply.TryApply(_result, text, out summary))
            {
                MessageBox.Show("В буфере нет таблицы сеансов/процессов 1С 8.2. Щёлкните по таблице в консоли и повторите.");
                return;
            }
            BindSessions();
            _sessHint.Text = "Взято из консоли 8.2: " + summary;
        }
        private void OpenConsole82()
        {
            try
            {
                string[] probes = new string[]
                {
                    Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers.msc"),
                    Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers (x86-64).msc"),
                    Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers.msc"),
                    Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers (x86-64).msc")
                };
                foreach (string msc in probes)
                {
                    if (!File.Exists(msc)) continue;
                    System.Diagnostics.Process.Start("mmc.exe", "\"" + msc + "\"");
                    return;
                }
                MessageBox.Show("Не найдена оснастка «1CV8 Servers.msc». Откройте её вручную: mmc.exe → Файл → Добавить/удалить оснастку.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть консоль: " + ex.Message);
            }
        }
        private void ImportConsole82()
        {
            string text = "";
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); }
            catch { }
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Скопируйте таблицу сеансов в консоли администрирования 1С 8.2 (Ctrl+C) — программа подхватит её сама, либо нажмите кнопку ещё раз.");
                return;
            }
            string summary;
            if (_result == null || !Console82Apply.TryApply(_result, text, out summary))
            {
                MessageBox.Show("В буфере обмена нет таблицы сеансов 1С 8.2.");
                return;
            }
            BindSessions();
            _sessHint.Text = "Импорт из консоли 8.2: " + summary;
        }
        private void ImportProcesses82(string text)
        {
            string summary;
            if (_result == null || !Console82Apply.TryApply(_result, text, out summary))
            {
                MessageBox.Show("В буфере нет таблицы рабочих процессов 1С 8.2.");
                return;
            }
            BindSessions();
            _sessHint.Text = "Импорт процессов 8.2: " + summary;
        }
        private void BindSessions()
        {
            if (_result == null) return;
            ClusterInfo c = _result.Cluster;

            Dictionary<string, int> procUse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (SessionEntry s in c.Processes)
            {
                string tp, tpin, tf; int tu, tt;
                Console82Import.ParseLicenseFull(s.LicenseShort, out tp, out tpin, out tu, out tt, out tf);
                if (tpin.Length > 0) { int n; procUse.TryGetValue(tpin, out n); procUse[tpin] = n + 1; }
            }
            Dictionary<string, int> sessUse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (SessionEntry s in c.Sessions)
            {
                string tp, tpin, tf; int tu, tt;
                Console82Import.ParseLicenseFull(s.LicenseShort, out tp, out tpin, out tu, out tt, out tf);
                if (tpin.Length > 0) { int n; sessUse.TryGetValue(tpin, out n); sessUse[tpin] = n + 1; }
            }
            List<ProcessRow> prows = new List<ProcessRow>();
            foreach (SessionEntry s in c.Processes)
            {
                ProcessRow pr = new ProcessRow();
                pr.Computer = s.User;
                pr.Port = (s.Host ?? "").TrimStart(':');
                pr.Usage = s.Usage;
                pr.Enabled = s.Enabled;
                pr.Active = s.Active;
                pr.Pid = s.SessionId;
                pr.Memory = s.Memory;
                string pk, pp, pf; int pu, pt;
                Console82Import.ParseLicenseFull(s.LicenseShort, out pk, out pp, out pu, out pt, out pf);
                pr.License = pk;
                pr.Pin = pp;
                int procCount; procUse.TryGetValue(pp, out procCount);
                pr.Seats = pt >= 0 ? pt.ToString() : "";
                pr.Used = procCount.ToString();
                pr.Free = pt > 0 ? Math.Max(0, pt - procCount).ToString() : "";
                prows.Add(pr);
            }
            _procs.DataSource = null;
            _procs.DataSource = prows;

            List<SessionRow> rows = new List<SessionRow>();
            _sessionItems = c.Sessions;
            foreach (SessionEntry s in c.Sessions)
            {
                SessionRow row = new SessionRow();
                row.Infobase = ShortInfobase(s.Infobase);
                row.SessionId = s.SessionId;
                row.StartedAt = ShortDate(s.StartedAt);
                row.Host = s.Host;
                row.User = string.IsNullOrEmpty(s.User) ? "—" : s.User;
                row.AppId = s.AppId;
                string licKind, licPin, licFile; int licUsed, licTotal;
                Console82Import.ParseLicenseFull(s.LicenseShort, out licKind, out licPin, out licUsed, out licTotal, out licFile);
                row.License = licKind.Length > 0 ? licKind : LicenseText(s);
                row.Pin = licPin;
                int sessCount; sessUse.TryGetValue(licPin, out sessCount);
                row.Seats = licTotal >= 0 ? licTotal.ToString() : "";
                row.Used = sessCount.ToString();
                row.Free = licTotal > 0 ? Math.Max(0, licTotal - sessCount).ToString() : "";
                rows.Add(row);
            }
            _sessions.DataSource = null;
            _sessions.DataSource = rows;

            if (!c.Available)
            {
                _sessHint.Text = "Кластер недоступен: " + c.Error +
                                 (string.IsNullOrEmpty(c.Note) ? "" : " " + c.Note);
            }
            else if (c.Sessions.Count == 0 && c.Processes.Count == 0)
            {
                _sessHint.Text = "Кластер " + c.Endpoint + " отвечает, активных сеансов нет.";
            }
            else
            {
                _sessHint.Text = "Процессов (серверные): " + c.Processes.Count + " · сеансов (клиентские): " + c.Sessions.Count + " · " + c.Endpoint;
            }

            if (rows.Count > 0)
            {
                _sessions.CurrentCell = _sessions.Rows[0].Cells[0];
                _sessions.Rows[0].Selected = true;
            }
            ShowSessionDetails();
        }


        private static string ShortInfobase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 28 ? "…" + s.Substring(s.Length - 27) : s;
        }

        private static string ShortDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            DateTime d;
            string[] formats = new string[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss" };
            if (DateTime.TryParseExact(s.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d))
            {
                return d.ToString("dd.MM HH:mm");
            }
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }
        private static string LicenseText(SessionEntry s)
        {
            if (!string.IsNullOrEmpty(s.LicenseShort)) return s.LicenseShort;
            string t = (s.LicenseType + " " + s.LicenseSeries + " " + s.LicenseFullName).Trim();
            if (t.Length == 0) return "—";
            return t;
        }

        private void ShowSessionDetails()
        {
            int idx = _sessions.SelectedRows.Count > 0 ? _sessions.SelectedRows[0].Index : -1;
            if (idx < 0 || idx >= _sessionItems.Count)
            {
                _sessDetails.Text = _result != null && _result.Cluster.Sessions.Count == 0
                    ? "Сеансов нет.\r\n\r\nСведения о лицензиях появляются, когда пользователи подключены через сервер 1С " +
                      "и у кластера настроен сервер администрирования (ras, порт 1545)."
                    : "";
                return;
            }
            SessionEntry s = _sessionItems[idx];
            List<string> lines = new List<string>();
            lines.Add("Пользователь : " + (string.IsNullOrEmpty(s.User) ? "(не определён)" : s.User));
            lines.Add("Компьютер    : " + s.Host);
            lines.Add("Приложение   : " + s.AppId);
            lines.Add("База         : " + s.Infobase);
            lines.Add("Начало       : " + s.StartedAt);
            if (!string.IsNullOrEmpty(s.LicenseType)) lines.Add("Тип лицензии : " + s.LicenseType);
            if (!string.IsNullOrEmpty(s.LicenseSeries)) lines.Add("Серия        : " + s.LicenseSeries);
            if (!string.IsNullOrEmpty(s.LicenseFullName)) lines.Add("Продукт      : " + s.LicenseFullName);
            if (!string.IsNullOrEmpty(s.IssuedByServer)) lines.Add("Выдана сервером : " + s.IssuedByServer);
            if (!string.IsNullOrEmpty(s.MaxUsersCur)) lines.Add("Мест занято/всего : " + s.MaxUsersCur + " / " + s.MaxUsersAll);
            if (!string.IsNullOrEmpty(s.LicenseFull))
            {
                lines.Add("");
                lines.Add(s.LicenseFull);
            }
            _sessDetails.Text = string.Join("\r\n", lines.ToArray());
        }

        private void RefreshCluster()
        {
            if (_result == null) return;
            string endpoint = _sessServer.Text.Trim();
            string user = _sessUser.Text.Trim();
            string pwd = _sessPwd.Text;
            _sessRefresh.Enabled = false;
            _sessHint.Text = "Опрос кластера " + endpoint + "…";
            ScanResult r = _result;
            Thread t = new Thread(delegate ()
            {
                try { CollectCluster.Run(r, endpoint, user, pwd); }
                catch (Exception ex) { r.Cluster.Error = ex.Message; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _sessRefresh.Enabled = true;
                        BindSessions();
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        public class ProcRow
        {
            public string Name { get; set; }
            public string Pid { get; set; }
            public string Memory { get; set; }
            public string Started { get; set; }
            public string Path { get; set; }
        }

        public class SvcRow
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string State { get; set; }
            public string StartMode { get; set; }
        }

        public class PortRow
        {
            public string PortText { get; set; }
            public string Protocol { get; set; }
            public string Owner { get; set; }
            public string PidText { get; set; }
            public string Role { get; set; }
        }

        public class SessionRow
        {
            public string Infobase { get; set; }
            public string SessionId { get; set; }
            public string StartedAt { get; set; }
            public string Host { get; set; }
            public string User { get; set; }
            public string AppId { get; set; }
            public string License { get; set; }
            public string Pin { get; set; }
            public string Used { get; set; }
            public string Seats { get; set; }
            public string Free { get; set; }
        }

        public class ProcessRow
        {
            public string Computer { get; set; }
            public string Port { get; set; }
            public string Usage { get; set; }
            public string Enabled { get; set; }
            public string Active { get; set; }
            public string Pid { get; set; }
            public string Memory { get; set; }
            public string License { get; set; }
            public string Pin { get; set; }
            public string Used { get; set; }
            public string Seats { get; set; }
            public string Free { get; set; }
        }
    }

    internal class JournalPage : PageBase
    {
        private DataGridView _grid;

        public JournalPage()
        {
            AddHeader("Журнал", "Что и откуда удалось прочитать при последнем сканировании");

            _grid = MakeGrid();
            _grid.Columns.Add(Col("Время", "TimeText", 72));
            _grid.Columns.Add(Col("Уровень", "LevelText", 60));
            _grid.Columns.Add(Col("Раздел", "Source", 90));
            _grid.Columns.Add(Col("Сообщение", "Text", 420));
            AddRow(Card(_grid, new Padding(1)), 470);
        }

        private DataGridViewColumn Col(string header, string prop, int weight)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.DataPropertyName = prop;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            return c;
        }

        public override void Bind(ScanResult r)
        {
            List<Row> rows = new List<Row>();
            foreach (LogEntry e in r.Log)
            {
                Row row = new Row();
                row.TimeText = e.Time.ToString("HH:mm:ss");
                row.LevelText = e.Level == LogLevel.Error ? "ошибка" : (e.Level == LogLevel.Warn ? "внимание" : "инфо");
                row.Source = e.Source;
                row.Text = e.Text;
                rows.Add(row);
            }
            _grid.DataSource = null;
            _grid.DataSource = rows;
        }

        public class Row
        {
            public string TimeText { get; set; }
            public string LevelText { get; set; }
            public string Source { get; set; }
            public string Text { get; set; }
        }
    }
}
