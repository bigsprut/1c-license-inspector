using System;
using System.IO;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Автоматическое чтение консоли 1С 8.2 при сканировании: если rac недоступен,
    /// а на машине есть оснастка 1Cv82, читаем сеансы и рабочие процессы через MMC 2.0.
    /// </summary>
    internal static class AutoConsole82
    {
        public static string FindMsc()
        {
            string[] probes = new string[]
            {
                Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers (x86-64).msc"),
                Path.Combine(Paths.ProgramFiles, "1cv82", "common", "1CV8 Servers.msc"),
                Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers.msc"),
                Path.Combine(Paths.ProgramFilesX86, "1cv82", "common", "1CV8 Servers (x86-64).msc")
            };
            foreach (string msc in probes) if (File.Exists(msc)) return msc;
            return "";
        }

        public static bool TryAutoCollect(ScanResult r)
        {
            try
            {
                if (r.Cluster != null && r.Cluster.Available && r.Cluster.Sessions.Count > 0)
                    return false;

                string msc = FindMsc();
                if (string.IsNullOrEmpty(msc))
                {
                    r.AddLog(LogLevel.Info, "Консоль 8.2", "оснастка 1CV8 Servers.msc не найдена — автосбор пропущен");
                    return false;
                }

                r.AddLog(LogLevel.Info, "Консоль 8.2", "оснастка: " + msc);
                string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mmc20Dump.exe");
                if (!File.Exists(helper))
                {
                    r.AddLog(LogLevel.Warn, "Консоль 8.2",
                        "не найден Mmc20Dump.exe рядом с программой — положите его в ту же папку, иначе автосбор 8.2 не работает");
                    return false;
                }

                string errProc, errSess;
                string procTsv = Mmc20Reader.ReadTable(msc, "Рабочие процессы", out errProc);
                string sessTsv = Mmc20Reader.ReadTable(msc, "Сеансы", out errSess);

                bool ok = false;
                string summary;
                if (!string.IsNullOrEmpty(procTsv) && Console82Apply.TryApply(r, procTsv, out summary))
                {
                    r.AddLog(LogLevel.Info, "Консоль 8.2", "авто (серверные): " + summary);
                    ok = true;
                }
                if (!string.IsNullOrEmpty(sessTsv) && Console82Apply.TryApply(r, sessTsv, out summary))
                {
                    r.AddLog(LogLevel.Info, "Консоль 8.2", "авто (клиентские): " + summary);
                    ok = true;
                }
                if (!ok)
                {
                    r.AddLog(LogLevel.Warn, "Консоль 8.2",
                        "автосбор не удался: процессы — " + errProc + "; сеансы — " + errSess);
                }
                return ok;
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Warn, "Консоль 8.2", "автосбор: " + ex.Message);
                return false;
            }
        }
    }
}
