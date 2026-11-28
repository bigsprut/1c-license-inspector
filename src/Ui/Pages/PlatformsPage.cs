using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal class PlatformsPage : PageBase
    {
        private DataGridView _grid;
        private KeyValueList _details;
        private int _gridRow;
        private List<PlatformInfo> _items = new List<PlatformInfo>();
        private int _detailsRow;

        public PlatformsPage()
        {
            AddHeader("Платформы 1С", "Все установленные версии 1С:Предприятие 8 и 7.7 с составом поставки");

            _grid = MakeGrid();
            Column("Версия", "Version", 90);
            Column("Семейство", "Family", 120);
            Column("Поставка", "Delivery", 220);
            Column("Разрядность", "Bitness", 80);
            Column("Установлена", "InstallDate", 90);
            Column("Путь", "Path", 240);
            _grid.SelectionChanged += delegate { ShowDetails(); };
            _grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                PlatformInfo p = Selected();
                if (p != null) OpenFolder(p.Path);
            };

            _gridRow = AddRow(Card(_grid, new Padding(1)), 306);
            AddSpacer(10);

            _details = new KeyValueList();
            _detailsRow = AddRow(WithTitle("Выбранная платформа", Card(_details, new Padding(0))), 190);
        }

        private DataGridViewColumn Column(string header, string prop, int weight)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.DataPropertyName = prop;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            _grid.Columns.Add(c);
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
            _items = r.Platforms;
            List<Row> rows = new List<Row>();
            foreach (PlatformInfo p in r.Platforms)
            {
                Row row = new Row();
                row.Version = p.Version;
                row.Family = p.Family;
                row.Delivery = p.Delivery;
                row.Bitness = p.Bitness;
                row.InstallDate = p.InstallDate;
                row.Path = p.Path;
                rows.Add(row);
            }
            _grid.DataSource = null;
            _grid.DataSource = rows;
            HideEmptyColumns(_grid, rows);
            SetRowHeight(_gridRow, FitGridHeight(_grid, rows.Count, 120, 420));
            // именно Rows.Count: список может быть непустым, а грид ещё без строк (без окна)
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
            ShowDetails();
        }

        public class Row
        {
            public string Version { get; set; }
            public string Family { get; set; }
            public string Delivery { get; set; }
            public string Bitness { get; set; }
            public string InstallDate { get; set; }
            public string Path { get; set; }
        }

        private PlatformInfo Selected()
        {
            if (_grid.CurrentRow == null) return null;
            int i = _grid.CurrentRow.Index;
            if (i < 0 || i >= _items.Count) return null;
            return _items[i];
        }

        private void ShowDetails()
        {
            PlatformInfo p = Selected();
            List<KeyValuePair<string, string>> kv = new List<KeyValuePair<string, string>>();
            if (p == null)
            {
                kv.Add(new KeyValuePair<string, string>("Платформа", "Выберите строку в таблице"));
                _details.Set(kv);
                ResizeDetails();
                return;
            }
            kv.Add(new KeyValuePair<string, string>("Платформа", p.Family + " " + p.Version + " (" + p.Bitness + ")"));
            kv.Add(new KeyValuePair<string, string>("Состав поставки", p.Delivery));
            kv.Add(new KeyValuePair<string, string>("Компоненты", p.Components));
            kv.Add(new KeyValuePair<string, string>("Каталог", p.Path));
            kv.Add(new KeyValuePair<string, string>("Каталог bin", p.BinPath));
            kv.Add(new KeyValuePair<string, string>("Источник сведений", p.Source + (string.IsNullOrEmpty(p.Comment) ? "" : " · " + p.Comment)));
            if (!string.IsNullOrEmpty(p.InstallDate)) kv.Add(new KeyValuePair<string, string>("Установлена", p.InstallDate));
            _details.Set(kv);
            ResizeDetails();
        }

        private void ResizeDetails()
        {
            int w = Root.ClientSize.Width > 300 ? Root.ClientSize.Width - 12 : 900;
            SetRowHeight(_detailsRow, Math.Max(120, _details.Measure(w) + 34));
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path)) return;
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }
    }
}
