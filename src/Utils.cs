using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace OneCLicenseInspector
{
    public static class Fmt
    {
        public static string Bytes(long b)
        {
            if (b < 1024) return b.ToString(CultureInfo.InvariantCulture) + " Б";
            double kb = b / 1024.0;
            if (kb < 1024) return kb.ToString("0.#", CultureInfo.InvariantCulture) + " КБ";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.#", CultureInfo.InvariantCulture) + " МБ";
            return (mb / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " ГБ";
        }

        public static string Date(DateTime d)
        {
            if (d.Year < 1980) return "";
            return d.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        public static string DateShort(DateTime d)
        {
            if (d.Year < 1980) return "";
            return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        public static string Duration(TimeSpan t)
        {
            if (t.TotalDays >= 1) return ((int)t.TotalDays).ToString(CultureInfo.InvariantCulture) + " д " + t.Hours + " ч";
            if (t.TotalHours >= 1) return ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + " ч " + t.Minutes + " мин";
            return Math.Max(0, (int)t.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " с";
        }

        public static string N(int n)
        {
            return n.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
        }
    }

    public static class Paths
    {
        public static string ProgramData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); }
        }

        public static string ProgramFiles
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); }
        }

        public static string ProgramFilesX86
        {
            get
            {
                try
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                }
                catch
                {
                    return ProgramFiles;
                }
            }
        }

        public static string SystemDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.System); }
        }

        public static string WindowsDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
        }
    }

    public static class Reg
    {
        public static string Str(RegistryKey key, string name)
        {
            if (key == null) return "";
            try
            {
                object v = key.GetValue(name);
                if (v == null) return "";
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            catch { return ""; }
        }

        public static int Dword(RegistryKey key, string name, int def)
        {
            if (key == null) return def;
            try
            {
                object v = key.GetValue(name);
                if (v == null) return def;
                return Convert.ToInt32(v, CultureInfo.InvariantCulture);
            }
            catch { return def; }
        }

        public static RegistryKey Open(string path, bool writable)
        {
            try { return Registry.LocalMachine.OpenSubKey(path, writable); }
            catch { return null; }
        }

        public static RegistryKey OpenBase(RegistryHive hive, string path)
        {
            try
            {
                RegistryKey b = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                return b.OpenSubKey(path, false);
            }
            catch { return null; }
        }

        public static List<string> SubKeys(RegistryKey key)
        {
            List<string> list = new List<string>();
            if (key == null) return list;
            try
            {
                foreach (string s in key.GetSubKeyNames()) list.Add(s);
            }
            catch { }
            return list;
        }
    }

    public static class Files
    {
        public static string Version(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                string v = vi.FileVersion;
                if (string.IsNullOrEmpty(v)) v = vi.ProductVersion;
                return (v ?? "").Trim();
            }
            catch { return ""; }
        }

        public static string Description(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                return (vi.FileDescription ?? "").Trim();
            }
            catch { return ""; }
        }

        public static string Company(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                return (vi.CompanyName ?? "").Trim();
            }
            catch { return ""; }
        }

        /// <summary>Возвращает "x86", "x64" или "" по PE-заголовку.</summary>
        public static string MachineType(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 0x40) return "";
                    if (br.ReadUInt16() != 0x5A4D) return "";
                    fs.Position = 0x3C;
                    int peOff = br.ReadInt32();
                    if (peOff <= 0 || peOff + 6 > fs.Length) return "";
                    fs.Position = peOff;
                    if (br.ReadUInt32() != 0x00004550) return "";
                    ushort machine = br.ReadUInt16();
                    if (machine == 0x014c) return "x86";
                    if (machine == 0x8664) return "x64";
                    if (machine == 0x0200) return "IA64";
                    return "0x" + machine.ToString("X4");
                }
            }
            catch { return ""; }
        }

        public static string Sha1(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA1 sha = SHA1.Create())
                {
                    byte[] h = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(h.Length * 2);
                    foreach (byte b in h) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        public static string HexPreview(string path, int count)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] buf = new byte[Math.Min(count, (int)Math.Min(fs.Length, int.MaxValue))];
                    int read = fs.Read(buf, 0, buf.Length);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < read; i++)
                    {
                        if (i > 0 && i % 16 == 0) sb.AppendLine();
                        else if (i > 0) sb.Append(' ');
                        sb.Append(buf[i].ToString("X2", CultureInfo.InvariantCulture));
                        if (i % 16 == 15)
                        {
                            sb.Append("  ");
                            for (int j = i - 15; j <= i; j++)
                            {
                                char c = (char)buf[j];
                                sb.Append(c >= 32 && c < 127 ? c : '.');
                            }
                        }
                    }
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        public static long Size(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        public static DateTime Modified(string path)
        {
            try { return File.GetLastWriteTime(path); }
            catch { return DateTime.MinValue; }
        }

        public static string ReadTextSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                return File.ReadAllText(path);
            }
            catch { return ""; }
        }
    }

    public static class Http
    {
        public static string Get(string url, int timeoutMs, out int status, out string error)
        {
            status = 0;
            error = "";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Proxy = null;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.KeepAlive = false;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    status = (int)resp.StatusCode;
                    using (Stream rs = resp.GetResponseStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        int n;
                        while ((n = rs.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch (WebException we)
            {
                error = we.Message;
                if (we.Response is HttpWebResponse) status = (int)((HttpWebResponse)we.Response).StatusCode;
                return "";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }
        public static string Post(string url, string body, int timeoutMs, out int status, out string error)
        {
            status = 0;
            error = "";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.Proxy = null;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.KeepAlive = false;
                req.ContentType = "text/xml; charset=utf-8";
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream())
                {
                    s.Write(data, 0, data.Length);
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    status = (int)resp.StatusCode;
                    using (Stream rs = resp.GetResponseStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        int n;
                        while ((n = rs.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch (WebException we)
            {
                error = we.Message;
                if (we.Response is HttpWebResponse)
                {
                    status = (int)((HttpWebResponse)we.Response).StatusCode;
                }
                return "";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return "";
            }
        }
    }

    public static class XmlUtil
    {
        public static string Value(XElement parent, string name)
        {
            if (parent == null) return "";
            XElement e = parent.Element(name);
            return e == null ? "" : (e.Value ?? "").Trim();
        }

        public static IEnumerable<XElement> Items(XElement root, string name)
        {
            if (root == null) return new XElement[0];
            return root.Elements(name);
        }

        public static XDocument Parse(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return null;
            int i = xml.IndexOf('<');
            if (i < 0) return null;
            if (i > 0) xml = xml.Substring(i);
            try { return XDocument.Parse(xml); }
            catch { return null; }
        }

        public static string StatusText(XDocument doc)
        {
            if (doc == null) return "";
            XElement st = doc.Root.Element("admin_status");
            if (st == null) return "";
            return Value(st, "code") + " " + Value(st, "text");
        }

        public static int StatusCode(XDocument doc)
        {
            if (doc == null) return -1;
            XElement st = doc.Root.Element("admin_status");
            if (st == null) return -1;
            int c;
            if (int.TryParse(Value(st, "code"), NumberStyles.Integer, CultureInfo.InvariantCulture, out c)) return c;
            return -1;
        }
    }

    public static class Os
    {
        public static bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static string ArchName()
        {
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
            string wow = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "";
            if (!string.IsNullOrEmpty(wow)) arch = wow;
            if (string.Equals(arch, "AMD64", StringComparison.OrdinalIgnoreCase)) return "x64";
            if (string.Equals(arch, "IA64", StringComparison.OrdinalIgnoreCase)) return "IA64";
            if (string.Equals(arch, "x86", StringComparison.OrdinalIgnoreCase)) return "x86";
            return arch;
        }

        public static string OsBuildText()
        {
            try
            {
                using (RegistryKey k = Reg.Open(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (k == null) return Environment.OSVersion.Version.ToString();
                    string build = Reg.Str(k, "CurrentBuildNumber");
                    string csd = Reg.Str(k, "CSDVersion");
                    string s = Environment.OSVersion.Version.ToString();
                    if (!string.IsNullOrEmpty(build)) s += " (сборка " + build + ")";
                    if (!string.IsNullOrEmpty(csd)) s += ", " + csd;
                    return s;
                }
            }
            catch { return Environment.OSVersion.Version.ToString(); }
        }

        public static string OsCaption()
        {
            string caption = "";
            try
            {
                using (RegistryKey k = Reg.Open(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    caption = Reg.Str(k, "ProductName");
                    string edition = Reg.Str(k, "EditionID");
                    bool already = !string.IsNullOrEmpty(edition) && caption.IndexOf(edition, StringComparison.OrdinalIgnoreCase) >= 0;
                    string[] words = new string[] { "Standard", "Enterprise", "Datacenter", "Web", "Foundation", "Storage", "Essential", "Small Business", "Home", "Professional" };
                    foreach (string w in words)
                    {
                        if (caption.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) already = true;
                    }
                    if (!string.IsNullOrEmpty(edition) && !already)
                    {
                        caption += " " + edition;
                    }
                }
            }
            catch { }
            if (string.IsNullOrEmpty(caption)) caption = Environment.OSVersion.VersionString;
            try
            {
                caption = caption.Replace("Microsoft Windows ", "Windows ").Trim();
            }
            catch { }
            return caption;
        }

        public static string DotNetText()
        {
            try
            {
                using (RegistryKey k = Reg.Open(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", false))
                {
                    if (k == null) return "не обнаружен";
                    string rel = Reg.Str(k, "Release");
                    string ver = Reg.Str(k, "Version");
                    if (!string.IsNullOrEmpty(ver)) return ver;
                    if (!string.IsNullOrEmpty(rel)) return "4.0 (" + rel + ")";
                    return "4.0";
                }
            }
            catch { return "не обнаружен"; }
        }
    }

    internal static class Diag
    {
        public static string LogPath
        {
            get
            {
                try
                {
                    string local = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "1c-license-inspector.log");
                    if (local.Length > 20) return local;
                }
                catch { }
                return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "1c-license-inspector.log");
            }
        }

        public static string Tail(int maxChars)
        {
            try
            {
                string s = System.IO.File.ReadAllText(LogPath);
                return s.Length <= maxChars ? s : s.Substring(s.Length - maxChars);
            }
            catch { return ""; }
        }

        public static void Reset()
        {
            try
            {
                System.IO.File.WriteAllText(LogPath, "=== start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\r\n");
                return;
            }
            catch { }
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "1c-license-inspector.log"),
                    "=== start(fallback) " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\r\n");
            }
            catch { }
        }

        public static void Log(string text)
        {
            try { System.IO.File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + "\r\n"); }
            catch { }
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "1c-license-inspector.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + "\r\n"); }
            catch { }
        }
    }
}
