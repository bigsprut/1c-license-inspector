using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Lite
{
    /// <summary>
    /// Вкладка «Лицензии» краткого приложения: только таблица лицензий.
    /// Строки собирает общий UsageData — то же, что видно на первой странице полного приложения.
    /// </summary>
    internal class LiteLicencesPage : Ui.PageBase
    {
        private DataGridView _grid;
        private Label _hint;
        private int _gridRow;
        private List<Ui.LicenseUsagePage.LicenceRow> _items = new List<Ui.LicenseUsagePage.LicenceRow>();

        public LiteLicencesPage()
        {
            AddHeader("Лицензии", "Сколько мест в лицензиях, срок действия и откуда они взяты");

            _grid = MakeGrid();
            // PageBase.Col сам добавляет столбец в таблицу и возвращает его.
            Col(_grid, "Рег. номер", "Pin", 130, "");
            Col(_grid, "Тип", "Kind", 110, "");
            Col(_grid, "Мест", "Seats", 70, "");
            Col(_grid, "Занято", "Used", 80, "");
            Col(_grid, "Свободно", "Free", 90, "");
            DataGridViewColumn exp = Col(_grid, "Срок действия", "Expires", 110, "");
            exp.ToolTipText = "Из файла лицензии (*.lic). «Бессрочно» — в файле написано «не ограничен». " +
                              "У лицензий 8.2 срок снаружи не читается: файлы зашифрованы.";
            DataGridViewColumn iss = Col(_grid, "Выдана сервером", "IssuedByServer", 95, "");
            iss.ToolTipText = "Признак из rac (issued-by-server): «да» — лицензию выдал сервер лицензий, " +
                              "«нет» — локальная. «—» — источник признак не сообщает (так у 8.2).";
            Col(_grid, "Источник", "Source", 150, "");
            Col(_grid, "Файл", "File", 260, "");
            _gridRow = AddRow(Card(_grid, new Padding(1)), 280);
            MarkGrowRow(_gridRow, 240);

            _hint = new Label();
            _hint.Dock = DockStyle.Bottom;
            _hint.Height = 28;
            _hint.Padding = new Padding(10, 6, 10, 4);
            _hint.ForeColor = Ui.Theme.Muted;
            _hint.Font = Ui.Theme.Small;
            _hint.Text = "";
            AddRow(_hint, 28);
        }

        /// <summary>Сколько строк в таблице — для строки состояния.</summary>
        public int Count { get { return _items.Count; } }

        public override void Bind(ScanResult r)        {
            Ui.UsageData d = Ui.UsageData.Build(r);
            _items = d.Licences;
            _grid.DataSource = null;
            _grid.DataSource = d.Licences;
            HideEmptyColumns(_grid, d.Licences);
            SetRowHeight(_gridRow, 60 + FitGridHeight(_grid, d.Licences.Count, 220, 4000));
            _hint.Text = string.Join(" · ", d.Hint.ToArray());
        }

        public override string CopyText()
        {
            StringBuilder b = new StringBuilder();
            ScanResult r = Snapshot;
            b.AppendLine("Лицензии 1С " + Program.Version +
                         (r == null ? "" : " · " + r.System.ComputerName + " · " + Fmt.Date(r.Finished)));
            b.AppendLine("Рег. номер\tТип\tМест\tЗанято\tСвободно\tСрок действия\tВыдана сервером\tИсточник\tФайл");
            foreach (Ui.LicenseUsagePage.LicenceRow l in _items)
            {
                b.AppendLine(string.Join("\t", new string[] { l.Pin, l.Kind, l.Seats, l.Used, l.Free,
                    l.Expires, l.IssuedByServer, l.Source, l.File }));
            }
            return b.ToString();
        }
    }
}
