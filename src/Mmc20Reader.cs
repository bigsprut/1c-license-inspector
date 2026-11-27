using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace OneCLicenseInspector
{
    /// <summary>
    /// Чтение таблицы консоли 1С 8.2 через MMC 2.0.
    /// Сама оснастка 32-битная, поэтому работу делает помощник Mmc20Dump.exe
    /// (лежит рядом с программой или распаковывается из ресурса).
    /// </summary>
    internal static class Mmc20Reader
    {
        public static string EnsureHelper(out string error)
        {
            error = "";
            try
            {
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mmc20Dump.exe");
                if (File.Exists(local)) return local;

                string temp = Path.Combine(Path.GetTempPath(), "Mmc20Dump.exe");
                if (File.Exists(temp)) return temp;

                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mmc20Dump.exe"))
                {
                    if (s == null) { error = "ресурс Mmc20Dump.exe отсутствует в сборке"; return null; }
                    if (File.Exists(temp)) File.Delete(temp);
                    using (FileStream f = File.Create(temp)) s.CopyTo(f);
                }
                return temp;
            }
            catch (Exception ex)
            {
                error = "не удалось распаковать Mmc20Dump.exe: " + ex.Message;
                return null;
            }
        }

        public static string ReadTable(string mscPath, string nodeName, out string error)
        {
            error = "";
            string helperError;
            string helper = EnsureHelper(out helperError);
            if (string.IsNullOrEmpty(helper))
            {
                error = helperError;
                return null;
            }

            string outFile = Path.Combine(Path.GetTempPath(), "mmc20-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                    helper, "\"" + mscPath + "\" \"" + nodeName + "\" \"" + outFile + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi))
                {
                    if (!proc.WaitForExit(30000)) { try { proc.Kill(); } catch { } }
                }

                if (!File.Exists(outFile))
                {
                    error = "помощник не создал таблицу (см. %TEMP%\\mmc20dump.log)";
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(outFile);
                string text = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
                    ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
                    : Encoding.UTF8.GetString(bytes);
                try { File.Delete(outFile); } catch { }
                return text.TrimStart('\uFEFF');
            }
            catch (Exception ex)
            {
                error = "запуск Mmc20Dump: " + ex.Message;
                return null;
            }
            finally
            {
                try { if (File.Exists(outFile)) File.Delete(outFile); } catch { }
            }
        }
    }
}
