using System;
using System.Collections.Generic;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Разбор таблицы сеансов, скопированной из консоли администрирования 1С 8.2 (Ctrl+C).
    /// Колонки: Инф. база, Номер сеанса, Время начала, Последняя активность, Компьютер,
    /// Пользователь, Приложение, Язык, Сервер, Порт, Процесс, Соединение, ... , Лицензия.
    /// </summary>
    internal static class Console82Import
    {
        public class Row
        {
            public string Infobase = "";
            public string Session = "";
            public string Started = "";
            public string LastActivity = "";
            public string Host = "";
            public string User = "";
            public string App = "";
            public string License = "";
        }

        public static List<Row> Parse(string text)
        {
            List<Row> rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string[] header = null;
            int idxBase = 0, idxSession = 1, idxStarted = 2, idxLast = 3, idxHost = 4, idxUser = 5, idxApp = 6, idxLic = -1;

            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.TrimEnd();
                if (line.Length == 0) continue;
                string[] cells = line.Split('\t');
                if (cells.Length < 5) continue;

                bool isHeader = (line.IndexOf("Номер сеанса", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("Сеанс", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                line.IndexOf("Пользователь", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isHeader)
                {
                    header = cells;
                    for (int i = 0; i < header.Length; i++)
                    {
                        string h = header[i].Trim().ToLowerInvariant();
                        if (h.StartsWith("инф")) idxBase = i;
                        else if (h.StartsWith("номер")) idxSession = i;
                        else if (h.StartsWith("время начала")) idxStarted = i;
                        else if (h.StartsWith("последняя")) idxLast = i;
                        else if (h.StartsWith("компьютер")) idxHost = i;
                        else if (h.StartsWith("пользователь")) idxUser = i;
                        else if (h.StartsWith("приложение")) idxApp = i;
                        else if (h.StartsWith("лицензия")) idxLic = i;
                    }
                    continue;
                }
                if (header == null) continue;

                Row row = new Row();
                row.Infobase = Cell(cells, idxBase);
                row.Session = Cell(cells, idxSession);
                row.Started = Cell(cells, idxStarted);
                row.LastActivity = Cell(cells, idxLast);
                row.Host = Cell(cells, idxHost);
                row.User = Cell(cells, idxUser);
                row.App = Cell(cells, idxApp);
                row.License = idxLic >= 0 ? Cell(cells, idxLic) : Cell(cells, cells.Length - 1);
                if (string.IsNullOrEmpty(row.Infobase) && string.IsNullOrEmpty(row.User)) continue;
                rows.Add(row);
            }
            return rows;
        }

        private static string Cell(string[] cells, int index)
        {
            if (index < 0 || index >= cells.Length) return "";
            return cells[index].Trim();
        }

        public static void ParseLicense(string license, out string kind, out string pin, out int used, out int total)
        {
            string file;
            ParseLicenseFull(license, out kind, out pin, out used, out total, out file);
        }

        /// <summary>
        /// Полная строка лицензии из консоли 8.2, например:
        /// «Сервер, 2068, <host>, 1541, <PIN> 20 20, File://C:/.../20170330191441.lic».
        /// </summary>
        public static void ParseLicenseFull(string license, out string kind, out string pin, out int used, out int total, out string file)
        {
            kind = ""; pin = ""; used = -1; total = -1; file = "";
            if (string.IsNullOrEmpty(license)) return;

            string[] segs = license.Split(',');
            if (segs.Length == 0) return;
            kind = segs[0].Trim();

            List<int> numbers = new List<int>();
            for (int i = 1; i < segs.Length; i++)
            {
                string seg = segs[i].Trim();
                if (seg.StartsWith("File://", StringComparison.OrdinalIgnoreCase))
                {
                    string p = seg.Substring(7).Trim();
                    int slash = p.LastIndexOf('/');
                    file = slash >= 0 ? p.Substring(slash + 1) : p;
                    continue;
                }
                if (seg.Length == 0) continue;

                string[] parts = seg.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    int n;
                    if (int.TryParse(part, out n)) numbers.Add(n);
                }
                if (pin.Length == 0)
                {
                    foreach (string part in parts)
                    {
                        string digits = part;
                        if (digits.Length >= 6 && IsDigits(digits)) { pin = digits; break; }
                    }
                }
            }

            // PIN мог оказаться в общем списке чисел
            if (pin.Length == 0)
            {
                foreach (int n in numbers)
                {
                    if (n >= 100000) { pin = n.ToString(); break; }
                }
            }

            // Два последних числа — «занято» и «всего»
            if (numbers.Count >= 2)
            {
                used = numbers[numbers.Count - 2];
                total = numbers[numbers.Count - 1];
            }
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        public static string LicenseUsageText(string license)
        {
            string kind, pin; int used, total;
            ParseLicense(license, out kind, out pin, out used, out total);
            if (used >= 0 && total >= 0) return kind + ", PIN " + pin + ": занято " + used + " из " + total;
            if (pin.Length > 0) return kind + ", PIN " + pin;
            return kind;
        }
        public static string LicenseKind(string license)
        {
            if (string.IsNullOrEmpty(license)) return "";
            int comma = license.IndexOf(',');
            return comma > 0 ? license.Substring(0, comma).Trim() : license.Trim();
        }

        public class ProcRow
        {
            public string Computer = "";
            public string Port = "";
            public string Usage = "";
            public string Enabled = "";
            public string Active = "";
            public string Pid = "";
            public string Memory = "";
            public string License = "";
        }

        public static bool LooksLikeProcesses(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("Порт", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   text.IndexOf("PID", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   text.IndexOf("Лицензия", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   text.IndexOf("Номер сеанса", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static List<ProcRow> ParseProcesses(string text)
        {
            List<ProcRow> rows = new List<ProcRow>();
            if (string.IsNullOrEmpty(text)) return rows;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string[] header = null;
            int iComp = 0, iPort = 1, iUse = 2, iOn = 3, iAct = 4, iPid = 5, iMem = 6, iLic = -1;
            foreach (string raw in lines)
            {
                if (raw == null) continue;
                string line = raw.TrimEnd();
                if (line.Length == 0) continue;
                string[] c = line.Split('\t');
                if (c.Length < 4) continue;
                if (line.IndexOf("Порт", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (line.IndexOf("Лицензия", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("PID", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    header = c;
                    for (int i = 0; i < header.Length; i++)
                    {
                        string h = header[i].Trim().ToLowerInvariant();
                        if (h.StartsWith("компьютер")) iComp = i;
                        else if (h.StartsWith("порт")) iPort = i;
                        else if (h.StartsWith("использ")) iUse = i;
                        else if (h.StartsWith("включ")) iOn = i;
                        else if (h.StartsWith("актив")) iAct = i;
                        else if (h.StartsWith("pid")) iPid = i;
                        else if (h.StartsWith("память")) iMem = i;
                        else if (h.StartsWith("лицензия")) iLic = i;
                    }
                    continue;
                }
                if (header == null) continue;
                ProcRow r = new ProcRow();
                r.Computer = Cell(c, iComp);
                r.Port = Cell(c, iPort);
                r.Usage = Cell(c, iUse);
                r.Enabled = Cell(c, iOn);
                r.Active = Cell(c, iAct);
                r.Pid = Cell(c, iPid);
                r.Memory = Cell(c, iMem);
                r.License = iLic >= 0 ? Cell(c, iLic) : Cell(c, c.Length - 1);
                if (r.Computer.Length == 0 && r.Port.Length == 0) continue;
                rows.Add(r);
            }
            return rows;
        }

        public static string ProcessSummary(List<ProcRow> rows)
        {
            int withLicense = 0;
            Dictionary<string, int> kinds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> pins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ProcRow r in rows)
            {
                if (string.IsNullOrEmpty(r.License)) continue;
                withLicense++;
                string kind = LicenseKind(r.License);
                int n;
                kinds.TryGetValue(kind, out n); kinds[kind] = n + 1;
                string pin = LicensePin(r.License);
                if (pin.Length > 0) { pins.TryGetValue(pin, out n); pins[pin] = n + 1; }
            }
            StringBuilder b = new StringBuilder();
            b.Append("Процессов (серверные): ").Append(rows.Count).Append(", с лицензией: ").Append(withLicense);
            foreach (ProcRow row in rows)
            {
                if (string.IsNullOrEmpty(row.License)) continue;
                b.Append(" · ").Append(LicenseUsageText(row.License));
                break;
            }
            foreach (KeyValuePair<string, int> kv in kinds) b.Append(" · ").Append(kv.Key).Append(": ").Append(kv.Value);
            foreach (KeyValuePair<string, int> kv in pins) b.Append(" · PIN ").Append(kv.Key).Append(": ").Append(kv.Value);
            return b.ToString();
        }

        public static string LicensePin(string license)
        {
            if (string.IsNullOrEmpty(license)) return "";
            int comma = license.IndexOf(',');
            string rest = comma >= 0 ? license.Substring(comma + 1).Trim() : license.Trim();
            string[] parts = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "";
        }
        public static string Summary(List<Row> rows)
        {
            int withLicense = 0;
            Dictionary<string, int> kinds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Row r in rows)
            {
                if (string.IsNullOrEmpty(r.License)) continue;
                withLicense++;
                string kind = LicenseKind(r.License);
                if (kind.Length == 0) kind = "—";
                int n;
                kinds.TryGetValue(kind, out n);
                kinds[kind] = n + 1;
            }
            StringBuilder b = new StringBuilder();
            b.Append("Сеансов (клиентские): ").Append(rows.Count).Append(", с лицензией: ").Append(withLicense);
            foreach (Row row in rows)
            {
                if (string.IsNullOrEmpty(row.License)) continue;
                b.Append(" · ").Append(LicenseUsageText(row.License));
                break;
            }
            foreach (KeyValuePair<string, int> kv in kinds)
                b.Append(" · ").Append(kv.Key).Append(": ").Append(kv.Value);
            return b.ToString();
        }
    }
}
