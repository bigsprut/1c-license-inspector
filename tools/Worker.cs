using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

// Обработчик очереди заданий для удалённого тестирования «Инспектора лицензий 1С».
// Один exe без зависимостей: работает на Server 2003 (.NET 4.0) и на 2016, не требует
// PowerShell вообще (на 2003 «из коробки» PowerShell 1.0, где нет try/catch и -ExecutionPolicy).
//
// Запуск:  Worker.exe all "\\tsclient\D\work\Learn\DSHarness\1CCommon"
//          Worker.exe s200 "\\tsclient\D\work\Learn\DSHarness\1CCommon"
// Выполняет только белый список команд из queue\<имя>\*.task и пишет результат в drop.
// Закрыть окно = выключить доступ (ничего не остаётся в системе).
internal static class Worker
{
    private static string _root = "";
    private static string _name = "all";
    private static string _queue = "";
    private static string _done = "";
    private static string _drop = "";
    private static string _work = "";
    private static string _localApp = "";
    private static string _log = "";

    private static int Main(string[] args)
    {
        _name = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "all";
        if (args.Length > 1) _root = args[1].Trim().TrimEnd('\\');
        if (_root.Length == 0)
        {
            // exe лежит в <проект>\build\Worker.exe
            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            _root = Directory.GetParent(exeDir) != null ? Directory.GetParent(exeDir).FullName : exeDir;
        }

        _queue = Path.Combine(_root, @"queue\" + _name);
        _done = Path.Combine(_queue, "done");
        _drop = Path.Combine(_root, "drop");
        _work = Path.Combine(Path.GetTempPath(), "1c-inspector-worker");
        _localApp = LocalAppPath();
        _log = Path.Combine(_drop, "worker-" + _name + ".log");

        EnsureDir(_queue);
        EnsureDir(_done);
        EnsureDir(_drop);
        EnsureDir(_work);

        KillLeftoversPrefix("1cli");
        CleanOldCopies();
        KillDuplicateWorkers();

        Log("запуск обработчика: имя=" + _name + ", корень=" + _root);
        Console.WriteLine("Host      : " + Environment.MachineName);
        Console.WriteLine("Project   : " + _root);
        Console.WriteLine("Queue     : " + _queue);
        Console.WriteLine("App       : " + AppPath());
        Console.WriteLine("Keep this window open. Close it to stop the worker.");

        string heartbeat = Path.Combine(_drop, "worker-" + _name + ".txt");
        while (true)
        {
            try
            {
                File.WriteAllText(heartbeat, DateTime.Now.ToString("s") + " · " + Environment.MachineName + " · " + _name, new UTF8Encoding(false));
                string task = NextTask();
                if (task.Length == 0) { Thread.Sleep(1200); continue; }
                HandleTask(task);
            }
            catch (Exception ex)
            {
                Log("ошибка цикла: " + ex.GetType().Name + ": " + ex.Message);
                Thread.Sleep(1000);
            }
        }
    }

    private static string NextTask()
    {
        try
        {
            string[] files = Directory.GetFiles(_queue, "*.task");
            if (files.Length == 0) return "";
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[0];
        }
        catch { return ""; }
    }

    private static void HandleTask(string taskFile)
    {
        string id = Path.GetFileNameWithoutExtension(taskFile);
        string command = "";
        try { command = File.ReadAllText(taskFile).Trim().ToLowerInvariant(); }
        catch { }
        int sp = command.IndexOfAny(new char[] { '\r', '\n' });
        if (sp > 0) command = command.Substring(0, sp).Trim();

        List<string> body = new List<string>();
        body.Add("Задание: " + id + " · команда: " + command);
        body.Add("Сервер: " + Environment.MachineName + " · " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
        body.Add("");
        Log("задание " + id + ": " + command);

        try
        {
            switch (command)
            {
                case "noop":
                    body.Add("Обработчик на связи.");
                    break;
                case "version":
                    body.Add("сборка на сервере: " + AppVersion());
                    break;
                case "digest":
                    RunAppWithFile(body, "/digest:", "digest", 240);
                    break;
                case "racprobe":
                    RunAppWithFile(body, "/racprobe:", "racprobe", 180);
                    break;
                case "selftest":
                    RunAppWithFile(body, "/selftest /selftestout:", "selftest", 180);
                    break;
                case "navtest":
                    RunNavtest(body);
                    break;
                case "netstat":
                    CaptureCommand(body, "/c netstat -an | findstr \":1540 :1541 :1545\"");
                    break;
                case "services":
                    CaptureCommand(body, "/c sc query state= all | findstr /i \"1C 1cv8 Service\"");
                    break;
                case "copy-log":
                    CopyAppLog(body);
                    break;
                case "start-ras":
                    StartRas(body);
                    break;
                case "cleanup":
                    CleanupServer(body);
                    break;
                default:
                    body.Add("НЕИЗВЕСТНАЯ КОМАНДА: " + command);
                    body.Add("Разрешено: noop, version, digest, racprobe, selftest, navtest, netstat, services, copy-log, start-ras, cleanup");
                    break;
            }
        }
        catch (Exception ex)
        {
            body.Add("СБОЙ: " + ex.GetType().Name + ": " + ex.Message);
        }

        try { CopyAppLogTo(body, false); } catch { }

        string resultFile = Path.Combine(_drop, "result-" + _name + "-" + id + ".txt");
        try { File.WriteAllText(resultFile, string.Join("\r\n", body.ToArray()) + "\r\n", new UTF8Encoding(true)); }
        catch (Exception ex) { Log("не смог записать результат: " + ex.Message); }

        try { File.Move(taskFile, Path.Combine(_done, id + ".task")); } catch { }
        Log("готово " + id + " → " + resultFile);
    }

    /// <summary>Берём самый свежий exe из build\ и dist\ (бывает, что build\ занят чужим окном).</summary>
    private static string AppPath()
    {
        string a = Path.Combine(_root, @"build\1C-License-Inspector.exe");
        string b = Path.Combine(_root, @"dist\1C-License-Inspector.exe");
        bool hasA = false, hasB = false;
        DateTime ta = DateTime.MinValue, tb = DateTime.MinValue;
        try { if (File.Exists(a)) { hasA = true; ta = File.GetLastWriteTime(a); } } catch { }
        try { if (File.Exists(b)) { hasB = true; tb = File.GetLastWriteTime(b); } } catch { }
        if (hasA && hasB) return ta >= tb ? a : b;
        if (hasA) return a;
        if (hasB) return b;
        return a;
    }
    private static string AppVersion()
    {
        try
        {
            string appExe = AppPath();
            if (!File.Exists(appExe)) return "нет файла сборки";
            return FileVersionInfo.GetVersionInfo(appExe).FileVersion;
        }
        catch { return ""; }
    }

    /// <summary>
    /// Локальная копия приложения. Имя содержит версию: при обновлении файл называется иначе,
    /// и Windows (2016 и новее) не показывает старую иконку из своего кэша — он привязан к пути.
    /// Раньше копия всегда называлась 1cli.exe, и на 2016 в проводнике оставалась старая иконка,
    /// хотя внутри уже была новая сборка (28.11.2026).
    /// </summary>
    private static string LocalAppPath()
    {
        string name = "1cli.exe";
        try
        {
            string appExe = AppPath();
            if (File.Exists(appExe))
            {
                string ver = FileVersionInfo.GetVersionInfo(appExe).FileVersion;   // напр. «1.6.2.0»
                if (!string.IsNullOrEmpty(ver))
                    name = "1cli-" + ver.Replace('.', '_') + ".exe";
            }
        }
        catch { }
        return Path.Combine(Path.Combine(Path.GetTempPath(), "1c-inspector-worker"), name);
    }

    /// <summary>Снять зависшие экземпляры приложения по префиксу (1cli, 1cli-1_6_2 и т. п.).</summary>
    private static void KillLeftoversPrefix(string prefix)
    {
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                string n = "";
                try { n = p.ProcessName; } catch { }
                if (string.IsNullOrEmpty(n)) continue;
                if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                try { Log("снимаю зависший экземпляр: " + n + " pid=" + p.Id); p.Kill(); } catch { }
            }
        }
        catch { }
    }

    /// <summary>Удалить прошлые копии приложения (1cli*.exe, кроме текущей).</summary>
    private static void CleanOldCopies()
    {
        try
        {
            string dir = Path.GetDirectoryName(_localApp);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (string f in Directory.GetFiles(dir, "1cli*.exe"))
            {
                if (string.Equals(f, _localApp, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(f); } catch { }
            }
        }
        catch { }
    }

    private static bool SyncApp()
    {
        string appExe = AppPath();
        if (!File.Exists(appExe)) return false;
        bool need = true;
        try
        {
            if (File.Exists(_localApp))
            {
                FileInfo a = new FileInfo(appExe);
                FileInfo b = new FileInfo(_localApp);
                if (a.Length == b.Length && a.LastWriteTime == b.LastWriteTime) need = false;
            }
            if (need)
            {
                File.Copy(appExe, _localApp, true);
                Log("обновлена локальная копия сборки");
            }
        }
        catch (Exception ex)
        {
            Log("не удалось обновить сборку: " + ex.Message);
            return false;
        }
        return true;
    }

    // Отчёт пишем в локальный файл (запись в UNC из приложения иногда висит), затем копируем.
    private static void RunAppWithFile(List<string> body, string argPrefix, string kind, int seconds)
    {
        if (!SyncApp()) { body.Add("нет файла сборки: " + AppPath()); return; }
        string tmp = Path.Combine(_work, kind + "-" + DateTime.Now.ToString("HHmmssfff") + ".txt");
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        string args = argPrefix.EndsWith(":") ? argPrefix + "\"" + tmp + "\" /silent" : argPrefix + tmp + " /silent";
        Process p = StartApp(args);
        bool ok = WaitForFile(tmp, seconds);
        try { if (p != null && !p.HasExited) p.Kill(); } catch { }
        if (!ok) { body.Add("ФАЙЛ НЕ ПОЯВИЛСЯ за " + seconds + " с"); return; }

        string dst = Path.Combine(_drop, kind + "-" + _name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        try { File.Copy(tmp, dst, true); body.Add("готово: " + dst + " (" + new FileInfo(dst).Length + " байт)"); }
        catch (Exception ex) { body.Add("не смог скопировать отчёт: " + ex.Message); }
    }

    private static void RunNavtest(List<string> body)
    {
        if (!SyncApp()) { body.Add("нет файла сборки"); return; }
        Process p = StartApp("/navtest /noerrorbox");
        Thread.Sleep(20000);
        try { if (p != null && !p.HasExited) { p.Kill(); body.Add("проверка не завершилась за 20 с (окно в фоне) — см. журнал"); } else body.Add("окно создано и закрыто"); }
        catch { }
    }

    private static void CaptureCommand(List<string> body, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        Process p = Process.Start(psi);
        string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            if (line.Trim().Length > 0) body.Add(line.TrimEnd());
        }
    }

    private static void CopyAppLog(List<string> body)
    {
        string src = AppLogPath();
        if (src.Length == 0) { body.Add("журнал приложения не найден"); return; }
        string dst = Path.Combine(_drop, "applog-" + _name + ".log");
        try { File.Copy(src, dst, true); body.Add("скопирован: " + dst); }
        catch (Exception ex) { body.Add("не смог скопировать журнал: " + ex.Message); }
    }

    private static void CopyAppLogTo(List<string> body, bool report)
    {
        string src = AppLogPath();
        if (src.Length == 0) return;
        try
        {
            string dst = Path.Combine(_drop, "applog-" + _name + ".log");
            File.Copy(src, dst, true);
            string crash = Path.Combine(Path.GetTempPath(), "1c-license-inspector-crash.log");
            if (File.Exists(crash)) File.Copy(crash, Path.Combine(_drop, "crashlog-" + _name + ".log"), true);
            if (report) body.Add("журнал: " + dst);
        }
        catch { }
    }

    private static string AppLogPath()
    {
        string p1 = Path.Combine(Path.GetTempPath(), "1c-license-inspector.log");
        if (File.Exists(p1)) return p1;
        try
        {
            string p2 = Path.Combine(_work, "1c-license-inspector.log");
            if (File.Exists(p2)) return p2;
        }
        catch { }
        return "";
    }

    private static void StartRas(List<string> body)
    {
        string ras = "";
        foreach (string root in new string[] { @"C:\Program Files\1cv8", @"C:\Program Files (x86)\1cv8" })
        {
            if (!Directory.Exists(root)) continue;
            List<string> dirs = new List<string>(Directory.GetDirectories(root));
            dirs.Sort();
            dirs.Reverse();
            foreach (string dir in dirs)
            {
                string probe = Path.Combine(dir, @"bin\ras.exe");
                if (File.Exists(probe)) { ras = probe; break; }
            }
            if (ras.Length > 0) break;
        }
        if (ras.Length == 0) { body.Add("ras.exe не найден"); return; }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(ras, "--port=1545");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = Path.GetDirectoryName(ras);
            Process.Start(psi);
            Thread.Sleep(4000);
            body.Add("запущен: " + ras + " --port=1545");
        }
        catch (Exception ex) { body.Add("не удалось запустить ras: " + ex.Message); }
    }

    /// <summary>
    /// Убрать следы работы с сервера. Сама уборка живёт в приложении (`Cleanup.Run`, 1.8.0) —
    /// здесь только вызываем его с ключом `/cleanup:` и возвращаем отчёт. Так логика одна:
    /// то же самое делает кнопка на странице «Журнал» и ключ `/cleanup` из командной строки.
    /// </summary>
    private static void CleanupServer(List<string> body)
    {
        KillLeftoversPrefix("1cli");
        Thread.Sleep(500);
        RunAppWithFile(body, "/cleanup:", "cleanup", 120);
        body.Add("");
        body.Add("Приложение, платформа 1С, служба ras и службы HASP не тронуты.");
        body.Add("Каталог " + _work + " дочистится сам после закрытия окна обработчика.");
    }

    private static Process StartApp(string args)
    {        ProcessStartInfo psi = new ProcessStartInfo(_localApp, args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WorkingDirectory = _work;
        return Process.Start(psi);
    }

    private static bool WaitForFile(string path, int seconds)
    {
        DateTime deadline = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < deadline)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0) return true;
            Thread.Sleep(500);
        }
        return File.Exists(path);
    }

    private static void KillLeftovers(string name)
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName(name))
            {
                try { Log("снимаю зависший экземпляр: pid=" + p.Id); p.Kill(); } catch { }
            }
        }
        catch { }
    }

    private static void KillDuplicateWorkers()
    {
        try
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("Worker"))
            {
                if (p.Id == me) continue;
                try
                {
                    Log("снимаю лишнее окно обработчика: pid=" + p.Id);
                    p.Kill();
                }
                catch { }
            }
        }
        catch { }
    }

    private static void EnsureDir(string dir)
    {
        try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
    }

    private static void Log(string text)
    {
        try
        {
            File.AppendAllText(_log, DateTime.Now.ToString("HH:mm:ss") + "  " + text + "\r\n", new UTF8Encoding(false));
        }
        catch { }
    }
}
