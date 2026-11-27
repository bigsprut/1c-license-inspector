using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Сохранение логина/пароля администратора кластера 1С (DPAPI, текущий пользователь).
    /// Файл: %ProgramData%\1C-License-Inspector\cluster.bin
    /// </summary>
    internal static class ClusterCredentials
    {
        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(Paths.ProgramData, "1C-License-Inspector");
                return Path.Combine(dir, "cluster.bin");
            }
        }

        public static void Save(string endpoint, string user, string pwd)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string text = (endpoint ?? "") + "\n" + (user ?? "") + "\n" + (pwd ?? "");
                byte[] plain = Encoding.UTF8.GetBytes(text);
                byte[] enc = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, enc);
            }
            catch { }
        }

        public static bool Load(out string endpoint, out string user, out string pwd)
        {
            endpoint = ""; user = ""; pwd = "";
            try
            {
                if (!File.Exists(FilePath)) return false;
                byte[] enc = File.ReadAllBytes(FilePath);
                byte[] plain = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                string[] parts = Encoding.UTF8.GetString(plain).Split('\n');
                if (parts.Length > 0) endpoint = parts[0];
                if (parts.Length > 1) user = parts[1];
                if (parts.Length > 2) pwd = parts[2];
                return true;
            }
            catch { return false; }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }
    }
}
