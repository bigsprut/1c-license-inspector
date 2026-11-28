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
        private Label _procsTitle;
        private Label _sessTitle;
        private TextBox _sessDetails;
        private Label _sessHint;
        private TextBox _sessServer;
        private TextBox _sessUser;
        private TextBox _sessPwd;
        private FlatButton _sessRefresh;
        private FlatButton _sess82;
        private ToolTip _tip;
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
            int tabRow = AddRow(Card(_host, new Padding(1)), 478);
            // Область вкладок забирает лишнюю высоту окна: при большом окне видно больше
            // строк таблицы (раньше высота была жёстко 478 px при любом размере окна).
            MarkGrowRow(tabRow, 478);

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
            // Одна строка: поля доступа, главное действие и диагностика; действия для 8.2 —
            // в отдельном меню, чтобы панель не расползалась на две строки.
            top.Height = 46;
            top.MinimumSize = new Size(0, 46);
            top.Padding = new Padding(6, 6, 6, 4);
            top.WrapContents = true;
            // высота по содержимому: если кнопки не помещаются в строку, панель растёт,
            // а не обрезает правые элементы
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.BackColor = Color.White;

            top.Controls.Add(LabelFor("Сервер"));
            _sessServer = new TextBox();
            _sessServer.Width = 130;
            _sessServer.Text = string.IsNullOrEmpty(Program.RasEndpoint) ? "localhost:1545" : Program.RasEndpoint;
            _sessServer.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessServer);

            top.Controls.Add(LabelFor("Логин"));
            _sessUser = new TextBox();
            _sessUser.Width = 100;
            _sessUser.Text = Program.ClusterUser;
            _sessUser.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessUser);

            top.Controls.Add(LabelFor("Пароль"));
            _sessPwd = new TextBox();
            _sessPwd.Width = 100;
            _sessPwd.UseSystemPasswordChar = true;
            _sessPwd.Margin = new Padding(4, 4, 12, 4);
            top.Controls.Add(_sessPwd);

            _sessRefresh = new FlatButton();
            _sessRefresh.Text = "Опросить";
            _sessRefresh.Icon = "refresh";
            _sessRefresh.Kind = ButtonKind.Primary;
            _sessRefresh.Size = new Size(120, 30);
            _sessRefresh.Margin = new Padding(2, 2, 4, 2);
            _sessRefresh.Click += delegate { RefreshCluster(); };

            FlatButton diagBtn = new FlatButton();
            diagBtn.Text = "Диагностика";
            diagBtn.Icon = "file";
            diagBtn.Kind = ButtonKind.Link;
            diagBtn.Size = new Size(124, 30);
            diagBtn.Margin = new Padding(2, 2, 4, 2);
            diagBtn.Click += delegate { ShowDiagnostics(); };
            top.Controls.Add(diagBtn);
            string savedEp, savedUser, savedPwd;
            if (ClusterCredentials.Load(out savedEp, out savedUser, out savedPwd))
            {
                if (!string.IsNullOrEmpty(savedEp)) _sessServer.Text = savedEp;
                _sessUser.Text = savedUser;
                _sessPwd.Text = savedPwd;
                Program.ClusterUser = savedUser;
                Program.ClusterPwd = savedPwd;
                if (!string.IsNullOrEmpty(savedEp)) Program.RasEndpoint = savedEp;
            }

            _sessServer.TextChanged += delegate { Program.RasEndpoint = _sessServer.Text.Trim(); };
            _sessUser.TextChanged += delegate { Program.ClusterUser = _sessUser.Text.Trim(); };
            _sessPwd.TextChanged += delegate { Program.ClusterPwd = _sessPwd.Text; };
            top.Controls.Add(_sessRefresh);

            // Подсказки: без них непонятно, что делают поля и кнопки.
            _tip = new ToolTip();
            _tip.AutoPopDelay = 15000;
            _tip.InitialDelay = 400;
            _tip.ReshowDelay = 100;
            _tip.SetToolTip(_sessServer, "Адрес сервера администрирования (ras). По умолчанию localhost:1545 —\r\n" +
                                         "это порт ras, а не кластера (1541).");
            _tip.SetToolTip(_sessUser, "Логин администратора кластера. Нужен только если в кластере назначены\r\n" +
                                       "администраторы; если их нет — оставьте поле пустым.");
            _tip.SetToolTip(_sessPwd, "Пароль администратора кластера. Хранится зашифрованным (DPAPI)\r\n" +
                                      "и в отчёт не попадает.");
            _tip.SetToolTip(_sessRefresh, "Прочитать сеансы и выданные лицензии с кластера (F5 — полное обновление).");
            _tip.SetToolTip(diagBtn, "Показать сырые ответы rac и подробный отчёт для разбора проблем.");

            // Действия для сервера 8.2 спрятаны в одно меню: четыре отдельные кнопки
            // на панели были неочевидны и отнимали место у главного действия.
            _sess82 = new FlatButton();
            _sess82.Text = "Сервер 8.2";
            _sess82.Icon = "file";
            _sess82.Kind = ButtonKind.Secondary;
            _sess82.Size = new Size(124, 30);
            _sess82.Margin = new Padding(2, 2, 4, 2);
            ContextMenuStrip menu82 = new ContextMenuStrip();
            menu82.Items.Add("Взять сеансы из открытой консоли 8.2", null, delegate { GrabFromConsole(); });
            menu82.Items.Add("Собрать автоматически (MMC 2.0)", null, delegate { AutoCollect82(); });
            menu82.Items.Add("Загрузить таблицу из файла или буфера", null, delegate { ImportConsole82(); });
            menu82.Items.Add(new ToolStripSeparator());
            menu82.Items.Add("Открыть консоль администрирования 8.2", null, delegate { OpenConsole82(); });
            _sess82.Click += delegate { menu82.Show(_sess82, new Point(0, _sess82.Height)); };
            _sess82.Visible = false; // покажем, если на машине действительно есть 8.2
            top.Controls.Add(_sess82);
            _tip.SetToolTip(_sess82, "Действия для сервера 1С 8.2: забрать сеансы из консоли администрирования,\r\n" +
                                     "собрать их автоматически (MMC 2.0) или открыть консоль.");

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
            _procs.Columns.Add(Col("Порт", "Port", 55));
            _procs.Columns.Add(Col("Использование", "Usage", 95));
            _procs.Columns.Add(Col("Включен", "Enabled", 70));
            _procs.Columns.Add(Col("Активен", "Active", 70));
            _procs.Columns.Add(Col("PID", "Pid", 55));
            _procs.Columns.Add(Col("Память, KB", "Memory", 80));
            _procs.Columns.Add(Col("Лицензия", "License", 70));
            _procs.Columns.Add(Col("Рег. номер", "Pin", 95));
            // Места и занятость — одной колонкой: три отдельных столбца не помещались,
            // а «Свободно» у серверной лицензии всегда считалось от занятости процессов.
            DataGridViewColumn procSeats = Col("Мест / занято", "SeatsBusy", 85);
            procSeats.ToolTipText = "Слева — ёмкость лицензии, справа — сколько её держат процессы. " +
                                    "У серверной лицензии «занято» — это работающий сервер, а не сеансы.";
            _procs.Columns.Add(procSeats);
            _procs.Dock = DockStyle.Top;
            _procs.Height = 150;

            _procsTitle = new Label();
            _procsTitle.Dock = DockStyle.Top;
            _procsTitle.Height = 22;
            _procsTitle.Padding = new Padding(10, 4, 10, 0);
            _procsTitle.ForeColor = Theme.Muted;
            _procsTitle.Font = Theme.Small;
            _procsTitle.Text = "Рабочие процессы (8.3 — rac process list, 8.2 — консоль администрирования)";

            _sessions = MakeGrid();
            _sessions.Columns.Add(Col("Инф. база", "Infobase", 100));
            _sessions.Columns.Add(Col("Номер", "SessionId", 60));
            _sessions.Columns.Add(Col("Начало", "StartedAt", 100));
            _sessions.Columns.Add(Col("Компьютер", "Host", 105));
            // «IP» и «Лицензия» убраны с пятой страницы в 1.5.58: адрес клиента и тип лицензии
            // видны в карточке сеанса ниже, а в таблице 13 столбцов не помещались (подписи
            // обрезались до «Пользовате…», «Тонки…», «программ…»).
            _sessions.Columns.Add(Col("Пользователь", "User", 140));
            _sessions.Columns.Add(Col("Приложение", "AppId", 110));
            _sessions.Columns.Add(Col("Состояние", "State", 85));
            _sessions.Columns.Add(Col("Рег. номер", "Pin", 100));
            DataGridViewColumn sessSeats = Col("Мест / занято", "SeatsBusy", 90);
            sessSeats.ToolTipText = "Слева — ёмкость лицензии, справа — сколько сеансов её держат. " +
                                    "У серверной лицензии вместо числа «сервер».";
            _sessions.Columns.Add(sessSeats);
            _sessions.Dock = DockStyle.Fill;
            _sessions.SelectionChanged += delegate { ShowSessionDetails(); };

            _sessTitle = new Label();
            _sessTitle.Dock = DockStyle.Top;
            _sessTitle.Height = 22;
            _sessTitle.Padding = new Padding(10, 6, 10, 0);
            _sessTitle.ForeColor = Theme.Muted;
            _sessTitle.Font = Theme.Small;
            _sessTitle.Text = "Сеансы и выданные лицензии";

            _sessPanel.Controls.Add(_sessions);
            _sessPanel.Controls.Add(_sessTitle);
            _sessPanel.Controls.Add(_procs);
            _sessPanel.Controls.Add(_procsTitle);
            _sessPanel.Controls.Add(_sessDetails);
            _sessPanel.Controls.Add(_sessHint);
            _sessPanel.Controls.Add(top);
            return _sessPanel;
        }


        /// <summary>Есть ли на машине признаки 1С 8.2 (платформа или сеансы, взятые из консоли).</summary>
        private bool Has82Platforms(ScanResult r)
        {
            try
            {
                if (r == null) return false;
                foreach (PlatformInfo p in r.Platforms)
                {
                    if (!string.IsNullOrEmpty(p.Version) && p.Version.StartsWith("8.2", StringComparison.Ordinal)) return true;
                }
                if (r.Cluster != null && r.Cluster.Processes.Count > 0) return true; // импорт из консоли 8.2
            }
            catch { }
            return false;
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
            if (_sess82 != null) _sess82.Visible = Has82Platforms(r);

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
                // «Занято» считается по PIN лицензии: если лицензии у процесса нет (8.3),
                // не показываем ноль — иначе столбец выглядит как «ничего не занято».
                pr.Used = pp.Length > 0 ? procCount.ToString() : "";
                prows.Add(pr);
            }
            _procs.DataSource = null;
            _procs.DataSource = prows;

            // Для 8.3 лицензионные столбцы у процессов пустые (лицензии по сеансам,
            // а не по rphost) — прячем все столбцы без данных, чтобы таблица не выглядела
            // «наполовину пустой».
            HideEmptyColumns(_procs, prows);
            // Нет процессов — не держим пустую таблицу на экране.
            bool showProcs = prows.Count > 0;
            try
            {
                _procs.Visible = showProcs;
                if (_procsTitle != null) _procsTitle.Visible = showProcs;
                if (showProcs) _procs.Height = FitGridHeight(_procs, prows.Count, 96, 240);
            }
            catch { }

            List<SessionRow> rows = new List<SessionRow>();
            _sessionItems = c.Sessions;
            foreach (SessionEntry s in c.Sessions)
            {
                SessionRow row = new SessionRow();
                row.Infobase = ShortInfobase(s.Infobase);
                // В консоли администрирования у сеанса есть числовой номер; UUID в столбце
                // обрезался в «aaaaaaaaa…» и ничего не говорил.
                row.SessionId = string.IsNullOrEmpty(s.SessionNum) ? ShortId(s.SessionId) : s.SessionNum;
                row.StartedAt = ShortDate(s.StartedAt);
                row.Host = s.Host;
                row.User = string.IsNullOrEmpty(s.User) ? "—" : s.User;
                row.AppId = Fmt.AppName(s.AppId);
                string licKind, licPin, licFile; int licUsed, licTotal;
                Console82Import.ParseLicenseFull(s.LicenseShort, out licKind, out licPin, out licUsed, out licTotal, out licFile);
                // 8.3 отдаёт лицензию структурными полями (тип, серия, места) — берём их,
                // иначе строка «Сервер, 123456789 5 5» разбиралась бы 8.2-парсером неверно.
                bool structured = !string.IsNullOrEmpty(s.LicenseSeries) || !string.IsNullOrEmpty(s.MaxUsersAll);
                bool sleeping = string.Equals(s.Hibernate, "yes", StringComparison.OrdinalIgnoreCase);
                row.State = sleeping ? "спит" : "активен";
                row.Pin = structured ? s.LicenseSeries : licPin;
                int seats; int.TryParse(structured ? s.MaxUsersAll : (licTotal >= 0 ? licTotal.ToString() : ""), out seats);
                int busy; int.TryParse(structured ? s.MaxUsersCur : "", out busy);
                if (!structured)
                {
                    int sessCount; sessUse.TryGetValue(licPin, out sessCount);
                    busy = licPin.Length > 0 ? sessCount : 0;
                }
                row.Seats = seats > 0 ? seats.ToString() : "";
                // Лицензия сервера описывает сервер, а не этот сеанс: «занято» для неё
                // не выводим, чтобы «5 из 5» не читалось как «свободных мест нет».
                bool serverLicence = s.IssuedByServer == "да";
                row.Used = !serverLicence && busy > 0 ? busy.ToString() : "";
                rows.Add(row);
            }
            _sessions.DataSource = null;
            _sessions.DataSource = rows;
            HideEmptyColumns(_sessions, rows);

            if (!c.Available)
            {
                string errText = c.Error ?? "";
                bool auth = errText.IndexOf("авторизац", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            errText.IndexOf("authoriz", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            errText.IndexOf("аутентиф", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            errText.IndexOf("Невер", StringComparison.OrdinalIgnoreCase) >= 0;
                _sessHint.Text = auth
                    ? "Кластер требует администратора: укажите логин и пароль кластера в полях выше и нажмите «Обновить». (" + errText + ")"
                    : "Кластер недоступен: " + errText +
                      (string.IsNullOrEmpty(c.Note) ? "" : " " + c.Note);
            }
            else if (c.Sessions.Count == 0 && c.Processes.Count == 0)
            {
                _sessHint.Text = string.IsNullOrEmpty(c.Error)
                    ? "Кластер " + c.Endpoint + " отвечает, активных сеансов нет."
                    : "Кластер отвечает, но сеансы не получены: " + c.Error;
            }
            else
            {
                string hint = "Процессов (серверные): " + c.Processes.Count + " · сеансов (клиентские): " + c.Sessions.Count + " · " + c.Endpoint;
                int sleepingNoLic = 0;
                foreach (SessionEntry se in c.Sessions)
                {
                    if (string.Equals(se.Hibernate, "yes", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrEmpty(se.LicenseSeries) && string.IsNullOrEmpty(se.LicenseType)) sleepingNoLic++;
                }
                if (sleepingNoLic > 0)
                    hint += " · лицензия не выдана у спящих сеансов: " + sleepingNoLic + " (1С освобождает её, пока пользователь не активен)";
                _sessHint.Text = hint;
            }

            if (rows.Count > 0)
            {
                // Столбец с индексом 0 мог быть скрыт как пустой (HideEmptyColumns), а WinForms
                // запрещает делать текущей невидимую ячейку — ищем первый видимый столбец.
                int ci = FirstVisibleColumnIndex(_sessions);
                if (ci < 0) _sessions.ClearSelection();
                else
                {
                    try
                    {
                        _sessions.CurrentCell = _sessions.Rows[0].Cells[ci];
                        _sessions.Rows[0].Selected = true;
                    }
                    catch { _sessions.ClearSelection(); }
                }
            }
            ShowSessionDetails();
        }


        private static string ShortInfobase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 28 ? "…" + s.Substring(s.Length - 27) : s;
        }

        /// <summary>Короткий идентификатор для таблицы: первые 8 символов UUID.</summary>
        private static string ShortId(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 10 ? s.Substring(0, 8) + "…" : s;
        }

        private static string ShortDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            DateTime d;
            string[] formats = new string[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy H:mm:ss" };
            if (DateTime.TryParseExact(s.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d))
            {
                return d.ToString("dd.MM HH:mm");
            }
            return s.Length > 16 ? s.Substring(0, 16) : s;
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
            string clientIp = CollectCluster.NormalizeIp(s.ClientIp);
            lines.Add("Компьютер    : " + (string.IsNullOrEmpty(s.Host)
                ? (clientIp.Length > 0 ? "не определён — 1С не отдаёт имя у спящего сеанса" : "")
                : s.Host));
            if (clientIp.Length > 0)
                lines.Add("Адрес клиента: " + clientIp +
                          (string.Equals(s.HostSource, "dns", StringComparison.OrdinalIgnoreCase)
                              ? " · имя компьютера получено обратным DNS"
                              : ""));
            lines.Add("Приложение   : " + Fmt.AppName(s.AppId));
            lines.Add("База         : " + s.Infobase +
                      (!string.IsNullOrEmpty(s.InfobaseId) && s.InfobaseId != s.Infobase ? " (" + s.InfobaseId + ")" : ""));
            lines.Add("Начало       : " + ShortDate(s.StartedAt));
            // Последняя активность — единственный признак «давно ли пользователь работал»:
            // у спящего сеанса она заметно отличается от времени начала.
            if (!string.IsNullOrEmpty(s.LastActivity) &&
                !string.Equals(s.LastActivity, s.StartedAt, StringComparison.Ordinal))
                lines.Add("Активность   : " + ShortDate(s.LastActivity));
            bool sleeping = string.Equals(s.Hibernate, "yes", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(s.Hibernate))
                lines.Add("Состояние    : " + (sleeping ? "спящий — 1С освободила клиентскую лицензию" : "активный"));
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

        public static string DiagFolder()
        {
            string dir = System.IO.Path.Combine(Paths.ProgramData, "1C-License-Inspector", "diag");
            try { if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir); return dir; }
            catch { return System.IO.Path.GetTempPath(); }
        }

        private void ShowDiagnostics()
        {
            string text = "";
            if (_result != null && _result.Cluster != null)
            {
                if (!string.IsNullOrEmpty(_result.Cluster.Raw)) text += "=== rac cluster list ===\r\n" + _result.Cluster.Raw + "\r\n";
                if (!string.IsNullOrEmpty(_result.Cluster.SessionRaw)) text += "=== rac session list ===\r\n" + _result.Cluster.SessionRaw + "\r\n";
                if (!string.IsNullOrEmpty(_result.Cluster.Error)) text += "=== ошибка ===\r\n" + _result.Cluster.Error + "\r\n";
            }
            if (text.Length == 0) text = "Данных rac нет. Нажмите «Обновить», чтобы программа опросила кластер.";

            Form f = new Form();
            f.Text = "Диагностика кластера 1С";
            f.StartPosition = FormStartPosition.CenterParent;
            f.Size = new Size(980, 640);
            f.MinimizeBox = false;
            f.MaximizeBox = true;
            f.BackColor = Theme.Paper;
            f.KeyPreview = true;
            f.KeyDown += delegate (object s2, KeyEventArgs e2)
            {
                if (e2.KeyCode == Keys.Escape) f.Close();
            };

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.Dock = DockStyle.Fill;
            box.Font = Theme.MonoFont(9f);
            box.Text = text;
            EnableCopy(box);

            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 46;
            bar.BackColor = Theme.Paper;

            FlatButton folder = new FlatButton();
            folder.Text = "Открыть папку";
            folder.Kind = ButtonKind.Secondary;
            folder.Size = new Size(150, 30);
            folder.Location = new Point(10, 8);
            folder.Click += delegate { try { System.Diagnostics.Process.Start("explorer.exe", DiagFolder()); } catch { } };

            FlatButton copy = new FlatButton();
            copy.Text = "Копировать";
            copy.Kind = ButtonKind.Link;
            copy.Size = new Size(130, 30);
            copy.Location = new Point(170, 8);
            copy.Click += delegate { try { Clipboard.SetText(box.Text); } catch { } };

            FlatButton close = new FlatButton();
            close.Text = "Закрыть";
            close.Kind = ButtonKind.Secondary;
            close.Size = new Size(110, 30);
            close.Location = new Point(310, 8);
            close.Click += delegate { f.Close(); };

            // Полная диагностика: отдельно выясняет, какие администраторы есть в кластере,
            // каким способом они входят и видит ли rac инфобазы и сеансы.
            FlatButton probe = new FlatButton();
            probe.Text = "Полная диагностика rac";
            probe.Kind = ButtonKind.Secondary;
            probe.Size = new Size(210, 30);
            probe.Location = new Point(450, 8);
            probe.Click += delegate
            {
                if (_result == null)
                {
                    box.Text = "Данных ещё нет: сначала нажмите «Обновить», чтобы программа опросила кластер.";
                    return;
                }
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    string path;
                    string full = RacProbe.BuildAndSave(_result, _sessServer.Text.Trim(), _sessUser.Text.Trim(), _sessPwd.Text, out path);
                    box.Text = "Файл диагностики: " + path + Environment.NewLine + Environment.NewLine + full;
                    box.SelectionStart = 0;
                    box.ScrollToCaret();
                }
                catch (Exception ex)
                {
                    box.Text = "Сбой диагностики rac: " + ex.GetType().Name + ": " + ex.Message;
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            };

            bar.Controls.Add(folder);
            bar.Controls.Add(copy);
            bar.Controls.Add(close);
            bar.Controls.Add(probe);
            f.Controls.Add(box);
            f.Controls.Add(bar);
            f.ShowDialog(this.FindForm());
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
                try { CollectCluster.Run(r, endpoint, user, pwd); ClusterCredentials.Save(endpoint, user, pwd); }
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
            public string State { get; set; }
            public string Pin { get; set; }
            public string Used { get; set; }
            public string Seats { get; set; }
            /// <summary>Столбец «Мест / занято»: ёмкость лицензии и число сеансов на ней.</summary>
            public string SeatsBusy
            {
                get
                {
                    string s = Seats ?? "";
                    string b = Used ?? "";
                    if (s.Length == 0 && b.Length == 0) return "";
                    if (s.Length == 0) return b;
                    if (b.Length == 0) return s;
                    return s + " / " + b;
                }
            }
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
            /// <summary>Столбец «Мест / занято»: сколько мест в лицензии и сколько занято процессами.</summary>
            public string SeatsBusy
            {
                get
                {
                    string s = Seats ?? "";
                    string b = Used ?? "";
                    if (s.Length == 0 && b.Length == 0) return "";
                    if (s.Length == 0) return b;
                    if (b.Length == 0) return s;
                    return s + " / " + b;
                }
            }
        }
    }

    internal class JournalPage : PageBase
    {
        private DataGridView _grid;
        private int _gridRow;
        private bool _onlyImportant;

        public JournalPage()
        {
            AddHeader("Журнал", "Что и откуда удалось прочитать при последнем сканировании");

            // Без фильтра важные записи тонули среди «инфо» (24 строки про платформы подряд).
            SegmentedTabs filter = new SegmentedTabs();
            filter.Items.AddRange(new string[] { "Все записи", "Только важные" });
            filter.SelectedIndex = 0;
            filter.SelectedIndexChanged += delegate
            {
                _onlyImportant = filter.SelectedIndex == 1;
                if (Snapshot != null) Bind(Snapshot);
            };
            AddRow(filter, 34);
            AddSpacer(6);

            _grid = MakeGrid();
            _grid.Columns.Add(Col("Время", "TimeText", 72));
            _grid.Columns.Add(Col("Уровень", "LevelText", 66));
            _grid.Columns.Add(Col("Раздел", "Source", 90));
            _grid.Columns.Add(Col("Сообщение", "Text", 420));
            _gridRow = AddRow(Card(_grid, new Padding(1)), 470);
            // Журнал тоже растёт вместе с окном — иначе при большом окне таблица оставалась
            // маленькой с прокруткой внутри, а вокруг пусто (1.5.60).
            MarkGrowRow(_gridRow, 470);
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
                if (_onlyImportant && e.Level == LogLevel.Info) continue;
                Row row = new Row();
                row.TimeText = e.Time.ToString("HH:mm:ss");
                row.LevelText = e.Level == LogLevel.Error ? "ошибка" : (e.Level == LogLevel.Warn ? "внимание" : "инфо");
                row.Source = e.Source;
                row.Text = e.Text;
                rows.Add(row);
            }
            // Сначала ошибки и предупреждения — даже в режиме «Все записи» важное сверху.
            rows.Sort(delegate (Row a, Row b)
            {
                int la = a.LevelText == "ошибка" ? 0 : (a.LevelText == "внимание" ? 1 : 2);
                int lb = b.LevelText == "ошибка" ? 0 : (b.LevelText == "внимание" ? 1 : 2);
                if (la != lb) return la.CompareTo(lb);
                return string.Compare(a.TimeText, b.TimeText, StringComparison.Ordinal);
            });
            _grid.DataSource = null;
            _grid.DataSource = rows;
            // Журнал занимает всё доступное место, но не меньше 8 строк.
            int h = FitGridHeight(_grid, rows.Count, 220, 2000);
            SetRowHeight(_gridRow, Root.Height - 120 > h ? Root.Height - 120 : h);
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
