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

                bool isHeader = line.IndexOf("Номер сеанса", StringComparison.OrdinalIgnoreCase) >= 0 &&
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

        public static string LicenseKind(string license)
        {
            if (string.IsNullOrEmpty(license)) return "";
            int comma = license.IndexOf(',');
            return comma > 0 ? license.Substring(0, comma).Trim() : license.Trim();
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
            b.Append("Сеансов: ").Append(rows.Count).Append(", с лицензией: ").Append(withLicense);
            foreach (KeyValuePair<string, int> kv in kinds)
                b.Append(" · ").Append(kv.Key).Append(": ").Append(kv.Value);
            return b.ToString();
        }
    }
}
