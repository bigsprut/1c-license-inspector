using System;
using System.Collections.Generic;
using System.Drawing;
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
        private List<LicenseFileInfo> _items = new List<LicenseFileInfo>();
        private Panel _hexCard;

        public SoftwarePage()
        {
            AddHeader("Программные лицензии", "Файлы лицензий 1С (*.lic): где лежат, когда активированы, в каком состоянии");

            _info = new KeyValueList();
            _infoRow = AddRow(WithTitle("Каталоги лицензий и привязка", Card(_info, new Padding(0))), 128);
            AddSpacer(6);

            _grid = MakeGrid();
            _grid.Columns.Add(Col("Файл", "FileName", 120));
            _grid.Columns.Add(Col("Активирована", "Activated", 100));
            _grid.Columns.Add(Col("Размер", "SizeText", 70));
            _grid.Columns.Add(Col("Формат", "Format", 100));
            _grid.Columns.Add(Col("Изменён", "ModifiedText", 100));
            _grid.Columns.Add(Col("Каталог", "Directory", 220));
            _grid.SelectionChanged += delegate { ShowDetails(); };
            AddRow(WithTitle("Файлы лицензий", Card(_grid, new Padding(1))), 176);
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
            _hexCard = WithTitle("Первые байты файла (диагностика формата)", Card(_hex, new Padding(10)));
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
            AddRow(WithTitle("Вывод утилиты лицензирования ring", Card(_ring, new Padding(10))), 126);
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
            info.Add(new KeyValuePair<string, string>("Каталоги поиска",
                r.LicenseStorages.Count == 0 ? "стандартные каталоги отсутствуют" : string.Join("; ", r.LicenseStorages.ToArray())));
            info.Add(new KeyValuePair<string, string>("Отпечаток ПК",
                string.IsNullOrEmpty(r.HostId) ? "не найден (host_id отсутствует)" : Shorten(r.HostId, 140)));
            info.Add(new KeyValuePair<string, string>("Утилита ring",
                string.IsNullOrEmpty(r.LicenseRing)
                    ? "не найдена — описание лицензий (PIN, редакция, число мест) читает только утилита лицензирования 1С"
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
                    "Если она установлена, программа покажет список лицензий с PIN, редакцией и числом мест автоматически." + Environment.NewLine +
                    "Утилита входит в комплект «1C:Enterprise License Tools» и требует Java.";
            }

            List<FileRow> rows = new List<FileRow>();
            foreach (LicenseFileInfo f in r.LicenseFiles)
            {
                FileRow row = new FileRow();
                row.FileName = f.FileName;
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
            if (rows.Count > 0) _grid.Rows[0].Selected = true;
            ShowDetails();
        }

        private static string Shorten(string s, int max)
        {
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
                _hex.Text = "Файлы программных лицензий не найдены.\r\n\r\n" +
                            "Возможные причины:\r\n" +
                            "  • лицензии аппаратные (ключ HASP) — тогда смотрите раздел «Аппаратные ключи»;\r\n" +
                            "  • лицензии активированы на другом компьютере или выдаются по сети;\r\n" +
                            "  • используется базовая версия без отдельных лицензий.";
                return;
            }

            List<KeyValuePair<string, string>> kv = new List<KeyValuePair<string, string>>();
            kv.Add(new KeyValuePair<string, string>("Файл", f.FullPath));
            kv.Add(new KeyValuePair<string, string>("Активирована", string.IsNullOrEmpty(f.Activated) ? "дата в имени файла не распознана" : f.Activated));
            kv.Add(new KeyValuePair<string, string>("Формат", f.Format + " · " + f.Note));
            kv.Add(new KeyValuePair<string, string>("SHA-1", f.Sha1));
            _info.Set(kv);
            _hex.Text = f.Preview;
        }

        public class FileRow
        {
            public string FileName { get; set; }
            public string Activated { get; set; }
            public string SizeText { get; set; }
            public string Format { get; set; }
            public string ModifiedText { get; set; }
            public string Directory { get; set; }
            public LicenseFileInfo Info { get; set; }
        }
    }
}
