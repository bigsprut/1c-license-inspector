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
            _sessions.Columns.Add(Col("Рег. номер", "Pin", 95));
            _sessions.Columns.Add(Col("Пользователь", "User", 145));
            _sessions.Columns.Add(Col("Компьютер", "Host", 100));
            DataGridViewColumn ipCol = Col("IP", "Ip", 95);
            ipCol.ToolTipText = "Адрес клиента из rac (client-ip). У спящего сеанса 1С не отдаёт имя " +
                                "компьютера вообще, поэтому адрес — единственное, что известно о машине.";
            _sessions.Columns.Add(ipCol);
            _sessions.Columns.Add(Col("Инф. база", "Infobase", 100));
            _sessions.Columns.Add(Col("№", "Number", 60));
            _sessions.Columns.Add(Col("Начало", "Started", 100));
            _sessions.Columns.Add(Col("Приложение", "App", 120));
            _sessions.Columns.Add(Col("Состояние", "State", 90));
            // Тип лицензии не дублируем (он в таблице лицензий выше), а «активность» перенесли
            // в карточку сеанса на странице «Использование»: 11 столбцов при 5–10 сеансах
            // не помещались, и подписи обрезались до «Пользовате…».
            DataGridViewColumn seatsCol = Col("Мест / занято", "SeatsBusy", 90);
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
            List<LicenceRow> licences = new List<LicenceRow>();
            List<SessionRow> sessions = new List<SessionRow>();
            HashSet<string> seenPins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int seatsTotal = 0, busyTotal = 0;
            Dictionary<string, bool> issued = IssuedMap(r);

            // 1. Лицензии из консоли администрирования 8.2 — там есть места и занятость.
            foreach (License82Row row in CollectLicenses.Build82(r))
            {
                LicenceRow lr = new LicenceRow();
                lr.Pin = row.Pin;
                lr.Kind = row.IsServer ? "серверная" : "клиентская";
                lr.Seats = row.Seats;
                lr.Used = row.Used;
                lr.Free = row.Free;
                lr.Source = "консоль 8.2";
                lr.File = row.File;
                lr.IssuedByServer = IssuedText(issued, row.Pin);
                licences.Add(lr);
                if (row.Pin.Length > 0) seenPins.Add(row.Pin);
                int seats, busy;
                if (int.TryParse(row.Seats, out seats)) seatsTotal += row.IsServer ? 0 : seats;
                if (!row.IsServer && int.TryParse(row.Used, out busy)) busyTotal += busy;
            }

            // 2. Лицензии из файлов *.lic (8.3): PIN и места читаются из текста файла.
            foreach (LicenseFileInfo f in r.LicenseFiles)
            {
                string pinDigits = CollectLicenses.Digits(f.Pin);
                if (pinDigits.Length >= 6 && seenPins.Contains(f.Pin)) continue;
                LicenceRow lr = new LicenceRow();
                lr.Pin = string.IsNullOrEmpty(f.Pin) ? "—" : f.Pin;
                lr.Kind = f.IsServerLicense ? "серверная" : (string.IsNullOrEmpty(f.LicenseType) ? "—" : f.LicenseType);
                lr.Seats = f.UsersNum > 0 ? f.UsersNum.ToString() : "";
                if (f.IsServerLicense)
                {
                    lr.Used = f.UsedByServer == 1 ? "сервер" : "—";
                }
                else
                {
                    lr.Used = f.UsedBySessions >= 0 ? f.UsedBySessions.ToString() : "—";
                    if (f.UsersNum > 0 && f.UsedBySessions >= 0)
                        lr.Free = Math.Max(0, f.UsersNum - f.UsedBySessions).ToString();
                }
                lr.Source = "файл .lic";
                lr.File = f.FileName;
                lr.Expires = ExpiresText(f.Expires);
                lr.IssuedByServer = IssuedText(issued, f.Pin);
                licences.Add(lr);
                if (!f.IsServerLicense) seatsTotal += Math.Max(0, f.UsersNum);
                if (!f.IsServerLicense && f.UsedBySessions > 0) busyTotal += f.UsedBySessions;
            }

            // Занято в нижней таблице — это ЧИСЛО СЕАНСОВ на эту лицензию, а не поля rac
            // max-users-cur/all: они показывают ёмкость лицензии и путали (20/20 у одного сеанса).
            Dictionary<string, int> sessionsByPin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, LicenceRow> licenceByPin = new Dictionary<string, LicenceRow>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> serverPin = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (LicenceRow lr in licences)
            {
                string d = CollectLicenses.Digits(lr.Pin);
                if (d.Length >= 6 && !licenceByPin.ContainsKey(d)) licenceByPin[d] = lr;
                if (lr.Kind != null && lr.Kind.IndexOf("сервер", StringComparison.OrdinalIgnoreCase) >= 0) serverPin[d] = true;
            }
            // 3. Сеансы — с рег. номером лицензии, местами и занятостью.
            if (r.Cluster != null)
            {
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    SessionRow row = new SessionRow();
                    row.User = string.IsNullOrEmpty(se.User) ? "—" : se.User;
                    row.Host = se.Host;
                    row.Ip = CollectCluster.NormalizeIp(se.ClientIp);
                    row.Infobase = Shorten(se.Infobase, 34);
                    row.Number = string.IsNullOrEmpty(se.SessionNum) ? se.SessionId : se.SessionNum;
                    if (row.Number.Length > 10) row.Number = row.Number.Substring(0, 8) + "…";
                    row.Started = ShortDate(se.StartedAt);
                    row.App = Fmt.AppName(se.AppId);
                    bool sleeping = string.Equals(se.Hibernate, "yes", StringComparison.OrdinalIgnoreCase);
                    row.State = string.IsNullOrEmpty(se.Hibernate) ? "" : (sleeping ? "спит" : "активен");

                    if (!string.IsNullOrEmpty(se.LicenseSeries) || !string.IsNullOrEmpty(se.MaxUsersAll))
                    {
                        row.Pin = se.LicenseSeries;
                        row.Seats = se.MaxUsersAll;
                        row.Busy = se.MaxUsersCur;
                    }
                    else if (!string.IsNullOrEmpty(se.LicenseShort))
                    {
                        string kind, pin, file; int used, total;
                        Console82Import.ParseLicenseFull(se.LicenseShort, out kind, out pin, out used, out total, out file);
                        row.Pin = pin;
                        row.Seats = total > 0 ? total.ToString() : "";
                        row.Busy = used >= 0 ? used.ToString() : "";
                        if (row.Number.Length == 0) row.Number = se.SessionId;
                    }
                    string pd = CollectLicenses.Digits(row.Pin);
                    if (pd.Length >= 6)
                    {
                        int n;
                        sessionsByPin.TryGetValue(pd, out n);
                        sessionsByPin[pd] = n + 1;
                    }
                    sessions.Add(row);
                }
            }

            // «Занято» в нижней таблице = сколько сеансов держат этот PIN; «Мест» — ёмкость лицензии.
            foreach (SessionRow row in sessions)
            {
                string d = CollectLicenses.Digits(row.Pin);
                if (d.Length < 6) continue;
                LicenceRow lic;
                if (licenceByPin.TryGetValue(d, out lic) && lic.Seats.Length > 0) row.Seats = lic.Seats;
                bool isServer;
                if (serverPin.TryGetValue(d, out isServer) && isServer) { row.Busy = "сервер"; continue; }
                int count;
                if (sessionsByPin.TryGetValue(d, out count)) row.Busy = count.ToString();
            }
            sessions.Sort(delegate (SessionRow a, SessionRow b)
            {
                return string.Compare(a.User ?? "", b.User ?? "", StringComparison.OrdinalIgnoreCase);
            });

            // Столбцы «Срок действия» и «Выдана сервером» показываем всегда, когда есть лицензии:
            // у 8.2 срок из зашифрованного файла не читается, а «выдана сервером» rac сообщает
            // только по сеансам. Неизвестное значение — прочерк плюс пояснение в подсказке,
            // а не пустая клетка (раньше из-за пустого поля писалось «нет» — это было неверно).
            int expUnknown = 0, issUnknown = 0;
            foreach (LicenceRow lr in licences)
            {
                if (string.IsNullOrEmpty(lr.Expires)) { lr.Expires = "—"; expUnknown++; }
                if (string.IsNullOrEmpty(lr.IssuedByServer)) { lr.IssuedByServer = "—"; issUnknown++; }
            }

            _licenceItems = licences;
            _sessionItems = sessions;
            _licences.DataSource = null;
            _licences.DataSource = licences;
            HideEmptyColumns(_licences, licences);
            SetRowHeight(_licencesRow, 30 + FitGridHeight(_licences, licences.Count, 110, 300));

            _sessions.DataSource = null;
            _sessions.DataSource = sessions;
            HideEmptyColumns(_sessions, sessions);
            SetRowHeight(_sessionsRow, 30 + FitGridHeight(_sessions, sessions.Count, 200, 2000));

            List<string> hint = new List<string>();
            hint.Add("Лицензий: " + licences.Count);
            if (seatsTotal > 0) hint.Add("мест: занято " + busyTotal + " из " + seatsTotal);
            hint.Add("сеансов: " + sessions.Count);
            // Спящий сеанс 1С отдаёт без имени компьютера — предупреждаем, что в столбце «IP»
            // не украшение, а всё, что о машине известно.
            int ipOnly = 0;
            foreach (SessionRow row in sessions)
            {
                if (string.IsNullOrEmpty(row.Host) && !string.IsNullOrEmpty(row.Ip)) ipOnly++;
            }
            if (ipOnly > 0) hint.Add("только IP (имени 1С не отдаёт): " + ipOnly);
            // Прочерки в новых столбцах объясняем словами: «—» значит «источник не сообщает»,
            // а не «лицензия не выдана сервером».
            if (licences.Count > 0 && issUnknown == licences.Count)
                hint.Add("«выдана сервером»: источник этот признак не сообщает");
            else if (issUnknown > 0)
                hint.Add("«—» в «выдана сервером» — признак не сообщён");
            if (licences.Count > 0 && expUnknown == licences.Count)
                hint.Add("срок действия не читается: у 8.2 файлы лицензий зашифрованы");
            if (r.Cluster != null && !string.IsNullOrEmpty(r.Cluster.Error)) hint.Add("кластер: " + r.Cluster.Error);
            _hint.Text = string.Join(" · ", hint.ToArray());
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
            b.AppendLine("Рег. номер\tПользователь\tКомпьютер\tIP\tИнф. база\t№\tНачало\tПриложение\tСостояние\tМест / занято");
            foreach (SessionRow s in _sessionItems)
            {
                b.AppendLine(Join(s.Pin, s.User, s.Host, s.Ip, s.Infobase, s.Number, s.Started,
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

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > max ? "…" + s.Substring(s.Length - (max - 1)) : s;
        }

        private static string ShortDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            DateTime d;
            string[] formats = new string[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy H:mm:ss" };
            if (DateTime.TryParseExact(s.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d.ToString("dd.MM HH:mm");
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }

        /// <summary>
        /// Срок действия из файла лицензии. Платформа пишет «не ограничен» — для человека это
        /// бессрочная лицензия, так и показываем; дату оставляем как есть.
        /// </summary>
        private static string ExpiresText(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return "";
            if (s.ToLowerInvariant().IndexOf("не огранич", StringComparison.Ordinal) >= 0) return "бессрочно";
            return s;
        }

        /// <summary>
        /// «Выдана сервером» rac сообщает не в файле лицензии, а в сеансах (issued-by-server).
        /// Собираем по сеансам, которые держат лицензию; ключ — цифры рег. номера.
        /// Если хоть один сеанс говорит «да», лицензия считается выданной сервером.
        /// Пустое поле — это «неизвестно», а не «нет»: у импорта из консоли 8.2 признака нет
        /// вовсе, и раньше из-за этого в таблице писалось «нет» (исправлено в 1.5.59).
        /// </summary>
        private static Dictionary<string, bool> IssuedMap(ScanResult r)
        {
            Dictionary<string, bool> map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (r == null || r.Cluster == null) return map;
            foreach (SessionEntry se in r.Cluster.Sessions)
            {
                string pin = CollectLicenses.Digits(se.LicenseSeries);
                if (pin.Length < 6) pin = CollectLicenses.Digits(Console82Import.LicensePin(se.LicenseShort));
                if (pin.Length < 6) continue;
                if (string.Equals(se.IssuedByServer, "да", StringComparison.OrdinalIgnoreCase)) map[pin] = true;
                else if (string.Equals(se.IssuedByServer, "нет", StringComparison.OrdinalIgnoreCase) &&
                         !map.ContainsKey(pin)) map[pin] = false;
            }
            return map;
        }

        private static string IssuedText(Dictionary<string, bool> map, string pin)
        {
            if (map == null) return "";
            string d = CollectLicenses.Digits(pin);
            if (d.Length < 6) return "";
            bool exact;
            if (map.TryGetValue(d, out exact)) return exact ? "да" : "нет";
            // Рег. номер в файле — это серия плюс контрольный знак («123456789O0» против
            // серии «123456789» в сеансе), поэтому сравниваем как в MatchUsage.
            foreach (KeyValuePair<string, bool> kv in map)
            {
                if (CollectLicenses.PinMatches(kv.Key, d)) return kv.Value ? "да" : "нет";
            }
            return "";
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
                App = ""; State = ""; Seats = ""; Busy = "";
            }

            public string Pin { get; set; }
            public string User { get; set; }
            public string Host { get; set; }
            /// <summary>Адрес клиента: у спящего сеанса имени компьютера 1С не отдаёт.</summary>
            public string Ip { get; set; }
            public string Infobase { get; set; }
            public string Number { get; set; }
            public string Started { get; set; }
            public string App { get; set; }
            public string State { get; set; }
            public string Seats { get; set; }
            public string Busy { get; set; }
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
