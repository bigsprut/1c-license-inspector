using System;
using System.Collections.Generic;
using System.Drawing;
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

        public UsagePage()
        {
            AddHeader("Использование", "Процессы 1С, службы защиты и сетевые порты, которые важны для лицензирования");

            _tabs = new SegmentedTabs();
            _tabs.Items.Add("Процессы");
            _tabs.Items.Add("Службы");
            _tabs.Items.Add("Порты");
            _tabs.Height = 42;
            _tabs.SelectedIndex = 0;
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
            AddRow(Card(_host, new Padding(1)), 430);

            _tabs.SelectedIndexChanged += delegate { ShowTab(); };
            ShowTab();
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
            if (_tabs.SelectedIndex == 0)
            {
                _processes.Visible = true; _processes.BringToFront();
                _services.Visible = false; _ports.Visible = false;
            }
            else if (_tabs.SelectedIndex == 1)
            {
                _services.Visible = true; _services.BringToFront();
                _processes.Visible = false; _ports.Visible = false;
            }
            else
            {
                _ports.Visible = true; _ports.BringToFront();
                _processes.Visible = false; _services.Visible = false;
            }
        }

        public override void Bind(ScanResult r)
        {
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
