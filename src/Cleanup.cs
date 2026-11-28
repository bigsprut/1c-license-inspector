using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Уборка следов работы программы на этом компьютере (1.8.0).
    ///
    /// Зачем: программу запускают и на чужих серверах, и после неё остаются файлы — журнал,
    /// дампы MMC, копии сборки, которые кладёт обработчик очереди, выгрузки отчётов в ProgramData
    /// и `cluster.bin` с сохранёнными логином/паролем кластера. Раньше убирать умел только
    /// обработчик (`cleanup` в очереди заданий), а само приложение — нет.
    ///
    /// Что удаляем:
    ///   - `%TEMP%\1c-license-inspector*.log|txt`, `mmc20-*.txt`, `mmc20dump.log`,
    ///     `rac-sessions-err.txt`, `1c-worker-selfdel.cmd`;
    ///   - содержимое `%TEMP%\1c-inspector-worker` (кроме `Worker.exe` и запущенного exe —
    ///     их Windows удалить не даст; каталог дочищается после закрытия окна);
    ///   - каталог `%ProgramData%\1C-License-Inspector` целиком: `drop` (выгрузки отчётов),
    ///     `diag` (диагностика rac) и `cluster.bin` (доступы кластера под DPAPI).
    ///
    /// Чего НЕ трогаем: платформу 1С, службу `ras`, службы и ключи HASP, сам exe программы
    /// и логи других программ. Уборка ничего не устанавливает и не запускает, кроме
    /// короткого `cmd`-скрипта отложенной дочистки, который удаляет сам себя.
    /// </summary>
    internal static class Cleanup
    {
        /// <summary>Каталог, в который обработчик очереди кладёт копии сборки и временные отчёты.</summary>
        public static string WorkerDir
        {
            get
            {
                try { return Path.Combine(Path.GetTempPath(), "1c-inspector-worker"); }
                catch { return ""; }
            }
        }

        /// <summary>Каталог данных программы: выгрузки отчётов, диагностика, сохранённые доступы.</summary>
        public static string DataDir
        {
            get
            {
                try { return Path.Combine(Paths.ProgramData, "1C-License-Inspector"); }
                catch { return ""; }
            }
        }

        private static readonly string[] TempPatterns = new string[]
        {
            "1c-license-inspector.log",
            "1c-license-inspector-crash.log",
            "1c-license-inspector-boot.log",
            "1c-license-inspector-selftest.txt",
            "mmc20-*.txt",
            "mmc20dump.log",
            "rac-sessions-err.txt",
            "1c-worker-selfdel.cmd"
        };

        private class Item
        {
            public string Path = "";
            public string Group = "";
            public long Size;
            public bool Deleted;
        }

        /// <summary>
        /// Что будет удалено — для окна подтверждения, без самого удаления.
        /// Возвращает три строки «категория: N файлов, M КБ» и итог.
        /// </summary>
        public static string Describe()
        {
            List<Item> items = Collect();
            StringBuilder b = new StringBuilder();
            int total = 0;
            long bytes = 0;
            foreach (string g in Groups)
            {
                int n = 0;
                long sz = 0;
                foreach (Item it in items)
                {
                    if (it.Group != g) continue;
                    n++;
                    sz += it.Size;
                }
                total += n;
                bytes += sz;
                b.AppendLine("   " + GroupName(g) + ": " + n + " · " + Fmt.Bytes(sz));
            }
            if (total == 0) return "Удалять нечего: следов работы не найдено.";
            return "Будет удалено " + total + " файл(ов), " + Fmt.Bytes(bytes) + ":\r\n" + b.ToString().TrimEnd();
        }

        /// <summary>
        /// Удалить следы и вернуть текстовый отчёт (его видно в окне, в файле `/cleanup:` и в
        /// ответе обработчика). Журнал удаляется последним, после чего запись в него глушится —
        /// иначе уборка оставила бы свежий файл журнала.
        /// </summary>
        public static string Run()
        {
            List<Item> items = Collect();
            Diag.Log("cleanup: начало уборки, файлов к удалению: " + items.Count);

            List<string> problems = new List<string>();
            long bytes = 0;

            foreach (Item it in items)
            {
                if (IsLog(it.Path)) continue;   // журналы — в самом конце, отдельным проходом
                Delete(it, ref bytes, problems);
            }

            // Журнал удаляется последним и уже после того, как запись в него выключена —
            // иначе уборка оставит файл с одной строкой «начало уборки» (проверено 28.11.2026).
            Diag.Muted = true;
            foreach (Item it in items)
            {
                if (!IsLog(it.Path)) continue;
                Delete(it, ref bytes, problems);
            }
            // Журнал мог появиться уже после сбора списка — добиваем по известным путям.
            AddLateLog(items, bytes, problems);

            // Пустые папки в ProgramData (drop, diag) убираем следом за файлами.
            RemoveEmptyDirs(DataDir, problems);

            string wdir = WorkerDir;
            bool workerPending = false;
            if (!string.IsNullOrEmpty(wdir) && Directory.Exists(wdir))
            {
                workerPending = true;
                ScheduleSelfDelete(wdir, problems);
            }

            StringBuilder b = new StringBuilder();
            int ok = 0;
            foreach (Item it in items) if (it.Deleted) ok++;
            b.AppendLine("Убрано следов работы: " + ok + " из " + items.Count + " файлов (" + Fmt.Bytes(bytes) + ").");
            foreach (string g in Groups)
            {
                int n = 0;
                long sz = 0;
                foreach (Item it in items)
                {
                    if (it.Group != g || !it.Deleted) continue;
                    n++;
                    sz += it.Size;
                }
                if (n == 0) continue;
                b.AppendLine("   " + GroupName(g) + ": " + n + " файл(ов), " + Fmt.Bytes(sz));
            }
            if (problems.Count > 0)
            {
                b.AppendLine("Не удалось:");
                int shown = 0;
                foreach (string p in problems)
                {
                    if (shown >= 10) { b.AppendLine("   и ещё " + (problems.Count - shown) + " — обычно это файлы в защищённых папках"); break; }
                    b.AppendLine("   " + p);
                    shown++;
                }
            }
            if (workerPending)
                b.AppendLine("Копии для сервера: каталог дочистится сам после закрытия окна обработчика (до 2 минут).");
            b.AppendLine("Сохранённые доступы кластера (cluster.bin): " +
                         (File.Exists(Path.Combine(DataDir, "cluster.bin")) ? "остались" : "удалены"));
            b.AppendLine("Не тронуты: платформа 1С, служба ras, службы и ключи HASP.");
            return b.ToString().TrimEnd();
        }

        private static readonly string[] Groups = new string[] { "temp", "worker", "data" };

        private static string GroupName(string group)
        {
            if (group == "temp") return "журналы и дампы в %TEMP%";
            if (group == "worker") return "копии программы для сервера";
            if (group == "data") return "выгрузки и доступы в ProgramData";
            return group;
        }

        private static List<Item> Collect()
        {
            List<Item> items = new List<Item>();
            string temp = "";
            try { temp = Path.GetTempPath(); } catch { }
            foreach (string pattern in TempPatterns) AddFiles(items, temp, pattern, null, false, "temp");

            string self = "";
            try { self = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName); } catch { }
            string[] skip = new string[] { "Worker.exe", self };
            AddFiles(items, WorkerDir, "*.*", skip, false, "worker");
            AddFiles(items, DataDir, "*.*", null, true, "data");
            return items;
        }

        private static void AddFiles(List<Item> items, string dir, string pattern, string[] skip, bool recursive, string group)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                if (!Directory.Exists(dir)) return;
                SearchOption opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string f in Directory.GetFiles(dir, pattern, opt))
                {
                    if (skip != null && Skip(skip, Path.GetFileName(f))) continue;
                    Item it = new Item();
                    it.Path = f;
                    it.Group = group;
                    try { it.Size = new FileInfo(f).Length; } catch { }
                    items.Add(it);
                }
            }
            catch { }
        }

        private static bool Skip(string[] skip, string name)
        {
            foreach (string s in skip)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsLog(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string name = Path.GetFileName(path);
            return name.StartsWith("1c-license-inspector", StringComparison.OrdinalIgnoreCase) &&
                   name.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Журнал может появиться уже после сбора списка (первая запись идёт из `Boot`) —
        /// добиваем его по известным путям: `%TEMP%` и папка рядом с exe.
        /// </summary>
        private static void AddLateLog(List<Item> items, long bytes, List<string> problems)
        {
            List<string> paths = new List<string>();
            try { paths.Add(Diag.LogPath); } catch { }
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                    paths.Add(Path.Combine(baseDir, "1c-license-inspector.log"));
            }
            catch { }
            foreach (string p in paths)
            {
                Item it = new Item();
                it.Path = p;
                it.Group = "temp";
                long before = bytes;
                Delete(it, ref bytes, problems);
                if (it.Deleted) items.Add(it);
                else if (bytes == before && File.Exists(p)) { }   // не смогли — причина уже в problems
            }
        }

        private static void Delete(Item it, ref long bytes, List<string> problems)
        {
            try
            {
                if (!File.Exists(it.Path)) return;
                File.Delete(it.Path);
                it.Deleted = true;
                bytes += it.Size;
            }
            catch (Exception ex)
            {
                string reason = ex is UnauthorizedAccessException ? "нет прав" : ex.Message;
                problems.Add(Path.GetFileName(it.Path) + " — " + reason);
            }
        }

        private static void RemoveEmptyDirs(string dir, List<string> problems)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                if (!Directory.Exists(dir)) return;
                try { Directory.Delete(dir, true); }
                catch (Exception ex) { problems.Add(Path.GetFileName(dir) + " (каталог) — " + ex.Message); }
            }
            catch { }
        }

        /// <summary>
        /// Отложенная дочистка: каталог с запущенным `Worker.exe` сейчас не удалить, поэтому
        /// пишем короткий cmd, который пытается удалить его каждые 4 секунды (до 8 минут) и
        /// исчезает сам, как только каталога не станет. Так после закрытия окна обработчика
        /// на сервере не остаётся ничего.
        /// </summary>
        private static void ScheduleSelfDelete(string dir, List<string> problems)
        {
            try
            {
                string temp = Path.GetTempPath();
                string cmd = Path.Combine(temp, "1c-worker-selfdel.cmd");
                StringBuilder s = new StringBuilder();
                s.AppendLine("@echo off");
                s.AppendLine("for /L %%i in (1,1,120) do (");
                s.AppendLine("  del /q \"" + dir + "\\*.*\" >nul 2>&1");
                s.AppendLine("  rd /q \"" + dir + "\" >nul 2>&1");
                s.AppendLine("  if not exist \"" + dir + "\" goto done");
                s.AppendLine("  ping -n 4 127.0.0.1 >nul");
                s.AppendLine(")");
                s.AppendLine(":done");
                s.AppendLine("del /q \"%~f0\" >nul 2>&1");
                File.WriteAllText(cmd, s.ToString(), Encoding.Default);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                problems.Add("дочистка каталога не поставлена: " + ex.Message);
            }
        }
    }
}
