using System;
using System.Collections.Generic;
using System.Drawing;
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

        public LicenseUsagePage()
        {
            AddHeader("Лицензии и сеансы", "Сколько мест в лицензиях и кто их занял — данные файлов лицензий и консоли администрирования");

            _licences = MakeGrid();
            _licences.Columns.Add(Col("Рег. номер", "Pin", 130));
            _licences.Columns.Add(Col("Тип", "Kind", 110));
            _licences.Columns.Add(Col("Мест", "Seats", 70));
            _licences.Columns.Add(Col("Занято", "Used", 80));
            _licences.Columns.Add(Col("Свободно", "Free", 90));
            _licences.Columns.Add(Col("Источник", "Source", 150));
            _licences.Columns.Add(Col("Файл", "File", 260));
            _licencesRow = AddRow(WithTitle("Лицензии: регистрационный номер, мест и занято", Card(_licences, new Padding(1))), 150);
            AddSpacer(6);

            _sessions = MakeGrid();
            _sessions.Columns.Add(Col("Рег. номер", "Pin", 115));
            _sessions.Columns.Add(Col("Пользователь", "User", 130));
            _sessions.Columns.Add(Col("Компьютер", "Host", 110));
            _sessions.Columns.Add(Col("Инф. база", "Infobase", 120));
            _sessions.Columns.Add(Col("№", "Number", 60));
            _sessions.Columns.Add(Col("Начало", "Started", 120));
            _sessions.Columns.Add(Col("Активность", "Activity", 120));
            _sessions.Columns.Add(Col("Приложение", "App", 105));
            _sessions.Columns.Add(Col("Состояние", "State", 80));
            _sessions.Columns.Add(Col("Лицензия", "Licence", 110));
            _sessions.Columns.Add(Col("Мест", "Seats", 60));
            _sessions.Columns.Add(Col("Занято", "Busy", 70));
            _sessionsRow = AddRow(WithTitle("Использование лицензий сеансами", Card(_sessions, new Padding(1))), 420);

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
                licences.Add(lr);
                if (!f.IsServerLicense) seatsTotal += Math.Max(0, f.UsersNum);
                if (!f.IsServerLicense && f.UsedBySessions > 0) busyTotal += f.UsedBySessions;
            }

            // 3. Сеансы — с рег. номером лицензии, местами и занятостью.
            if (r.Cluster != null)
            {
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    SessionRow row = new SessionRow();
                    row.User = string.IsNullOrEmpty(se.User) ? "—" : se.User;
                    row.Host = se.Host;
                    row.Infobase = Shorten(se.Infobase, 34);
                    row.Number = string.IsNullOrEmpty(se.SessionNum) ? se.SessionId : se.SessionNum;
                    if (row.Number.Length > 10) row.Number = row.Number.Substring(0, 8) + "…";
                    row.Started = ShortDate(se.StartedAt);
                    row.Activity = ShortDate(se.LastActivity);
                    row.App = se.AppId;
                    bool sleeping = string.Equals(se.Hibernate, "yes", StringComparison.OrdinalIgnoreCase);
                    row.State = string.IsNullOrEmpty(se.Hibernate) ? "" : (sleeping ? "спит" : "активен");

                    if (!string.IsNullOrEmpty(se.LicenseSeries) || !string.IsNullOrEmpty(se.MaxUsersAll))
                    {
                        row.Pin = se.LicenseSeries;
                        row.Licence = string.IsNullOrEmpty(se.LicenseType) ? "программная" : se.LicenseType;
                        row.Seats = se.MaxUsersAll;
                        row.Busy = se.MaxUsersCur;
                    }
                    else if (!string.IsNullOrEmpty(se.LicenseShort))
                    {
                        string kind, pin, file; int used, total;
                        Console82Import.ParseLicenseFull(se.LicenseShort, out kind, out pin, out used, out total, out file);
                        row.Pin = pin;
                        row.Licence = kind;
                        row.Seats = total > 0 ? total.ToString() : "";
                        row.Busy = used >= 0 ? used.ToString() : "";
                        if (row.Number.Length == 0) row.Number = se.SessionId;
                    }
                    else
                    {
                        row.Licence = sleeping ? "не выдана (спит)" : "—";
                    }
                    if (sleeping && row.Pin.Length == 0 && row.Licence.Length == 0) row.Licence = "не выдана (спит)";
                    sessions.Add(row);
                }
            }

            sessions.Sort(delegate (SessionRow a, SessionRow b)
            {
                return string.Compare(a.User ?? "", b.User ?? "", StringComparison.OrdinalIgnoreCase);
            });

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
            if (r.Cluster != null && !string.IsNullOrEmpty(r.Cluster.Error)) hint.Add("кластер: " + r.Cluster.Error);
            _hint.Text = string.Join(" · ", hint.ToArray());
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

        public class LicenceRow
        {
            public string Pin { get; set; }
            public string Kind { get; set; }
            public string Seats { get; set; }
            public string Used { get; set; }
            public string Free { get; set; }
            public string Source { get; set; }
            public string File { get; set; }
        }

        public class SessionRow
        {
            public string Pin { get; set; }
            public string User { get; set; }
            public string Host { get; set; }
            public string Infobase { get; set; }
            public string Number { get; set; }
            public string Started { get; set; }
            public string Activity { get; set; }
            public string App { get; set; }
            public string State { get; set; }
            public string Licence { get; set; }
            public string Seats { get; set; }
            public string Busy { get; set; }
        }
    }
}
