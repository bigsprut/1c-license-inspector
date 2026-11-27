using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal class HardwarePage : PageBase
    {
        private DataGridView _keys;
        private DataGridView _features;
        private KeyValueList _hasp4;
        private KeyValueList _lm;
        private List<SentinelKey> _keyItems = new List<SentinelKey>();
        private List<SentinelFeature> _featureItems = new List<SentinelFeature>();
        private ScanResult _result;
        private FlatButton _btnMonitor;
        private FlatButton _btnImport;
        private FlatButton _btnService;
        private FlatButton _btnAcc;
        private int _lmRow;
        private int _haspRow;

        public HardwarePage()
        {
            AddHeader("Аппаратные ключи", "Ключи HASP HL / Sentinel HL и устаревший менеджер HASP4 Net для 1С 7.7 и 8.2");

            _lm = new KeyValueList();
            _lmRow = AddRow(WithTitle("Менеджер лицензий Sentinel LDK", Card(_lm, new Padding(0))), 124);
            AddSpacer(6);

            _keys = MakeGrid();
            _keys.Columns.Add(Col("ID ключа", "Id", 110));
            _keys.Columns.Add(Col("Название", "Name", 130));
            _keys.Columns.Add(Col("Тип", "TypeName", 120));
            _keys.Columns.Add(Col("Расположение", "Location", 90));
            _keys.Columns.Add(Col("ПО", "Firmware", 60));
            _keys.Columns.Add(Col("Сеансов", "Sessions", 60));
            _keys.SelectionChanged += delegate { FilterFeatures(); };
            AddRow(WithTitle("Ключи", Card(_keys, new Padding(1))), 150);
            AddSpacer(6);

            _features = MakeGrid();
            _features.Columns.Add(Col("Ключ", "KeyId", 90));
            _features.Columns.Add(Col("ID", "FeatureId", 45));
            _features.Columns.Add(Col("Компонент", "Name", 190));
            _features.Columns.Add(Col("Тип", "Type", 110));
            _features.Columns.Add(Col("Зан.", "Logins", 55));
            _features.Columns.Add(Col("Всего", "Limit", 60));
            _features.Columns.Add(Col("Своб.", "FreeText", 70));
            _features.Columns.Add(Col("Ограничение", "ExpireText", 105));
            AddRow(WithTitle("Компоненты лицензий и свободные места", Card(_features, new Padding(1))), 210);
            AddSpacer(6);

            _hasp4 = new KeyValueList();
            _haspRow = AddRow(WithTitle("Устаревший менеджер HASP4 Net", Card(_hasp4, new Padding(0))), 196);
            AddSpacer(6);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.BackColor = Theme.Canvas;
            tools.WrapContents = false;
            tools.Padding = new Padding(0);

            _btnMonitor = new FlatButton();
            _btnMonitor.Text = "Открыть HASP Monitor";
            _btnMonitor.Icon = "key";
            _btnMonitor.Kind = ButtonKind.Secondary;
            _btnMonitor.Size = new Size(200, 34);
            _btnMonitor.Click += delegate { OpenMonitor(); };
            tools.Controls.Add(_btnMonitor);

            _btnImport = new FlatButton();
            _btnImport.Text = "Загрузить отчёт HASPInfo…";
            _btnImport.Icon = "file";
            _btnImport.Kind = ButtonKind.Secondary;
            _btnImport.Size = new Size(236, 34);
            _btnImport.Margin = new Padding(8, 3, 0, 0);
            _btnImport.Click += delegate { ImportHaspInfo(); };
            tools.Controls.Add(_btnImport);

            _btnService = new FlatButton();
            _btnService.Text = "Запустить службу Sentinel";
            _btnService.Icon = "refresh";
            _btnService.Kind = ButtonKind.Secondary;
            _btnService.Size = new Size(220, 34);
            _btnService.Margin = new Padding(8, 3, 0, 0);
            _btnService.Click += delegate { StartSentinelService(); };
            tools.Controls.Add(_btnService);

            _btnAcc = new FlatButton();
            _btnAcc.Text = "Открыть ACC";
            _btnAcc.Icon = "key";
            _btnAcc.Kind = ButtonKind.Secondary;
            _btnAcc.Size = new Size(140, 34);
            _btnAcc.Margin = new Padding(8, 3, 0, 0);
            _btnAcc.Click += delegate { OpenAcc(); };
            tools.Controls.Add(_btnAcc);

            AddRow(tools, 42);
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

        private class KeyRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string TypeName { get; set; }
            public string Location { get; set; }
            public string Firmware { get; set; }
            public string Sessions { get; set; }
        }
        public override void Bind(ScanResult r)
        {
            _result = r;
            List<KeyValuePair<string, string>> lm = new List<KeyValuePair<string, string>>();
            if (r.Sentinel.Available)
            {
                lm.Add(new KeyValuePair<string, string>("Состояние", "Работает · " + r.Sentinel.Status));
                lm.Add(new KeyValuePair<string, string>("Версия менеджера", string.IsNullOrEmpty(r.Sentinel.Version) ? "—" : r.Sentinel.Version));
                string srv = (r.Sentinel.ServerName ?? "").Trim();
                string sov = (r.Sentinel.ServerOs ?? "").Trim();
                if (sov.Length > 0) srv = srv.Length == 0 ? sov : srv + " · " + sov;
                lm.Add(new KeyValuePair<string, string>("Сервер", srv.Length == 0 ? "—" : srv));
                lm.Add(new KeyValuePair<string, string>("Адреса", string.IsNullOrEmpty(r.Sentinel.Addresses) ? "—" : r.Sentinel.Addresses));
                lm.Add(new KeyValuePair<string, string>("Время работы", r.Sentinel.Uptime));
                lm.Add(new KeyValuePair<string, string>("Подключений / сеансов", r.Sentinel.LoginCount + " / " + r.Sentinel.SessionCount));
                if (!string.IsNullOrEmpty(r.Sentinel.DriverSummary))
                    lm.Add(new KeyValuePair<string, string>("Драйверы", r.Sentinel.DriverSummary));
            }
            else
            {
                lm.Add(new KeyValuePair<string, string>("Состояние", "Менеджер лицензий не отвечает на 127.0.0.1:1947"));
                lm.Add(new KeyValuePair<string, string>("Причина", string.IsNullOrEmpty(r.Sentinel.Error) ? "служба Sentinel LDK остановлена или не установлена" : r.Sentinel.Error));
                if (r.Sentinel.ServiceInstalled)
                {
                    lm.Add(new KeyValuePair<string, string>("Служба Sentinel LDK",
                        r.Sentinel.ServiceRunning ? "запущена, но не отвечает" : "остановлена — ключи не читаются, пока её не запустить"));
                }
                else
                {
                    lm.Add(new KeyValuePair<string, string>("Служба Sentinel LDK", "не установлена — поставьте драйверы Sentinel LDK"));
                }
                if (r.Hasp4.UsbKeys.Count > 0)
                {
                    lm.Add(new KeyValuePair<string, string>("Что это значит",
                        "физический ключ вставлен, но без службы программа не может прочитать его состав. Запустите службу кнопкой ниже и нажмите «Обновить» (F5)."));
                }
                else
                {
                    lm.Add(new KeyValuePair<string, string>("Что это значит",
                        "ключ не найден ни в USB, ни через менеджер лицензий. Если 1С работает, лицензии могут быть программными или выдаваться по сети."));
                }
            }
            _lm.Set(lm);

            _keyItems = r.Sentinel.Keys;
            List<KeyRow> keyRows = new List<KeyRow>();
            foreach (SentinelKey k in _keyItems)
            {
                KeyRow kr = new KeyRow();
                kr.Id = k.Id;
                kr.Name = string.IsNullOrEmpty(k.Name) ? k.Id : k.Name;
                kr.TypeName = k.TypeName;
                kr.Location = string.IsNullOrEmpty(k.Location) ? "Local" : k.Location;
                kr.Firmware = k.Firmware;
                kr.Sessions = k.Sessions;
                keyRows.Add(kr);
            }
            _keys.DataSource = null;
            _keys.DataSource = keyRows;
            if (_keys.Rows.Count > 0) _keys.Rows[0].Selected = true;

            _featureItems = r.Sentinel.Features;
            FilterFeatures();

            List<KeyValuePair<string, string>> h = new List<KeyValuePair<string, string>>();
            if (r.Hasp4.ServiceInstalled || r.Hasp4.PortListening)
            {
                h.Add(new KeyValuePair<string, string>("Служба", r.Hasp4.ServiceName + (r.Hasp4.ServiceRunning ? " — работает" : " — остановлена")));
                h.Add(new KeyValuePair<string, string>("Версия", string.IsNullOrEmpty(r.Hasp4.Version) ? "—" : r.Hasp4.Version));
                h.Add(new KeyValuePair<string, string>("Порт 475", r.Hasp4.PortListening ? "прослушивается (TCP/UDP)" : "закрыт"));
                h.Add(new KeyValuePair<string, string>("Параметры", string.IsNullOrEmpty(r.Hasp4.IniSummary) ? "—" : r.Hasp4.IniSummary));
                h.Add(new KeyValuePair<string, string>("Настройки 1С", string.IsNullOrEmpty(r.Hasp4.NethaspIni) ? "nethasp.ini не найден" : r.Hasp4.NethaspIni));
                h.Add(new KeyValuePair<string, string>("Примечание", r.Hasp4.Comment));
            }
            else
            {
                h.Add(new KeyValuePair<string, string>("Служба", "HASP License Manager не установлена"));
                h.Add(new KeyValuePair<string, string>("Порт 475", "закрыт"));
                h.Add(new KeyValuePair<string, string>("Примечание", r.Hasp4.Comment));
                h.Add(new KeyValuePair<string, string>("Для 1С 7.7", "Если используется локальный USB-ключ, проверьте, что он вставлен и установлен драйвер HASP."));
            }
            if (!string.IsNullOrEmpty(r.Hasp4.ToolPath))
            {
                string kind = string.Equals(r.Hasp4.ToolKind, "monitor", StringComparison.OrdinalIgnoreCase)
                    ? "HASP Monitor (графическая)"
                    : "HASPInfo (консольная)";
                h.Add(new KeyValuePair<string, string>("Утилита HASP", kind + ": " + r.Hasp4.ToolPath));
            }
            else
            {
                h.Add(new KeyValuePair<string, string>("Утилита HASP", "не найдена — состав лицензий показывает HASP Monitor или отчёт HASPInfo"));
            }
            if (!string.IsNullOrEmpty(r.Hasp4.ReportedKeyId))
            {
                h.Add(new KeyValuePair<string, string>("Ключ по отчёту",
                    r.Hasp4.ReportedKeyId + (string.IsNullOrEmpty(r.Hasp4.ReportedKeyType) ? "" : " · " + r.Hasp4.ReportedKeyType)));
            }
            if (r.Hasp4.UsbKeys.Count > 0)
            {
                h.Add(new KeyValuePair<string, string>("USB-ключ", string.Join("; ", r.Hasp4.UsbKeys.ToArray())));
            }
            if (r.Sentinel.Keys.Count == 0 && r.Hasp4.UsbKeys.Count > 0)
            {
                h.Add(new KeyValuePair<string, string>("Что делать", "ключ вставлен, но менеджер лицензий не отдал данные: запустите службу Sentinel LDK или откройте ACC."));
            }
            if (r.Sentinel.Keys.Count > 0 && r.Sentinel.Status.IndexOf("ACC", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                h.Add(new KeyValuePair<string, string>("Источник данных", "веб-интерфейс ACC (Admin API закрыт, ключи читаются через tab_dev/tab_feat)"));
            }
            _hasp4.Set(h);

            if (_btnService != null)
            {
                bool running = false;
                try
                {
                    foreach (System.ServiceProcess.ServiceController sc in System.ServiceProcess.ServiceController.GetServices())
                    {
                        if (string.Equals(sc.ServiceName, "hasplms", StringComparison.OrdinalIgnoreCase))
                        {
                            running = sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                            break;
                        }
                    }
                }
                catch { }
                _btnService.Enabled = !running;
            }

            if (_btnMonitor != null)
            {
                _btnMonitor.Enabled = !string.IsNullOrEmpty(r.Hasp4.ToolPath) &&
                                      string.Equals(r.Hasp4.ToolKind, "monitor", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ResizeLists()
        {
            int w = Root.ClientSize.Width > 300 ? Root.ClientSize.Width - 12 : 900;
            SetRowHeight(_lmRow, Math.Max(110, _lm.Measure(w) + 34));
            SetRowHeight(_haspRow, Math.Max(150, _hasp4.Measure(w) + 34));
        }

        private void StartSentinelService()
        {
            try
            {
                using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController("hasplms"))
                {
                    if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }
                }
                MessageBox.Show("Служба Sentinel LDK запущена. Нажмите «Обновить» (F5), чтобы прочитать ключи.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить службу: " + ex.Message);
            }
        }

        private void OpenAcc()
        {
            try { System.Diagnostics.Process.Start("http://localhost:1947/_int_/devices.html"); }
            catch { }
        }

        private void OpenMonitor()
        {
            try
            {
                if (_result == null || string.IsNullOrEmpty(_result.Hasp4.ToolPath)) return;
                System.Diagnostics.Process.Start(_result.Hasp4.ToolPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить HASP Monitor: " + ex.Message);
            }
        }

        private void ImportHaspInfo()
        {
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "Отчёт HASPInfo";
                    dlg.Filter = "Отчёт HASPInfo (*.haspinfo;*.txt)|*.haspinfo;*.txt|Все файлы (*.*)|*.*";
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    if (_result == null) return;

                    string text = System.IO.File.ReadAllText(dlg.FileName);
                    _result.Hasp4.ToolPath = dlg.FileName;
                    _result.Hasp4.ToolKind = "info";
                    _result.Hasp4.ToolOutput = text;
                    _result.Hasp4.ReportedKeyId = "";
                    _result.Hasp4.ReportedKeyType = "";
                    CollectHasp4.ParseToolOutput(_result.Hasp4);
                    Bind(_result);
                    MessageBox.Show("Отчёт загружен. Ключ: " +
                                    (string.IsNullOrEmpty(_result.Hasp4.ReportedKeyId) ? "идентификатор не распознан" : _result.Hasp4.ReportedKeyId),
                        "HASPInfo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось прочитать отчёт: " + ex.Message);
            }
        }

        private void FilterFeatures()
        {
            SentinelKey key = null;
            if (_keys.CurrentRow != null && _keys.CurrentRow.Index >= 0 && _keys.CurrentRow.Index < _keyItems.Count)
                key = _keyItems[_keys.CurrentRow.Index];

            List<FeatureRow> rows = new List<FeatureRow>();
            foreach (SentinelFeature f in _featureItems)
            {
                if (key != null && !string.Equals(f.KeyId, key.Id, StringComparison.OrdinalIgnoreCase)) continue;
                FeatureRow row = new FeatureRow();
                row.KeyId = f.KeyId;
                row.FeatureId = f.FeatureId;
                row.Name = string.IsNullOrEmpty(f.ProductName) ? f.Name : (f.Name + " · " + f.ProductName);
                row.Type = f.Type;
                row.Logins = f.Logins;
                row.Limit = f.IsUnlimited ? "∞" : f.Limit;
                row.FreeText = f.IsUnlimited ? "не ограничено" : f.FreeSeats.ToString();
                row.ExpireText = string.IsNullOrEmpty(f.ExpireText) ? f.Restriction : f.ExpireText;
                rows.Add(row);
            }
            _features.DataSource = null;
            _features.DataSource = rows;
        }

        public class FeatureRow
        {
            public string KeyId { get; set; }
            public string FeatureId { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Logins { get; set; }
            public string Limit { get; set; }
            public string FreeText { get; set; }
            public string ExpireText { get; set; }
        }
    }
}
