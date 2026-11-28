using System;
using System.Collections.Generic;

namespace OneCLicenseInspector.Ui
{
    /// <summary>
    /// Общая сборка двух таблиц — «какие лицензии есть» и «кто их занял».
    /// Вынесено из первой страницы полного приложения, чтобы краткое приложение
    /// (только две вкладки) считало те же строки тем же кодом и не расходилось с ним.
    /// </summary>
    internal class UsageData
    {
        public List<LicenseUsagePage.LicenceRow> Licences = new List<LicenseUsagePage.LicenceRow>();
        public List<LicenseUsagePage.SessionRow> Sessions = new List<LicenseUsagePage.SessionRow>();
        /// <summary>Строки подсказки под таблицами (те же, что на первой странице).</summary>
        public List<string> Hint = new List<string>();

        public static UsageData Build(ScanResult r)
        {
            UsageData d = new UsageData();
            List<LicenseUsagePage.LicenceRow> licences = d.Licences;
            List<LicenseUsagePage.SessionRow> sessions = d.Sessions;
            HashSet<string> seenPins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int seatsTotal = 0, busyTotal = 0;
            Dictionary<string, bool> issued = IssuedMap(r);

            // 1. Лицензии из консоли администрирования 8.2 — там есть места и занятость.
            foreach (License82Row row in CollectLicenses.Build82(r))
            {
                LicenseUsagePage.LicenceRow lr = new LicenseUsagePage.LicenceRow();
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
                LicenseUsagePage.LicenceRow lr = new LicenseUsagePage.LicenceRow();
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

            // Занято в таблице сеансов — это ЧИСЛО СЕАНСОВ на эту лицензию, а не поля rac
            // max-users-cur/all: они показывают ёмкость лицензии и путали (20/20 у одного сеанса).
            Dictionary<string, int> sessionsByPin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, LicenseUsagePage.LicenceRow> licenceByPin = new Dictionary<string, LicenseUsagePage.LicenceRow>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> serverPin = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (LicenseUsagePage.LicenceRow lr in licences)
            {
                string dg = CollectLicenses.Digits(lr.Pin);
                if (dg.Length >= 6 && !licenceByPin.ContainsKey(dg)) licenceByPin[dg] = lr;
                if (lr.Kind != null && lr.Kind.IndexOf("сервер", StringComparison.OrdinalIgnoreCase) >= 0) serverPin[dg] = true;
            }

            // 3. Сеансы — с рег. номером лицензии, местами и занятостью.
            if (r.Cluster != null)
            {
                foreach (SessionEntry se in r.Cluster.Sessions)
                {
                    LicenseUsagePage.SessionRow row = new LicenseUsagePage.SessionRow();
                    row.Entry = se;
                    row.User = string.IsNullOrEmpty(se.User) ? "—" : se.User;
                    row.Host = se.Host;
                    row.Ip = CollectCluster.NormalizeIp(se.ClientIp);
                    row.Infobase = Shorten(se.Infobase, 34);
                    row.Number = string.IsNullOrEmpty(se.SessionNum) ? se.SessionId : se.SessionNum;
                    if (row.Number.Length > 10) row.Number = row.Number.Substring(0, 8) + "…";
                    row.Started = ShortDate(se.StartedAt);
                    // Последняя активность — по ней видно, работает человек или сеанс просто висит.
                    row.Activity = ShortDate(se.LastActivity);
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

            // «Занято» = сколько сеансов держат этот PIN; «Мест» — ёмкость лицензии.
            foreach (LicenseUsagePage.SessionRow row in sessions)
            {
                string dg = CollectLicenses.Digits(row.Pin);
                if (dg.Length < 6) continue;
                LicenseUsagePage.LicenceRow lic;
                if (licenceByPin.TryGetValue(dg, out lic) && lic.Seats.Length > 0) row.Seats = lic.Seats;
                bool isServer;
                if (serverPin.TryGetValue(dg, out isServer) && isServer) { row.Busy = "сервер"; continue; }
                int count;
                if (sessionsByPin.TryGetValue(dg, out count)) row.Busy = count.ToString();
            }
            sessions.Sort(delegate (LicenseUsagePage.SessionRow a, LicenseUsagePage.SessionRow b)
            {
                return string.Compare(a.User ?? "", b.User ?? "", StringComparison.OrdinalIgnoreCase);
            });

            // «Срок действия» и «Выдана сервером» показываем всегда, когда есть лицензии;
            // неизвестное значение — прочерк плюс пояснение в подсказке.
            int expUnknown = 0, issUnknown = 0;
            foreach (LicenseUsagePage.LicenceRow lr in licences)
            {
                if (string.IsNullOrEmpty(lr.Expires)) { lr.Expires = "—"; expUnknown++; }
                if (string.IsNullOrEmpty(lr.IssuedByServer)) { lr.IssuedByServer = "—"; issUnknown++; }
            }

            d.Hint.Add("Лицензий: " + licences.Count);
            if (seatsTotal > 0) d.Hint.Add("мест: занято " + busyTotal + " из " + seatsTotal);
            d.Hint.Add("сеансов: " + sessions.Count);
            int ipOnly = 0;
            foreach (LicenseUsagePage.SessionRow row in sessions)
            {
                if (string.IsNullOrEmpty(row.Host) && !string.IsNullOrEmpty(row.Ip)) ipOnly++;
            }
            if (ipOnly > 0) d.Hint.Add("только IP (имени 1С не отдаёт): " + ipOnly);
            if (licences.Count > 0 && issUnknown == licences.Count)
                d.Hint.Add("«выдана сервером»: источник этот признак не сообщает");
            else if (issUnknown > 0)
                d.Hint.Add("«—» в «выдана сервером» — признак не сообщён");
            if (licences.Count > 0 && expUnknown == licences.Count)
                d.Hint.Add("срок действия не читается: у 8.2 файлы лицензий зашифрованы");
            if (r.Cluster != null && !string.IsNullOrEmpty(r.Cluster.Error)) d.Hint.Add("кластер: " + r.Cluster.Error);
            return d;
        }

        /// <summary>
        /// Срок действия из файла лицензии. Платформа пишет «не ограничен» — для человека это
        /// бессрочная лицензия, так и показываем; дату оставляем как есть.
        /// </summary>
        internal static string ExpiresText(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return "";
            if (s.ToLowerInvariant().IndexOf("не огранич", StringComparison.Ordinal) >= 0) return "бессрочно";
            return s;
        }

        /// <summary>
        /// «Выдана сервером» rac сообщает не в файле лицензии, а в сеансах (issued-by-server).
        /// Пустое поле — это «неизвестно», а не «нет»: у импорта из консоли 8.2 признака нет.
        /// </summary>
        internal static Dictionary<string, bool> IssuedMap(ScanResult r)
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

        internal static string IssuedText(Dictionary<string, bool> map, string pin)
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

        /// <summary>Короткая дата «дд.ММ чч:мм» для таблиц.</summary>
        internal static string ShortDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            DateTime d;
            string[] formats = new string[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy H:mm:ss" };
            if (DateTime.TryParseExact(s.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d.ToString("dd.MM HH:mm");
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }

        internal static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > max ? "…" + s.Substring(s.Length - (max - 1)) : s;
        }
    }
}
