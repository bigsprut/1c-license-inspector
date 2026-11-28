using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Lite
{
    /// <summary>
    /// Вкладка «Использование лицензий»: кто занял места, и карточка выбранного сеанса.
    /// Рабочие процессы сервера в кратком приложении не показываются — по решению Сергея.
    /// </summary>
    internal class LiteSessionsPage : Ui.PageBase
    {
        private DataGridView _grid;
        private TextBox _details;
        private Label _hint;
        private int _gridRow;
        private List<Ui.LicenseUsagePage.SessionRow> _items = new List<Ui.LicenseUsagePage.SessionRow>();

        public LiteSessionsPage()
        {
            AddHeader("Использование лицензий", "Кто занял места: пользователь, компьютер, база, приложение");

            _grid = MakeGrid();
            // PageBase.Col сам добавляет столбец в таблицу и возвращает его.
            Col(_grid, "Рег. номер", "Pin", 100, "");
            Col(_grid, "Пользователь", "User", 150, "");
            Col(_grid, "Компьютер", "Host", 105, "");
            DataGridViewColumn ip = Col(_grid, "IP", "Ip", 90, "");
            ip.ToolTipText = "Адрес клиента из rac (client-ip). У спящего сеанса 1С не отдаёт имя " +
                             "компьютера вообще, поэтому адрес — единственное, что известно о машине.";
            Col(_grid, "Инф. база", "Infobase", 105, "");
            Col(_grid, "№", "Number", 55, "");
            Col(_grid, "Начало", "Started", 95, "");
            DataGridViewColumn act = Col(_grid, "Активность", "Activity", 105, "");
            act.ToolTipText = "Последняя активность сеанса по данным rac (last-active-at): у работающего — " +
                              "совсем свежая, у спящего — когда он последний раз работал.";
            Col(_grid, "Приложение", "App", 110, "");
            Col(_grid, "Состояние", "State", 85, "");
            DataGridViewColumn seats = Col(_grid, "Мест / занято", "SeatsBusy", 95, "");
            seats.ToolTipText = "Слева — ёмкость лицензии, справа — сколько сеансов её держат. " +
                                "У серверной лицензии вместо числа «сервер».";
            _grid.SelectionChanged += delegate { ShowDetails(); };
            _gridRow = AddRow(Card(_grid, new Padding(1)), 320);
            MarkGrowRow(_gridRow, 260);

            // Карточка выбранного сеанса: адрес клиента, лицензия, места, активность.
            Panel card = new Panel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(1);
            card.BackColor = Ui.Theme.Line;
            _details = new TextBox();
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Vertical;
            _details.BorderStyle = BorderStyle.None;
            _details.BackColor = Color.White;
            _details.ForeColor = Ui.Theme.Ink;
            _details.Font = Ui.Theme.Body;
            _details.Dock = DockStyle.Fill;
            _details.ShortcutsEnabled = true;
            _details.Text = "Выберите сеанс в таблице — ниже появятся подробности.";
            EnableCopy(_details);
            card.Controls.Add(_details);
            AddRow(card, 132);

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

        public override void Bind(ScanResult r)
        {
            Ui.UsageData d = Ui.UsageData.Build(r);
            _items = d.Sessions;
            _grid.DataSource = null;
            _grid.DataSource = d.Sessions;
            HideEmptyColumns(_grid, d.Sessions);
            SetRowHeight(_gridRow, 60 + FitGridHeight(_grid, d.Sessions.Count, 240, 4000));
            _hint.Text = string.Join(" · ", d.Hint.ToArray());
            ShowDetails();
        }

        private void ShowDetails()
        {
            Ui.LicenseUsagePage.SessionRow row = null;
            try
            {
                if (_grid.SelectedRows.Count > 0)
                    row = _grid.SelectedRows[0].DataBoundItem as Ui.LicenseUsagePage.SessionRow;
            }
            catch { }
            if (row == null || row.Entry == null)
            {
                _details.Text = _items.Count == 0
                    ? "Сеансов нет.\r\n\r\nСведения о лицензиях появляются, когда пользователи подключены через сервер 1С " +
                      "и у кластера настроен сервер администрирования (ras, порт 1545)."
                    : "Выберите сеанс в таблице — ниже появятся подробности.";
                return;
            }
            _details.Text = Ui.SessionCard.Build(row.Entry);
        }

        public override string CopyText()
        {
            StringBuilder b = new StringBuilder();
            ScanResult r = Snapshot;
            b.AppendLine("Использование лицензий 1С " + Program.Version +
                         (r == null ? "" : " · " + r.System.ComputerName + " · " + Fmt.Date(r.Finished)));
            b.AppendLine("Рег. номер\tПользователь\tКомпьютер\tIP\tИнф. база\t№\tНачало\tАктивность\tПриложение\tСостояние\tМест / занято");
            foreach (Ui.LicenseUsagePage.SessionRow s in _items)
            {
                b.AppendLine(string.Join("\t", new string[] { s.Pin, s.User, s.Host, s.Ip, s.Infobase,
                    s.Number, s.Started, s.Activity, s.App, s.State, s.SeatsBusy }));
            }
            return b.ToString();
        }
    }
}
