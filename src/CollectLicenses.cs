using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace OneCLicenseInspector
{
    internal static class CollectLicenses
    {
        public static void Run(ScanResult r)
        {
            List<string> storages = BuildStorages();
            foreach (string dir in storages)
            {
                if (!Directory.Exists(dir)) continue;
                r.LicenseStorages.Add(dir);
                List<string> files = new List<string>();
                try
                {
                    files.AddRange(Directory.GetFiles(dir, "*.lic", SearchOption.TopDirectoryOnly));
                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        try { files.AddRange(Directory.GetFiles(sub, "*.lic", SearchOption.AllDirectories)); }
                        catch { }
                    }
                }
                catch { }

                foreach (string f in files)
                {
                    LicenseFileInfo info = ReadLicenseFile(f);
                    if (info == null) continue;
                    bool dup = false;
                    foreach (LicenseFileInfo x in r.LicenseFiles)
                    {
                        if (string.Equals(x.FullPath, info.FullPath, StringComparison.OrdinalIgnoreCase) ||
                            (string.Equals(x.FileName, info.FileName, StringComparison.OrdinalIgnoreCase) && x.Size == info.Size && x.Sha1 == info.Sha1)) { dup = true; break; }
                    }
                    if (!dup) r.LicenseFiles.Add(info);
                }
            }

            r.LicenseFiles.Sort(delegate (LicenseFileInfo a, LicenseFileInfo b)
            {
                return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            });

            r.HostId = ReadHostId();
            r.LicenseRing = FindRing(r.Platforms);
            r.JavaPath = FindJava();
            if (r.LicenseFiles.Count == 0) BroadSearch(r);
            if (!string.IsNullOrEmpty(r.LicenseRing))
            {
                r.LicenseRingOutput = RunRing(r.LicenseRing, r.LicenseStorages);
                r.AddLog(LogLevel.Info, "Лицензии", "Утилита ring: " + r.LicenseRing);
            }

            r.AddLog(LogLevel.Info, "Лицензии", "Каталогов лицензий: " + r.LicenseStorages.Count + ", файлов .lic: " + r.LicenseFiles.Count);

            if (r.LicenseFiles.Count == 0)
            {
                bool has8 = false;
                foreach (PlatformInfo p in r.Platforms) if (p.Family.IndexOf("8", StringComparison.Ordinal) >= 0) has8 = true;
                if (has8)
                {
                    r.AddWarning(LogLevel.Info, "Программные лицензии не найдены",
                        "Файлы программных лицензий 1С (*.lic) не обнаружены ни в одном из стандартных каталогов. " +
                        "Вероятно, используются аппаратные ключи защиты HASP, либо лицензии не активированы.");
                }
            }
        }

        /// <summary>Если в стандартных каталогах пусто — ищем *.lic по системе (не глубже 4 уровней).</summary>
        private static void BroadSearch(ScanResult r)
        {
            List<string> roots = new List<string>();
            roots.Add(Paths.ProgramData);
            roots.Add(Paths.WindowsDir);
            try
            {
                string drive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:") + "\\";
                foreach (string baseDir in new string[] { "Users", "Documents and Settings" })
                {
                    string root = Path.Combine(drive, baseDir);
                    if (!Directory.Exists(root)) continue;
                    foreach (string user in Directory.GetDirectories(root)) roots.Add(user);
                }
            }
            catch { }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                WalkForLicenses(root, 0, r, seen);
                if (r.LicenseFiles.Count > 0) break;
            }
            if (r.LicenseFiles.Count > 0)
            {
                r.AddLog(LogLevel.Info, "Лицензии", "Файлы *.lic найдены расширенным поиском: " + r.LicenseFiles.Count);
            }
        }

        private static void WalkForLicenses(string dir, int depth, ScanResult r, HashSet<string> seen)
        {
            if (depth > 4) return;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.lic"))
                {
                    string full = Path.GetFullPath(f);
                    if (!seen.Add(full)) continue;
                    r.LicenseFiles.Add(ReadLicenseFile(full));
                    AddUniquePath(r.LicenseStorages, Path.GetDirectoryName(full));
                }
                foreach (string sub in Directory.GetDirectories(dir)) WalkForLicenses(sub, depth + 1, r, seen);
            }
            catch { }
        }
        private static List<string> BuildStorages()
        {
            List<string> list = new List<string>();
            AddUniquePath(list, Path.Combine(Paths.ProgramData, "1C", "licenses"));
            AddUniquePath(list, Path.Combine(Paths.ProgramData, "1C", "1CE"));
            AddUniquePath(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "1C", "licenses"));
            AddUniquePath(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "1C", "licenses"));
            AddUniquePath(list, Path.Combine(Paths.WindowsDir, "System32", "1C", "licenses"));

            try
            {
                string allUsers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "");
                AddUniquePath(list, Path.Combine(allUsers, "1C", "licenses"));
            }
            catch { }

            // Профиль службы 1С (LocalSystem/NetworkService): там же 1С хранит
            // локальные лицензии, активированные сервером.
            AddSystemProfileStorages(list);

            // Профили всех пользователей: лицензии, активированные в клиенте.
            AddAllUserStorages(list);
            return list;
        }

        private static void AddSystemProfileStorages(List<string> list)
        {
            string[] roots = new string[]
            {
                Path.Combine(Paths.WindowsDir, "System32", "config", "systemprofile", "AppData"),
                Path.Combine(Paths.WindowsDir, "SysWOW64", "config", "systemprofile", "AppData")
            };
            foreach (string appData in roots)
            {
                foreach (string sub in new string[] { "Local", "Roaming", "LocalLow" })
                {
                    AddUniquePath(list, Path.Combine(appData, sub, "1C", "licenses"));
                }
            }
        }

        private static void AddAllUserStorages(List<string> list)
        {
            string[] userRoots = new string[]
            {
                Path.Combine((Environment.GetEnvironmentVariable("SystemDrive") ?? "C:") + "\\", "Users"),
                Path.Combine((Environment.GetEnvironmentVariable("SystemDrive") ?? "C:") + "\\", "Documents and Settings")
            };
            foreach (string root in userRoots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string user in Directory.GetDirectories(root))
                    {
                        AddUniquePath(list, Path.Combine(user, "AppData", "Local", "1C", "licenses"));
                        AddUniquePath(list, Path.Combine(user, "AppData", "Roaming", "1C", "licenses"));
                        // Windows Server 2003 / XP
                        AddUniquePath(list, Path.Combine(user, "Local Settings", "Application Data", "1C", "licenses"));
                        AddUniquePath(list, Path.Combine(user, "Application Data", "1C", "licenses"));
                    }
                }
                catch { }
            }
        }

        private static bool IsJunctionAlias(string path)
        {
            try
            {
                string p = path.Replace('/', '\\');
                return p.IndexOf(@"\Users\All Users\", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.IndexOf(@"\Users\Все пользователи\", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.IndexOf(@"\Documents and Settings\All Users\", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.IndexOf(@"\Documents and Settings\Все пользователи\", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool IsLic(string path)
        {
            try { return string.Equals(Path.GetExtension(path), ".lic", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static void AddUniquePath(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string norm = path.TrimEnd('\\');
            foreach (string x in list)
            {
                if (string.Equals(x.TrimEnd('\\'), norm, StringComparison.OrdinalIgnoreCase)) return;
            }
            list.Add(norm);
        }

        /// <summary>В *.lic после base64-блока идут открытые поля: рег. номер, продукт, места, привязка.</summary>
        private static void ParseLicenseText(LicenseFileInfo info)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(info.FullPath);
                string text = DecodeText(bytes);
                if (string.IsNullOrEmpty(text)) return;
                info.Pin = Field(text, "Регистрационный номер");
                info.LicenseType = Field(text, "Тип лицензии");
                info.Product = Field(text, "Наименование продукта");
                info.Expires = Field(text, "Срок действия");
                info.Users = Field(text, "Количество пользователей");
                info.BoundTo = Field(text, "Привязка");
                int n;
                if (int.TryParse(info.Users, out n)) info.UsersNum = n;
            }
            catch { }
        }

        private static string DecodeText(byte[] bytes)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes); } catch { }
            try { return Encoding.GetEncoding(1251).GetString(bytes); } catch { }
            return Encoding.Default.GetString(bytes);
        }

        private static string Field(string text, string label)
        {
            int i = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            int colon = text.IndexOf(':', i);
            if (colon < 0) return "";
            int end = text.IndexOf('\n', colon);
            string v = end < 0 ? text.Substring(colon + 1) : text.Substring(colon + 1, end - colon - 1);
            return v.Trim().Trim('\r', ' ');
        }
        public static string ReadText(string path)
        {
            try { return DecodeText(File.ReadAllBytes(path)); } catch { return ""; }
        }

        public static LicenseFileInfo ParseFile(string path)
        {
            return ReadLicenseFile(path);
        }

        private static LicenseFileInfo ReadLicenseFile(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                LicenseFileInfo info = new LicenseFileInfo();
                info.FileName = fi.Name;
                info.Directory = fi.DirectoryName;
                info.FullPath = fi.FullName;
                info.Size = fi.Length;
                info.Modified = fi.LastWriteTime;
                info.Sha1 = Files.Sha1(path);
                info.Activated = DecodeNameDate(fi.Name);
                info.Format = DetectFormat(path);
                info.Preview = Files.HexPreview(path, 64);
                ParseLicenseText(info);
                info.Note = NoteForFormat(info.Format);
                return info;
            }
            catch { return null; }
        }

        private static string DecodeNameDate(string name)
        {
            Match m = Regex.Match(name ?? "", @"^(\d{14})\.lic$", RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            DateTime d;
            if (DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return Fmt.Date(d);
            return "";
        }

        private static string DetectFormat(string path)
        {
            try
            {
                byte[] head = new byte[16];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int n = fs.Read(head, 0, head.Length);
                    if (n < 4) return "пустой";
                }
                if (head[0] == 0x50 && head[1] == 0x4B) return "контейнер ZIP";
                if (head[0] == '<') return "текст / XML";
                bool printable = true;
                for (int i = 0; i < 8; i++)
                {
                    if (head[i] < 9 || (head[i] > 13 && head[i] < 32) || head[i] > 126) { printable = false; break; }
                }
                if (printable) return "текстовый";
                return "двоичный";
            }
            catch { return "?"; }
        }

        private static string NoteForFormat(string format)
        {
            switch (format)
            {
                case "контейнер ZIP":
                    return "Файл-контейнер. Внутренние поля описаны в служебном формате 1С.";
                case "текст / XML":
                    return "Текстовый файл — можно открыть блокнотом.";
                case "текстовый":
                    return "Текстовый файл.";
                case "пустой":
                    return "Файл нулевой длины (повреждён или создан ошибочно).";
                default:
                    return "Двоичный файл лицензии. Описание (PIN, редакция, число мест) читает только сама платформа 1С.";
            }
        }

        private static string ReadHostId()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Paths.ProgramData, "1C", "1cv8", "host_id"),
                Path.Combine(Paths.ProgramData, "1C", "1CE", "host_id"),
                Path.Combine(Paths.ProgramData, "1C", "licenses", "host_id")
            };
            foreach (string p in candidates)
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    long size = Files.Size(p);
                    string hash = Files.Sha1(p);
                    if (hash.Length > 12) hash = hash.Substring(0, 12);
                    return "есть, " + Fmt.Bytes(size) + ", SHA-1 " + hash + " (содержимое скрыто: это ключ привязки лицензий)";
                }
                catch { }
            }
            return "";
        }

        private static string RunRing(string ringExe, List<string> storages)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string dir in storages)
            {
                if (!Directory.Exists(dir)) continue;
                sb.AppendLine("> ring license list --path \"" + dir + "\"");
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    bool bat = ringExe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) || ringExe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
                    if (bat)
                    {
                        psi.FileName = "cmd.exe";
                        psi.Arguments = "/c \"\"" + ringExe + "\" license list --path \"" + dir + "\"\"";
                    }
                    else
                    {
                        psi.FileName = ringExe;
                        psi.Arguments = "license list --path \"" + dir + "\"";
                    }
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.WorkingDirectory = Path.GetDirectoryName(ringExe);
                    using (Process p = Process.Start(psi))
                    {
                        string outp = p.StandardOutput.ReadToEnd();
                        string err = p.StandardError.ReadToEnd();
                        if (!p.WaitForExit(8000))
                        {
                            try { p.Kill(); } catch { }
                            sb.AppendLine(outp);
                            sb.AppendLine("[превышено время ожидания ответа ring]");
                            continue;
                        }
                        if (!string.IsNullOrEmpty(outp)) sb.AppendLine(outp.Trim());
                        if (!string.IsNullOrEmpty(err)) sb.AppendLine("stderr: " + err.Trim());
                        if (string.IsNullOrEmpty(outp) && string.IsNullOrEmpty(err))
                            sb.AppendLine("[ring не вернул данных]");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("[ошибка запуска ring] " + ex.Message);
                }
            }
            return sb.ToString().Trim();
        }
        private static string FindJava()
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathVar.Split(';'))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    string f = Path.Combine(dir.Trim(), "java.exe");
                    if (File.Exists(f)) return f;
                }
                catch { }
            }
            string[] probe = new string[]
            {
                @"C:\Program Files\Java", @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Zulu", @"C:\Program Files\Eclipse Adoptium"
            };
            foreach (string dir in probe)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    string[] found = Directory.GetFiles(dir, "java.exe", SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
                catch { }
            }
            return "";
        }
        private static string FindRing(List<PlatformInfo> platforms)
        {
            foreach (PlatformInfo p in platforms)
            {
                try
                {
                    string ring = Path.Combine(p.BinPath, "ring.exe");
                    if (File.Exists(ring)) return ring;
                }
                catch { }
            }
            return "";
        }
    }
}
