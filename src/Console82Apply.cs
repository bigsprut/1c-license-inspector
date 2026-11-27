using System;
using System.Collections.Generic;

namespace OneCLicenseInspector
{
    /// <summary>Применение таблицы из консоли 8.2 к результату сканирования.</summary>
    internal static class Console82Apply
    {
        public static bool TryApply(ScanResult r, string text, out string summary)
        {
            summary = "";
            if (r == null || string.IsNullOrEmpty(text)) return false;

            if (Console82Import.LooksLikeProcesses(text))
            {
                List<Console82Import.ProcRow> procs = Console82Import.ParseProcesses(text);
                if (procs.Count == 0) return false;

                r.Cluster.Processes.Clear();
                foreach (Console82Import.ProcRow pr in procs)
                {
                    SessionEntry se = new SessionEntry();
                    se.User = pr.Computer;
                    se.Host = ":" + pr.Port;
                    se.AppId = pr.Usage;
                    se.LicenseShort = pr.License;
                    se.SessionId = pr.Pid;
                    se.Usage = pr.Usage;
                    se.Enabled = pr.Enabled;
                    se.Active = pr.Active;
                    se.Memory = pr.Memory;
                    string lk, lp, lfile; int lu, lt;
                    Console82Import.ParseLicenseFull(pr.License, out lk, out lp, out lu, out lt, out lfile);
                    se.LicenseFile = lfile;
                    r.Cluster.Processes.Add(se);
                }
                r.Cluster.Available = true;
                r.Cluster.Error = "";
                if (r.Cluster.Endpoint.IndexOf("консоль", StringComparison.OrdinalIgnoreCase) < 0)
                    r.Cluster.Endpoint = "консоль 8.2";
                string kind, pin; int used, total;
                foreach (Console82Import.ProcRow pr in procs)
                {
                    if (string.IsNullOrEmpty(pr.License)) continue;
                    Console82Import.ParseLicense(pr.License, out kind, out pin, out used, out total);
                    r.Cluster.ImportedServerPin = pin;
                    r.Cluster.ImportedServerUsed = used;
                    r.Cluster.ImportedServerTotal = total;
                    break;
                }
                summary = Console82Import.ProcessSummary(procs);
                r.AddLog(LogLevel.Info, "Консоль 8.2", summary);
                return true;
            }

            List<Console82Import.Row> rows = Console82Import.Parse(text);
            if (rows.Count == 0) return false;

            r.Cluster.Sessions.Clear();
            foreach (Console82Import.Row pr in rows)
            {
                SessionEntry se = new SessionEntry();
                se.SessionId = pr.Session;
                se.User = pr.User;
                se.Host = pr.Host;
                se.AppId = pr.App;
                se.StartedAt = pr.Started;
                se.LicenseShort = pr.License;
                se.Infobase = pr.Infobase;
                r.Cluster.Sessions.Add(se);
            }
            r.Cluster.Available = true;
            r.Cluster.Error = "";
            r.Cluster.Endpoint = "консоль 8.2";
            string ckind, cpin; int cused, ctotal;
            foreach (Console82Import.Row pr in rows)
            {
                if (string.IsNullOrEmpty(pr.License)) continue;
                Console82Import.ParseLicense(pr.License, out ckind, out cpin, out cused, out ctotal);
                r.Cluster.ImportedClientPin = cpin;
                r.Cluster.ImportedClientUsed = cused;
                r.Cluster.ImportedClientTotal = ctotal;
                break;
            }
            summary = Console82Import.Summary(rows);
            r.AddLog(LogLevel.Info, "Консоль 8.2", summary);
            return true;
        }
    }
}
