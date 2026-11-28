using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    /// <summary>
    /// Главная страница: две таблицы — какие лицензии есть (рег. номер, места, занято)
    /// и кто именно их занял (сеансы со всеми сведениями). Открывается первой при запуске.
    /// Источники: файлы *.lic (8.3) и MMC-консоль администрирования (8.2).
    /// </summary>
    internal class LicenseUsagePage : PageBase
    {
        private DataGridView _licences;
        private DataGridView _sessions;
        private int _licencesRow;
        private int _sessionsRow;
        private Label _hint;
        private List<LicenceRow> _licenceItems = new List<LicenceRow>();
        private List<SessionRow> _sessionItems = new List<SessionRow>();
        private FlatButton _copy;

        public LicenseUsagePage()
        {
            AddHeader("Лицензии и сеансы", "Сколько мест в лицензиях и кто их занял — данные файлов лицензий и консоли администрирования");

            // Копирование страницы: кнопка рядом с заголовком (и Ctrl+C из шапки работает так же).
            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.WrapContents = false;
            tools.Padding = new Padding(0);
            tools.BackColor = Color.Transparent;
            _copy = new FlatButton();
            _copy.Text = "Копировать таблицы";
            _copy.Icon = "copy";
            _copy.Kind = ButtonKind.Secondary;
            _copy.Size = new Size(190, 30);
            _copy.Margin = new Padding(0, 1, 8, 0);
            _copy.Click += delegate { CopyToClipboard(); };
            tools.Controls.Add(_copy);
            Label copyTip = new Label();
            copyTip.AutoSize = true;
            copyTip.Text = "или Ctrl+C — текст двух таблиц с разделителем табуляции";
            copyTip.ForeColor = Theme.Muted;
            copyTip.Font = Theme.Small;
            copyTip.Margin = new Padding(2, 8, 0, 0);
            tools.Controls.Add(copyTip);
            AddRow(tools, 34);
            AddSpacer(4);
            _licences = MakeGrid();
            _licences.Columns.Add(Col("Рег. номер", "Pin", 130));
            _licences.Columns.Add(Col("Тип", "Kind", 110));
            _licences.Columns.Add(Col("Мест", "Seats", 70));
            _licences.Columns.Add(Col("Занято", "Used", 80));
            _licences.Columns.Add(Col("Свободно", "Free", 90));
            DataGridViewColumn expiresCol = Col("Срок действия", "Expires", 110);
            expiresCol.ToolTipText = "Из файла лицензии (*.lic). «Бессрочно» — в файле написано «не ограничен». " +
                                     "У лицензий 8.2 срок снаружи не читается: файлы зашифрованы.";
            _licences.Columns.Add(expiresCol);
            DataGridViewColumn issuedCol = Col("Выдана сервером", "IssuedByServer", 95);
            issuedCol.ToolTipText = "Признак из rac (issued-by-server) по сеансам, которые держат эту лицензию: " +
                                    "«да» — лицензию выдал сервер лицензий, «нет» — локальная. «—» — данных нет.";
            _licences.Columns.Add(issuedCol);
            _licences.Columns.Add(Col("Источник", "Source", 150));
            _licences.Columns.Add(Col("Файл", "File", 260));
            _licencesRow = AddRow(WithTitle("Лицензии: регистрационный номер, мест и занято", Card(_licences, new Padding(1))), 150);
            AddSpacer(6);

            _sessions = MakeGrid();
            _sessions.Columns.Add(Col("Рег. номер", "Pin", 105));
            _sessions.Columns.Add(Col("Пользователь", "User", 140));
            _sessions.Columns.Add(Col("Компьютер", "Host", 100));
            DataGridViewColumn ipCol = Col("IP", "Ip", 95);
            ipCol.ToolTipText = "Адрес клиента из rac (client-ip). У спящего сеанса 1С не отдаёт имя " +
                                "компьютера вообще, поэтому адрес — единственное, что известно о машине.";
            _sessions.Columns.Add(ipCol);
            _sessions.Columns.Add(Col("Инф. база", "Infobase", 105));
            // «№» убран с первой страницы в 1.6.1: 11 столбцов не помещались, а номер сеанса
            // виден в карточке (на первой странице карточки нет — там номер есть в копировании).
            _sessions.Columns.Add(Col("Начало", "Started", 105));
            // Активность — время последнего обращения из rac (last-active-at): по нему видно,
            // кто действительно работает, а кто просто держит сеанс открытым (или спит).
            DataGridViewColumn actCol = Col("Активность", "Activity", 100);
            actCol.ToolTipText = "Последняя активность сеанса по данным rac (last-active-at): " +
                                 "«сейчас» у работающего, а у спящего — когда он последний раз работал.";
            _sessions.Columns.Add(actCol);
            _sessions.Columns.Add(Col("Приложение", "App", 125));
            _sessions.Columns.Add(Col("Состояние", "State", 85));
            // Тип лицензии не дублируем (он в таблице лицензий выше), а «активность» перенесли
            // в карточку сеанса на странице «Использование»: 11 столбцов при 5–10 сеансах
            // не помещались, и подписи обрезались до «Пользовате…».
            DataGridViewColumn seatsCol = Col("Мест / занято", "SeatsBusy", 85);
            seatsCol.ToolTipText = "Слева — ёмкость лицензии, справа — сколько сеансов держат её сейчас. " +
                                   "У серверной лицензии вместо числа «сервер»: её занимает работающий сервер, а не сеансы.";
            _sessions.Columns.Add(seatsCol);
            _sessionsRow = AddRow(WithTitle("Использование лицензий сеансами", Card(_sessions, new Padding(1))), 420);
            // Строку сеансов делаем «растущей»: при большом окне таблица занимает всё свободное
            // место, иначе она оставалась прежней высоты, а снизу было пусто (исправлено 1.5.60).
            MarkGrowRow(_sessionsRow, 220);

            _hint = new Label();
            _hint.Dock = DockStyle.Bottom;
            _hint.Height = 26;
            _hint.Padding = new Padding(10, 5, 10, 3);
            _hint.ForeColor = Theme.Muted;
            _hint.Font = Theme.Small;
            _hint.Text = "";
            AddRow(_hint, 26);
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

        private Panel WithTitle(string title, Control body)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.BackColor = Color.Transparent;
            SectionTitle st = new SectionTitle();
            st.Text2 = title;
            st.Dock = DockStyle.Fill;
            st.Margin = new Padding(0);
            t.Controls.Add(st, 0, 0);
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            t.Controls.Add(body, 0, 1);
            return t;
        }

        public override void Bind(ScanResult r)
        {
            // Строки собирает UsageData — тем же кодом пользуется краткое приложение
            // (две вкладки), поэтому таблицы в обоих окнах не расходятся.
            UsageData d = UsageData.Build(r);
            _licenceItems = d.Licences;
            _sessionItems = d.Sessions;

            _licences.DataSource = null;
            _licences.DataSource = d.Licences;
            HideEmptyColumns(_licences, d.Licences);
            SetRowHeight(_licencesRow, 30 + FitGridHeight(_licences, d.Licences.Count, 110, 300));

            _sessions.DataSource = null;
            _sessions.DataSource = d.Sessions;
            HideEmptyColumns(_sessions, d.Sessions);
            SetRowHeight(_sessionsRow, 30 + FitGridHeight(_sessions, d.Sessions.Count, 200, 2000));

            _hint.Text = string.Join(" · ", d.Hint.ToArray());
        }
        /// <summary>Текст страницы для буфера обмена: две таблицы, разделитель — табуляция.</summary>
        public override string CopyText()
        {
            StringBuilder b = new StringBuilder();
            ScanResult r = Snapshot;
            b.AppendLine("Инспектор лицензий 1С " + Program.Version +
                         (r == null ? "" : " · " + r.System.ComputerName + " · " + Fmt.Date(r.Finished)));
            b.AppendLine();
            b.AppendLine("ЛИЦЕНЗИИ (" + _licenceItems.Count + ")");
            b.AppendLine("Рег. номер\tТип\tМест\tЗанято\tСвободно\tСрок действия\tВыдана сервером\tИсточник\tФайл");
            foreach (LicenceRow l in _licenceItems)
            {
                b.AppendLine(Join(l.Pin, l.Kind, l.Seats, l.Used, l.Free, l.Expires, l.IssuedByServer, l.Source, l.File));
            }
            b.AppendLine();
            b.AppendLine("ИСПОЛЬЗОВАНИЕ ЛИЦЕНЗИЙ СЕАНСАМИ (" + _sessionItems.Count + ")");
            b.AppendLine("Рег. номер\tПользователь\tКомпьютер\tIP\tИнф. база\t№\tНачало\tАктивность\tПриложение\tСостояние\tМест / занято");
            foreach (SessionRow s in _sessionItems)
            {
                b.AppendLine(Join(s.Pin, s.User, s.Host, s.Ip, s.Infobase, s.Number, s.Started, s.Activity,
                                  s.App, s.State, s.SeatsBusy));
            }
            return b.ToString();
        }

        private static string Join(params string[] cells)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) b.Append('\t');
                b.Append((cells[i] ?? "").Replace("\t", " "));
            }
            return b.ToString();
        }

        private void CopyToClipboard()
        {
            try
            {
                Clipboard.SetText(CopyText());
                _hint.Text = "Скопировано в буфер: лицензий " + _licenceItems.Count + ", сеансов " + _sessionItems.Count +
                             " · вставляйте в письмо, чат или Excel (табуляция сохранит столбцы)";
            }
            catch (Exception ex)
            {
                _hint.Text = "Не удалось скопировать: " + ex.Message;
            }
        }

        public class LicenceRow
        {
            public LicenceRow()
            {
                Pin = ""; Kind = ""; Seats = ""; Used = ""; Free = ""; Expires = ""; IssuedByServer = "";
                Source = ""; File = "";
            }

            public string Pin { get; set; }
            public string Kind { get; set; }
            public string Seats { get; set; }
            public string Used { get; set; }
            public string Free { get; set; }
            /// <summary>Срок действия из файла лицензии («бессрочно» или дата).</summary>
            public string Expires { get; set; }
            /// <summary>Признак rac issued-by-server: «да»/«нет»/«—» — данных нет.</summary>
            public string IssuedByServer { get; set; }
            public string Source { get; set; }
            public string File { get; set; }
        }

        public class SessionRow
        {
            public SessionRow()
            {
                Pin = ""; User = ""; Host = ""; Ip = ""; Infobase = ""; Number = ""; Started = "";
                App = ""; State = ""; Seats = ""; Busy = ""; Activity = "";
            }

            public string Pin { get; set; }
            public string User { get; set; }
            public string Host { get; set; }
            /// <summary>Адрес клиента: у спящего сеанса имени компьютера 1С не отдаёт.</summary>
            public string Ip { get; set; }
            public string Infobase { get; set; }
            public string Number { get; set; }
            public string Started { get; set; }
            /// <summary>Время последней активности из rac (last-active-at).</summary>
            public string Activity { get; set; }
            public string App { get; set; }
            public string State { get; set; }
            public string Seats { get; set; }
            public string Busy { get; set; }
            /// <summary>Исходный сеанс — для карточки под таблицей.</summary>
            public SessionEntry Entry;
            /// <summary>Столбец «Мест / занято»: ёмкость лицензии и число сеансов на ней.</summary>
            public string SeatsBusy
            {
                get
                {
                    string s = Seats ?? "";
                    string b = Busy ?? "";
                    if (s.Length == 0 && b.Length == 0) return "";
                    if (s.Length == 0) return b;
                    if (b.Length == 0) return s;
                    return s + " / " + b;
                }
            }
        }
    }
}
