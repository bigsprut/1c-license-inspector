using System;
using System.IO;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Автоматическая выгрузка результатов на диск локальной машины через RDP-редирект
    /// (\\tsclient\...). Сергей запускает программу на сервере, файлы появляются
    /// в его папке проекта, агент читает их оттуда.
    /// </summary>
    internal static class AutoDrop
    {
        public const string Preferred = @"\\tsclient\D\work\Learn\DSHarness\1CCommon\drop";

        public static string Dir()
        {
            try
            {
                if (Directory.Exists(Preferred)) return Preferred;
                string parent = Path.GetDirectoryName(Preferred.TrimEnd('\\'));
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    Directory.CreateDirectory(Preferred);
                    return Preferred;
                }
            }
            catch { }

            try
            {
                string fallback = Path.Combine(Paths.ProgramData, "1C-License-Inspector", "drop");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
            catch { return Path.GetTempPath(); }
        }

        public static void Write(ScanResult r)
        {
            try
            {
                string dir = Dir();
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string name = (r.System.ComputerName ?? "server").Trim();
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');

                string html = Path.Combine(dir, "report-" + name + "-" + stamp + ".html");
                File.WriteAllText(html, Report.BuildHtml(r), new UTF8Encoding(true));

                string digest = Report.BuildDigest(r, Diag.Tail(60000));
                File.WriteAllText(Path.Combine(dir, "digest-" + name + "-" + stamp + ".txt"), digest, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, "latest-digest.txt"), digest, new UTF8Encoding(false));

                try { File.Copy(Diag.LogPath, Path.Combine(dir, "latest.log"), true); } catch { }

                if (r.Cluster != null)
                {
                    if (!string.IsNullOrEmpty(r.Cluster.Raw))
                        File.WriteAllText(Path.Combine(dir, "latest-rac-cluster.txt"), r.Cluster.Raw, new UTF8Encoding(false));
                    if (!string.IsNullOrEmpty(r.Cluster.SessionRaw))
                        File.WriteAllText(Path.Combine(dir, "latest-rac-sessions.txt"), r.Cluster.SessionRaw, new UTF8Encoding(false));
                }

                Diag.Log("drop: " + dir);
            }
            catch (Exception ex)
            {
                Diag.Log("drop failed: " + ex.Message);
            }
        }
    }
}
