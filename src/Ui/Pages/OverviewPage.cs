using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal class OverviewPage : PageBase
    {
        private StatCard _cPlatforms;
        private StatCard _cKeys;
        private StatCard _cSeats;
        private StatCard _cFiles;
        private StatusListPanel _status;
        private WarnPanel _warn;
        private KeyValueList _facts;
        private int _colsRow;
        private int _factsRow;
        private TableLayoutPanel _cols;

        public OverviewPage()
        {
            AddHeader("Обзор", "Что найдено на этом компьютере и всё ли в порядке с лицензиями");

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.ColumnCount = 4;
            cards.RowCount = 1;
            cards.BackColor = Color.Transparent;
            for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            _cPlatforms = NewCard("—", "Платформы 1С", "установленные версии", Theme.Accent);
            _cKeys = NewCard("—", "Аппаратные ключи", "Sentinel LDK / HASP", Theme.Blue);
            _cSeats = NewCard("—", "Свободные места", "в аппаратных ключах", Theme.Green);
            _cFiles = NewCard("—", "Файлы лицензий", "программные лицензии", Color.FromArgb(147, 112, 190));

            cards.Controls.Add(_cPlatforms, 0, 0);
            cards.Controls.Add(_cKeys, 1, 0);
            cards.Controls.Add(_cSeats, 2, 0);
            cards.Controls.Add(_cFiles, 3, 0);
            AddRow(cards, 104);
            AddSpacer(8);

            _cols = new TableLayoutPanel();
            _cols.ColumnCount = 2;
            _cols.RowCount = 1;
            _cols.BackColor = Color.Transparent;
            _cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));
            _cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));

            _status = new StatusListPanel();
            _status.Dock = DockStyle.Fill;
            Panel left = Card(_status, new Padding(0));
            _cols.Controls.Add(WithTitle("Состояние лицензирования", left), 0, 0);

            _warn = new WarnPanel();
            _warn.Dock = DockStyle.Fill;
            Panel right = Card(_warn, new Padding(0));
            _cols.Controls.Add(WithTitle("Предупреждения", right), 1, 0);

            _colsRow = _row;
            AddRow(_cols, 320);
            AddSpacer(8);

            _facts = new KeyValueList();
            _facts.Dock = DockStyle.Fill;
            Panel factCard = Card(_facts, new Padding(0));
            _factsRow = AddRow(WithTitle("Краткая сводка", factCard), 186);
        }

        private Panel WithTitle(string title, Control body)
        {
            Panel p = new TableLayoutPanelTitle(title, body);
            return p;
        }

        private StatCard NewCard(string value, string label, string hint, Color accent)
        {
            StatCard c = new StatCard();
            c.Value = value;
            c.Label = label;
            c.Hint = hint;
            c.Accent = accent;
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 0, 10, 0);
            return c;
        }

        public override void Bind(ScanResult r)
        {
            _cPlatforms.Value = r.Platforms.Count.ToString();
            _cPlatforms.Hint = r.Platforms.Count == 0 ? "не найдены" : BestPlatform(r);
            _cPlatforms.Invalidate();

            _cKeys.Value = r.Sentinel.Keys.Count.ToString();
            _cKeys.Hint = r.Sentinel.Keys.Count == 0 ? "ключей не видно" : "Sentinel LDK";
            _cKeys.Invalidate();

            int clTotal = 0;
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                string kind = ((lf.LicenseType ?? "") + " " + (lf.Product ?? "")).ToLowerInvariant();
                if (kind.IndexOf("клиент") >= 0 && kind.IndexOf("сервер") < 0) clTotal += Math.Max(1, lf.UsersNum);
            }
            int clUsed = -1;
            if (r.Cluster != null && r.Cluster.Available)
            {
                clUsed = 0;
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    string sl = ((se.LicenseShort ?? "") + " " + (se.LicenseFullName ?? "") + " " + (se.LicenseType ?? "")).ToLowerInvariant();
                    if ((sl.IndexOf("клиент") >= 0 || se.AppId == "1CV8C" || se.AppId == "Designer" || se.AppId == "WebClient") && sl.IndexOf("сервер") < 0)
                        clUsed++;
                }
            }

            int total = r.TotalSeats();            int free = r.TotalFreeSeats();
            if (r.Cluster != null && r.Cluster.ImportedClientTotal > 0)
            {
                int used = r.Cluster.ImportedClientUsed >= 0 ? r.Cluster.ImportedClientUsed : clUsed;
                _cSeats.Value = Math.Max(0, r.Cluster.ImportedClientTotal - used) + " из " + r.Cluster.ImportedClientTotal;
                _cSeats.Hint = "клиентских свободно (занято " + used + ", консоль 8.2)";
                _cSeats.Accent = r.Cluster.ImportedClientTotal - used <= 0 ? Theme.Red : Theme.Green;
            }
            else if (clUsed >= 0 && clTotal > 0)
            {
                _cSeats.Value = Math.Max(0, clTotal - clUsed) + " из " + clTotal;
                _cSeats.Hint = "клиентских свободно (занято " + clUsed + ")";
                _cSeats.Accent = clTotal - clUsed <= 0 ? Theme.Red : Theme.Green;
            }
            else
            {
                _cSeats.Value = total > 0 ? (free + " из " + total) : "—";
                _cSeats.Hint = total > 0 ? "занято " + r.TotalBusySeats() : "нет данных";
                _cSeats.Accent = total > 0 && free == 0 ? Theme.Red : Theme.Green;
            }
            _cSeats.Invalidate();

            _cFiles.Value = r.LicenseFiles.Count.ToString();
            _cFiles.Hint = r.LicenseFiles.Count == 0 ? "лицензий не найдено" : "лицензий (1 файл = 1 лицензия)";
            _cFiles.Invalidate();

            List<StatusItem> items = new List<StatusItem>();
            items.Add(new StatusItem(
                r.Platforms.Count > 0 ? Theme.Green : Theme.Accent,
                "Платформы 1С",
                r.Platforms.Count > 0
                    ? "Найдено " + r.Platforms.Count + ": " + BestPlatform(r)
                    : "Установленные платформы 1С не обнаружены."));

            items.Add(new StatusItem(
                r.Sentinel.Available ? (r.Sentinel.Keys.Count > 0 ? Theme.Green : Theme.Accent) : Theme.Muted,
                "Менеджер лицензий Sentinel LDK",
                r.Sentinel.Available
                    ? "Работает" + (string.IsNullOrEmpty(r.Sentinel.Version) ? "" : ", версия " + r.Sentinel.Version) +
                      ". Ключей: " + r.Sentinel.Keys.Count + ", компонентов: " + r.Sentinel.Features.Count + "."
                    : "Не отвечает на порту 1947. Аппаратные ключи HASP HL/Sentinel HL могут быть недоступны."));

            string hasp4 = r.Hasp4.ServiceInstalled
                ? (r.Hasp4.ServiceRunning ? "Служба HASP License Manager работает" : "Служба HASP License Manager остановлена")
                : "не установлена";
            items.Add(new StatusItem(
                r.Hasp4.ServiceInstalled ? (r.Hasp4.ServiceRunning ? Theme.Green : Theme.Accent) : Theme.Muted,
                "Менеджер HASP4 Net (1С 7.7 / 8.2)",
                hasp4 + (r.Hasp4.PortListening ? ", порт 475 открыт." : ", порт 475 закрыт.") +
                (string.IsNullOrEmpty(r.Hasp4.Version) ? "" : " Версия " + r.Hasp4.Version + ".")));

            items.Add(new StatusItem(
                r.LicenseFiles.Count > 0 ? Theme.Green : Theme.Muted,
                "Программные лицензии",
                r.LicenseFiles.Count > 0
                    ? "Файлов *.lic: " + r.LicenseFiles.Count + ". Каталоги: " + string.Join(", ", r.LicenseStorages.ToArray())
                    : "Файлы программных лицензий не найдены ни в одном стандартном каталоге."));

            _status.Items = items;
            _warn.Items = r.Warnings;
            _warn.Invalidate();

            int totalW = Root.ClientSize.Width > 300 ? Root.ClientSize.Width : 900;
            int leftW = (int)((totalW - 10) * 0.55);
            int rightW = (int)((totalW - 10) * 0.45);
            int h = Math.Max(_status.Measure(leftW), _warn.Measure(rightW)) + 34;
            SetRowHeight(_colsRow, Math.Max(240, h));
            _cols.PerformLayout();
            Root.PerformLayout();

            List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>();
            facts.Add(new KeyValuePair<string, string>("Компьютер", r.System.ComputerName + " (" + r.System.OsCaption + " " + r.System.OsArch + ")"));
            facts.Add(new KeyValuePair<string, string>("Лицензий в работе", r.Sentinel.Sessions.Count > 0 ? r.Sentinel.Sessions.Count + " сеансов" : "нет активных сеансов"));
            facts.Add(new KeyValuePair<string, string>("Файлов лицензий", r.LicenseFiles.Count.ToString()));
            facts.Add(new KeyValuePair<string, string>("Каталоги лицензий", r.LicenseStorages.Count == 0 ? "—" : string.Join("; ", r.LicenseStorages.ToArray())));
            facts.Add(new KeyValuePair<string, string>("Проверка выполнена", Fmt.Date(r.Finished) + " за " + Fmt.Duration(r.Finished - r.Started)));
            _facts.Set(facts);
        }

        private static string BestPlatform(ScanResult r)
        {
            PlatformInfo best = null;
            foreach (PlatformInfo p in r.Platforms)
            {
                if (best == null) best = p;
                else if (string.Compare(p.Version, best.Version, StringComparison.OrdinalIgnoreCase) > 0) best = p;
            }
            if (best == null) return "";
            return best.Family + " " + best.Version + " " + best.Bitness;
        }

        private class TableLayoutPanelTitle : Panel
        {
            public TableLayoutPanelTitle(string title, Control body)
            {
                BackColor = Color.Transparent;
                Dock = DockStyle.Fill;
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

                Controls.Add(t);
            }
        }
    }
}
