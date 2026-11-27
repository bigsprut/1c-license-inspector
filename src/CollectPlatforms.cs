using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OneCLicenseInspector
{
    internal static class CollectPlatforms
    {
        private class UninstallEntry
        {
            public string Name = "";
            public string Version = "";
            public string Location = "";
            public string InstallDate = "";
            public string Hive = "";
        }

        public static void Run(ScanResult r)
        {
            List<UninstallEntry> uninstall = ReadUninstall();
            Dictionary<string, PlatformInfo> seen = new Dictionary<string, PlatformInfo>(StringComparer.OrdinalIgnoreCase);

            // 1. Установленные экземпляры из реестра Uninstall (наиболее полный список версий).
            foreach (UninstallEntry e in uninstall)
            {
                if (!IsOneCProduct(e.Name)) continue;
                string path = NormalizeDir(e.Location, e.Version, e.Name);
                PlatformInfo p = Build8(path, e.Version, "реестр: " + e.Hive);
                if (p == null) continue;
                p.InstallDate = DecodeInstallDate(e.InstallDate);
                p.Comment = e.Name;
                AddUnique(seen, p);
            }

            // 2. Каталоги платформ на дисках (на случай портативной установки).
            List<string> roots = new List<string>();
            roots.Add(Path.Combine(Paths.ProgramFiles, "1cv8"));
            if (!PathsEqual(Paths.ProgramFilesX86, Paths.ProgramFiles))
                roots.Add(Path.Combine(Paths.ProgramFilesX86, "1cv8"));
            if (!PathsEqual(Paths.ProgramFilesX86, Paths.ProgramFiles))
                roots.Add(Path.Combine(Paths.ProgramFilesX86, "1cv82"));
            roots.Add(Path.Combine(Paths.ProgramFiles, "1cv82"));
            roots.Add(Path.Combine(Paths.ProgramFilesX86, "1cv8fv"));

            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string dir in SafeDirs(root))
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, "common", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(name, "conf", StringComparison.OrdinalIgnoreCase)) continue;
                    string ver = IsVersionName(name) ? name : "";
                    PlatformInfo p = Build8(dir, ver, "каталог");
                    if (p != null) AddUnique(seen, p);
                }
            }

            // 3. Платформы, найденные по InstallLocation без версии в пути.
            foreach (UninstallEntry e in uninstall)
            {
                if (!IsOneCProduct(e.Name)) continue;
                string loc = (e.Location ?? "").Trim().TrimEnd('\\');
                if (loc.Length == 0 || !Directory.Exists(loc)) continue;
                if (HasPlatformBin(loc))
                {
                    PlatformInfo p = Build8(loc, e.Version, "реестр: " + e.Hive);
                    if (p != null)
                    {
                        p.InstallDate = DecodeInstallDate(e.InstallDate);
                        p.Comment = e.Name;
                        AddUnique(seen, p);
                    }
                }
            }

            // 4. 1С:Предприятие 7.7
            AddSeventySeven(r, uninstall, seen);

            foreach (PlatformInfo p in seen.Values)
            {
                r.Platforms.Add(p);
                r.AddLog(LogLevel.Info, "Платформы", p.Family + " " + p.Version + " (" + p.Bitness + ") — " + p.Path);
            }
            r.Platforms.Sort(delegate (PlatformInfo a, PlatformInfo b)
            {
                int c = string.Compare(a.Family, b.Family, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(b.Version, a.Version, StringComparison.OrdinalIgnoreCase);
            });

            if (r.Platforms.Count == 0)
            {
                r.AddWarning(LogLevel.Warn, "Платформа не найдена",
                    "На этом компьютере не обнаружены установленные платформы 1С:Предприятие 8 или 7.7. Лицензии 1С тоже вряд ли используются.");
            }
        }

        private static void AddUnique(Dictionary<string, PlatformInfo> seen, PlatformInfo p)
        {
            if (p == null) return;
            string key = (p.Family + "|" + p.Version + "|" + p.Path).ToLowerInvariant();
            if (!seen.ContainsKey(key)) seen[key] = p;
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals((a ?? "").TrimEnd('\\'), (b ?? "").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOneCProduct(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.IndexOf("1с", StringComparison.Ordinal) >= 0 || n.IndexOf("1c:", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("1с:", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("предприятие", StringComparison.Ordinal) >= 0 && n.IndexOf("1", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("1cv8", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("1cv7", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("1c:enterprise", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string DecodeInstallDate(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length != 8) return "";
            DateTime d;
            if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            return "";
        }

        private static string NormalizeDir(string location, string version, string name)
        {
            string loc = (location ?? "").Trim().TrimEnd('\\');
            if (loc.Length > 0 && Directory.Exists(loc)) return loc;
            loc = Path.Combine(Paths.ProgramFiles, "1cv8", version ?? "");
            if (!string.IsNullOrEmpty(version) && Directory.Exists(loc)) return loc;
            loc = Path.Combine(Paths.ProgramFilesX86, "1cv8", version ?? "");
            if (!string.IsNullOrEmpty(version) && Directory.Exists(loc)) return loc;
            return (location ?? "").Trim().TrimEnd('\\');
        }

        private static bool HasPlatformBin(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                if (File.Exists(Path.Combine(dir, "1cv8.exe"))) return true;
                if (File.Exists(Path.Combine(dir, "bin", "1cv8.exe"))) return true;
                if (File.Exists(Path.Combine(dir, "1cv8c.exe"))) return true;
                if (File.Exists(Path.Combine(dir, "1cv8s.exe"))) return true;
                return false;
            }
            catch { return false; }
        }

        private static IEnumerable<string> SafeDirs(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return new string[0]; }
        }

        private static bool IsVersionName(string name)
        {
            return Regex.IsMatch(name ?? "", @"^\d+\.\d+");
        }

        private static List<UninstallEntry> ReadUninstall()
        {
            List<UninstallEntry> list = new List<UninstallEntry>();
            RegistryHive[] hives = new RegistryHive[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    if (hive == RegistryHive.CurrentUser && view == RegistryView.Registry64) continue;
                    try
                    {
                        using (RegistryKey b = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey un = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false))
                        {
                            if (un == null) continue;
                            foreach (string sub in un.GetSubKeyNames())
                            {
                                using (RegistryKey k = un.OpenSubKey(sub, false))
                                {
                                    if (k == null) continue;
                                    string name = Reg.Str(k, "DisplayName");
                                    if (!IsOneCProduct(name)) continue;
                                    UninstallEntry e = new UninstallEntry();
                                    e.Name = name;
                                    e.Version = Reg.Str(k, "DisplayVersion");
                                    e.Location = Reg.Str(k, "InstallLocation");
                                    e.InstallDate = Reg.Str(k, "InstallDate");
                                    e.Hive = (hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU") + (view == RegistryView.Registry32 ? "\\WOW6432Node" : "");
                                    list.Add(e);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return list;
        }

        private static PlatformInfo Build8(string dir, string version, string source)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            string bin = Path.Combine(dir, "bin");
            if (!Directory.Exists(bin)) bin = dir;

            string exeFull = Path.Combine(bin, "1cv8.exe");
            string exeThin = Path.Combine(bin, "1cv8c.exe");
            string exeSrv = Path.Combine(bin, "1cv8s.exe");
            string exeAgent = Path.Combine(bin, "ragent.exe");

            bool hasFull = File.Exists(exeFull);
            bool hasThin = File.Exists(exeThin);
            bool hasSrv = File.Exists(exeSrv);
            bool hasCluster = File.Exists(exeAgent) || File.Exists(Path.Combine(bin, "rmngr.exe")) || File.Exists(Path.Combine(bin, "rphost.exe"));
            if (!hasFull && !hasThin && !hasSrv && !hasCluster) return null;

            PlatformInfo p = new PlatformInfo();
            p.Family = "1С:Предприятие 8";
            p.Path = dir.TrimEnd('\\');
            p.BinPath = bin;
            p.Source = source;
            p.HasConfigurator = hasFull;
            p.HasThickClient = hasFull;
            p.HasThinClient = hasThin;
            p.HasServer = hasSrv;
            p.HasCluster = hasCluster;

            string fileVersion = Files.Version(hasFull ? exeFull : (hasThin ? exeThin : (hasSrv ? exeSrv : exeAgent)));
            p.Version = !string.IsNullOrEmpty(version) ? version : fileVersion;
            if (string.IsNullOrEmpty(p.Version)) p.Version = fileVersion;
            if (string.IsNullOrEmpty(p.Version)) p.Version = Path.GetFileName(p.Path);

            string probe = hasFull ? exeFull : (hasThin ? exeThin : (hasSrv ? exeSrv : exeAgent));
            p.Bitness = Files.MachineType(probe);
            if (string.IsNullOrEmpty(p.Bitness)) p.Bitness = "x64";

            List<string> comps = new List<string>();
            if (hasFull) comps.Add("толстый клиент");
            if (hasFull) comps.Add("конфигуратор");
            if (hasThin) comps.Add("тонкий клиент");
            if (hasSrv) comps.Add("сервер 1С");
            if (hasCluster) comps.Add("кластер серверов");
            if (File.Exists(Path.Combine(bin, "ibcmd.exe"))) comps.Add("ibcmd");
            if (File.Exists(Path.Combine(bin, "rac.exe"))) comps.Add("rac");
            if (File.Exists(Path.Combine(bin, "ras.exe"))) comps.Add("ras");
            p.Components = string.Join(", ", comps.ToArray());

            if (hasSrv && hasFull) p.Delivery = "Полная поставка";
            else if (hasSrv) p.Delivery = "Сервер 1С";
            else if (hasCluster) p.Delivery = "Кластер серверов";
            else if (hasThin && !hasFull) p.Delivery = "Тонкий клиент";
            else p.Delivery = "Клиент и конфигуратор";

            try
            {
                DateTime created = Directory.GetCreationTime(dir);
                p.InstallDate = Fmt.DateShort(created);
            }
            catch { }

            return p;
        }

        private static void AddSeventySeven(ScanResult r, List<UninstallEntry> uninstall, Dictionary<string, PlatformInfo> seen)
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(Paths.ProgramFilesX86, "1Cv77"));
            candidates.Add(Path.Combine(Paths.ProgramFiles, "1Cv77"));
            candidates.Add(Path.Combine(Paths.ProgramFilesX86, "1Cv7"));
            candidates.Add(Path.Combine(Paths.ProgramFiles, "1Cv7"));

            string regPath = "";
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\1C\1Cv7\7.7", false))
                {
                    if (k != null)
                    {
                        regPath = Reg.Str(k, "Path");
                        if (string.IsNullOrEmpty(regPath)) regPath = Reg.Str(k, "InstallPath");
                    }
                }
            }
            catch { }
            if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath)) candidates.Add(regPath);

            foreach (UninstallEntry e in uninstall)
            {
                if (e.Name.IndexOf("7.7", StringComparison.Ordinal) >= 0 && !string.IsNullOrEmpty(e.Location) && Directory.Exists(e.Location))
                    candidates.Add(e.Location);
            }

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in candidates)
            {
                if (!Directory.Exists(root) || !used.Add(root)) continue;
                string exe = Find77Exe(root);
                if (exe == null) continue;

                PlatformInfo p = new PlatformInfo();
                p.Family = "1С:Предприятие 7.7";
                p.Path = root.TrimEnd('\\');
                p.BinPath = Path.GetDirectoryName(exe);
                p.Source = "каталог";
                p.Bitness = "x86";
                p.Version = Files.Version(exe);
                if (string.IsNullOrEmpty(p.Version)) p.Version = "7.7";
                p.HasThickClient = true;
                p.HasConfigurator = true;
                p.HasServer = File.Exists(Path.Combine(p.BinPath, "1cv7s.exe")) ||
                              File.Exists(Path.Combine(root, "1cv7s.exe"));
                p.Components = p.HasServer ? "клиент, конфигуратор, сервер (SQL-версия)" : "клиент, конфигуратор";
                p.Delivery = p.HasServer ? "Сетевая (SQL)" : "Локальная";

                string comps = Read77Components();
                if (!string.IsNullOrEmpty(comps)) p.Comment = "Компоненты: " + comps;

                try { p.InstallDate = Fmt.DateShort(Directory.GetCreationTime(root)); }
                catch { }

                AddUnique(seen, p);
                
            }
        }

        private static string Find77Exe(string root)
        {
            string[] probe = new string[]
            {
                Path.Combine(root, "1cv7.exe"),
                Path.Combine(root, "BIN", "1cv7.exe"),
                Path.Combine(root, "bin", "1cv7.exe")
            };
            foreach (string p in probe) if (File.Exists(p)) return p;
            try
            {
                string[] found = Directory.GetFiles(root, "1cv7.exe", SearchOption.AllDirectories);
                if (found.Length > 0)
                {
                    Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                    return found[0];
                }
            }
            catch { }
            return null;
        }

        private static string Read77Components()
        {
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\1C\1Cv7\7.7\Components", false))
                {
                    if (k == null) return "";
                    List<string> names = new List<string>();
                    foreach (string v in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(v)) continue;
                        names.Add(v);
                    }
                    return string.Join(", ", names.ToArray());
                }
            }
            catch { return ""; }
        }
    }
}
