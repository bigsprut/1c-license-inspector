using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal class SoftwarePage : PageBase
    {
        private DataGridView _grid;
        private KeyValueList _info;
        private TextBox _hex;
        private TextBox _ring;
        private int _infoRow;
        private int _gridRow;
        private Panel _fileSection;
        private int _ringRow;
        private List<LicenseFileInfo> _items = new List<LicenseFileInfo>();
        private Panel _hexCard;

        public SoftwarePage()
        {
            AddHeader("Программные лицензии", "Файлы лицензий 1С (*.lic): где лежат, когда активированы, в каком состоянии");

            _info = new KeyValueList();
            _infoRow = AddRow(WithTitle("Каталоги лицензий и привязка", Card(_info, new Padding(0))), 128);
            AddSpacer(6);

            _grid = MakeGrid();
            _grid.Columns.Add(Col("Файл", "FileName", 140));
            _grid.Columns.Add(Col("Рег. номер", "Pin", 120));
            _grid.Columns.Add(Col("Продукт", "Product", 205));
            _grid.Columns.Add(Col("Тип", "LicenseType", 90));
            _grid.Columns.Add(Col("Польз.", "Users", 60));
            DataGridViewColumn usedCol = Col("Использ.", "Used", 80);
            usedCol.ToolTipText = "Сколько сеансов держат эту лицензию прямо сейчас. У спящих сеансов лицензия освобождена и здесь не считается.";
            _grid.Columns.Add(usedCol);
            _grid.Columns.Add(Col("Срок", "Expires", 110));
            _grid.Columns.Add(Col("Каталог", "Directory", 190));
            _grid.SelectionChanged += delegate { ShowDetails(); };
            _fileSection = WithTitle("Файлы лицензий", Card(_grid, new Padding(1)));
            _gridRow = AddRow(_fileSection, 176);
            AddSpacer(6);

            _hex = new TextBox();
            _hex.Multiline = true;
            _hex.ReadOnly = true;
            _hex.ScrollBars = ScrollBars.Vertical;
            _hex.BorderStyle = BorderStyle.None;
            _hex.BackColor = Color.FromArgb(250, 251, 252);
            _hex.ForeColor = Theme.InkSoft;
            _hex.Font = Theme.MonoFont(8.5f);
            _hex.Dock = DockStyle.Fill;
            EnableCopy(_hex);
            _hexCard = WithTitle("Содержимое файла лицензии — можно выделить и скопировать (Ctrl+C)", Card(_hex, new Padding(10)));
            AddRow(_hexCard, 140);
            AddSpacer(6);

            _ring = new TextBox();
            _ring.Multiline = true;
            _ring.ReadOnly = true;
            _ring.ScrollBars = ScrollBars.Vertical;
            _ring.BorderStyle = BorderStyle.None;
            _ring.BackColor = Color.FromArgb(250, 251, 252);
            _ring.ForeColor = Theme.InkSoft;
            _ring.Font = Theme.MonoFont(8.5f);
            _ring.Dock = DockStyle.Fill;
            EnableCopy(_ring);
            _ringRow = AddRow(WithTitle("Вывод утилиты лицензирования ring", Card(_ring, new Padding(10))), 126);
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
            List<KeyValuePair<string, string>> info = new List<KeyValuePair<string, string>>();
            int seatSum = 0;
            bool seatUnlimited = false;
            foreach (SentinelFeature f in r.Sentinel.Features)
            {
                if (f.IsUnlimited) seatUnlimited = true;
                else seatSum += f.LimitNum > 0 ? f.LimitNum : 0;
            }
            int clientTotal = 0;
            int serverTotal = 0;
            int otherTotal = 0;
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                string kind = ((lf.LicenseType ?? "") + " " + (lf.Product ?? "")).ToLowerInvariant();
                if (kind.IndexOf("сервер") >= 0) serverTotal++;
                else if (kind.IndexOf("клиент") >= 0) clientTotal += Math.Max(1, lf.UsersNum);
                else otherTotal++;
            }
            int usedClient = -1;
            if (r.Cluster != null && r.Cluster.Available)
            {
                usedClient = 0;
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    string sl = ((se.LicenseShort ?? "") + " " + (se.LicenseFullName ?? "") + " " + (se.LicenseType ?? "")).ToLowerInvariant();
                    if (sl.IndexOf("клиент") >= 0 || se.AppId == "1CV8C" || se.AppId == "Designer" || se.AppId == "WebClient")
                    {
                        if (sl.IndexOf("сервер") < 0) usedClient++;
                    }
                }
            }
            int usedTotal = 0; bool anyUsage = false;
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                if (lf.UsedBySessions >= 0) { usedTotal += lf.UsedBySessions; anyUsage = true; }
            }
            if (anyUsage) usedClient = usedTotal;
            string clientUsage = usedClient < 0
                ? "нет данных (кластер недоступен)"
                : (clientTotal <= 0
                    ? usedClient + " сеанс(ов), клиентских мест не найдено"
                    : usedClient + " из " + clientTotal + ", свободно " + Math.Max(0, clientTotal - usedClient));
            bool serverRunning = false;
            foreach (ProcessInfo pi in r.Processes)
            {
                if (string.Equals(pi.Name, "rphost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pi.Name, "rmngr", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pi.Name, "ragent", StringComparison.OrdinalIgnoreCase)) { serverRunning = true; break; }
            }
            string seats = seatUnlimited ? "∞" : (seatSum > 0 ? seatSum.ToString() : "нет данных");
            int usersSum = 0;
            foreach (LicenseFileInfo lf in r.LicenseFiles) usersSum += lf.UsersNum;
            info.Add(new KeyValuePair<string, string>("Лицензий всего",
                r.LicenseFiles.Count + " · клиентских мест: " + clientTotal + " · серверных: " + serverTotal + (otherTotal > 0 ? " · прочих: " + otherTotal : "") + " · пользователей: " + usersSum));
            info.Add(new KeyValuePair<string, string>("Занято / свободно", clientUsage));
            info.Add(new KeyValuePair<string, string>("Серверная лицензия", (serverTotal > 0 ? "есть (" + serverTotal + ")" : "не найдена") + (serverRunning ? " · сервер 1С запущен" : " · сервер 1С не запущен")));
            info.Add(new KeyValuePair<string, string>("Места в аппаратных ключах", seats));
            info.Add(new KeyValuePair<string, string>("Каталоги поиска",
                r.LicenseStorages.Count == 0 ? "стандартные каталоги отсутствуют" : Fmt.ShortList(r.LicenseStorages, 3)));
            info.Add(new KeyValuePair<string, string>("Отпечаток ПК",
                string.IsNullOrEmpty(r.HostId) ? "не найден (host_id отсутствует)" : Shorten(r.HostId, 140)));
            info.Add(new KeyValuePair<string, string>("Java для ring",
                string.IsNullOrEmpty(r.JavaPath) ? "не найдена — без Java утилита ring не запустится" : r.JavaPath));
            info.Add(new KeyValuePair<string, string>("Утилита ring",
                string.IsNullOrEmpty(r.LicenseRing)
                    ? "не найдена (не обязательна: PIN, редакция и места читаются из *.lic)"
                    : r.LicenseRing));


            _info.Set(info);
            int w = Root.ClientSize.Width > 300 ? Root.ClientSize.Width - 12 : 900;
            SetRowHeight(_infoRow, Math.Max(110, _info.Measure(w) + 34));

            if (!string.IsNullOrEmpty(r.LicenseRingOutput))
            {
                _ring.Text = r.LicenseRingOutput;
            }
            else if (!string.IsNullOrEmpty(r.LicenseRing))
            {
                _ring.Text = "Утилита найдена: " + r.LicenseRing + Environment.NewLine + "Вывод не получен (проверьте, установлена ли Java).";
            }
            else
            {
                _ring.Text = "Утилита лицензирования 1С (ring) не найдена." + Environment.NewLine +
                    (string.IsNullOrEmpty(r.JavaPath) ? "Java не найдена." : "Java есть: " + r.JavaPath) + Environment.NewLine +
                    "Если она установлена, программа покажет список лицензий с PIN, редакцией и числом мест автоматически." + Environment.NewLine +
                    "PIN, редакция и число мест читаются прямо из файлов *.lic (нижняя часть файла — открытый текст)." + Environment.NewLine +
                    "Утилита входит в комплект «1C:Enterprise License Tools» и требует Java.";
            }

            // Какие лицензии захвачены сеансами: серия лицензии из `rac` (например «123456789»)
            // сопоставляется с рег. номером файла («123456789O0»).
            List<string> usedSeries = new List<string>();
            if (r.Cluster != null)
            {
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    string digits = Digits(se.LicenseSeries);
                    if (digits.Length >= 6) usedSeries.Add(digits);
                }
            }

            List<FileRow> rows = new List<FileRow>();
            foreach (LicenseFileInfo f in r.LicenseFiles)
            {
                FileRow row = new FileRow();
                row.FileName = f.FileName;
                row.Pin = string.IsNullOrEmpty(f.Pin) ? "—" : f.Pin;
                row.Product = string.IsNullOrEmpty(f.Product) ? "—" : f.Product;
                row.LicenseType = string.IsNullOrEmpty(f.LicenseType) ? "—" : f.LicenseType;
                row.Users = string.IsNullOrEmpty(f.Users) ? "—" : f.Users;
                row.Used = f.UsedBySessions < 0 ? "—" : f.UsedBySessions.ToString();
                row.Expires = string.IsNullOrEmpty(f.Expires) ? "—" : f.Expires;
                row.Activated = string.IsNullOrEmpty(f.Activated) ? "—" : f.Activated;
                row.SizeText = Fmt.Bytes(f.Size);
                row.Format = f.Format;
                row.ModifiedText = Fmt.Date(f.Modified);
                row.Directory = f.Directory;
                row.Info = f;
                rows.Add(row);
            }
            _items = r.LicenseFiles;
            _grid.DataSource = null;
            _grid.DataSource = rows;
            HideEmptyColumns(_grid, rows);
            if (_fileSection != null) _fileSection.Visible = rows.Count > 0;
            SetRowHeight(_gridRow, rows.Count > 0 ? 30 + FitGridHeight(_grid, rows.Count, 96, 300) : 0);
            if (rows.Count > 0) _grid.Rows[0].Selected = true;
            ShowDetails();

            // Панель вывода ring подстраиваем под число строк — пустое поле на пол-экрана не нужно.
            int ringLines = 3;
            if (!string.IsNullOrEmpty(_ring.Text)) ringLines = _ring.Text.Replace("\r", "").Split('\n').Length;
            SetRowHeight(_ringRow, 30 + Math.Max(64, Math.Min(220, ringLines * 14 + 26)));
        }

        /// <summary>Только цифры: «123456789O0» → «1234567890».</summary>
        private static string Digits(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            StringBuilder b = new StringBuilder(v.Length);
            foreach (char c in v)
            {
                if (c >= '0' && c <= '9') b.Append(c);
            }
            return b.ToString();
        }

        private static bool PinMatches(string sessionDigits, string fileDigits)
        {
            if (sessionDigits.Length < 6 || fileDigits.Length < 6) return false;
            if (sessionDigits == fileDigits) return true;
            if (fileDigits.StartsWith(sessionDigits, StringComparison.Ordinal)) return true;
            if (sessionDigits.StartsWith(fileDigits, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Сколько сеансов держат лицензию, или «—», если кластер не опрошен.</summary>
        private static string UsageText(string filePin, List<string> usedSeries, bool clusterKnown)
        {
            string pin = Digits(filePin);
            if (pin.Length < 6) return clusterKnown ? "" : "—";
            int used = 0;
            foreach (string s in usedSeries)
            {
                if (PinMatches(s, pin)) used++;
            }
            if (used > 0) return used.ToString();
            return clusterKnown ? "0" : "—";
        }
        /// <summary>Сменить заголовок секции, не пересобирая её (нужно для пустых состояний).</summary>
        private static void SetSectionTitle(Panel section, string text)
        {
            try
            {
                if (section == null) return;
                foreach (Control c in section.Controls)
                {
                    SectionTitle st = c as SectionTitle;
                    if (st != null) { st.Text2 = text; st.Invalidate(); return; }
                }
            }
            catch { }
        }

        private static string Shorten(string s, int max)        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private void ShowDetails()
        {
            LicenseFileInfo f = null;
            if (_grid.CurrentRow != null && _grid.CurrentRow.Index >= 0 && _grid.CurrentRow.Index < _items.Count)
                f = _items[_grid.CurrentRow.Index];

            if (f == null)
            {
                SetSectionTitle(_hexCard, "Файлы программных лицензий");
                _hex.Text = "Файлы программных лицензий не найдены.\r\n\r\n" +
                            "Возможные причины:\r\n" +
                            "  • лицензии аппаратные (ключ HASP) — тогда смотрите раздел «Аппаратные ключи»;\r\n" +
                            "  • лицензии активированы на другом компьютере или выдаются по сети;\r\n" +
                            "  • используется базовая версия без отдельных лицензий.";
                return;
            }

            List<KeyValuePair<string, string>> kv = new List<KeyValuePair<string, string>>();
            SetSectionTitle(_hexCard, "Содержимое файла лицензии — можно выделить и скопировать (Ctrl+C)");
            kv.Add(new KeyValuePair<string, string>("Файл", f.FullPath));
            kv.Add(new KeyValuePair<string, string>("Активирована", string.IsNullOrEmpty(f.Activated) ? "дата в имени файла не распознана" : f.Activated));
            kv.Add(new KeyValuePair<string, string>("Формат", f.Format + " · " + f.Note));
            kv.Add(new KeyValuePair<string, string>("SHA-1", f.Sha1));

            // Верхнюю карточку со сводкой больше не затираем сведениями о файле: она про
            // каталоги и привязку, а сведения о выбранном файле показываем над содержимым.
            StringBuilder head = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in kv) head.AppendLine(pair.Key + ": " + pair.Value);
            head.AppendLine();

            bool binary = !string.IsNullOrEmpty(f.Format) &&
                          f.Format.IndexOf("текст", StringComparison.OrdinalIgnoreCase) < 0;
            if (binary)
            {
                // Двоичный файл нельзя показывать как текст — получается мусор вроде «яя<».
                _hex.Text = head.ToString() +
                            "Содержимое двоичное (" + Fmt.Bytes(f.Size) + "), как текст не отображается." + Environment.NewLine +
                            "Первые байты: " + (string.IsNullOrEmpty(f.Preview) ? "—" : f.Preview) + Environment.NewLine + Environment.NewLine +
                            "PIN, редакцию и число мест по такому файлу читает только сама платформа 1С" +
                            " (или утилита ring, если она установлена).";
            }
            else
            {
                _hex.Text = head + CollectLicenses.ReadText(f.FullPath);
            }
        }

        public class FileRow
        {
            public string FileName { get; set; }
            public string Pin { get; set; }
            public string Product { get; set; }
            public string LicenseType { get; set; }
            public string Users { get; set; }
            public string Used { get; set; }
            public string Expires { get; set; }
            public string Activated { get; set; }
            public string SizeText { get; set; }
            public string Format { get; set; }
            public string ModifiedText { get; set; }
            public string Directory { get; set; }
            public LicenseFileInfo Info { get; set; }
        }
    }
}
