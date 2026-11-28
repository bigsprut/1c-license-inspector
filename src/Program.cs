using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OneCLicenseInspector
{
    internal static class Scanner
    {
        public static ScanResult Run(Action<string> progress)
        {
            Diag.Reset();
            Diag.Log("scan: begin");
            ScanResult r = new ScanResult();
            r.Started = DateTime.Now;
            Report1(progress, "Сбор сведений о компьютере…");
            Diag.Log("scan: system");
            SafeRun(r, "Система", delegate { CollectSystem.Run(r); });
            Report1(progress, "Поиск платформ 1С…");
            Diag.Log("scan: platforms");
            SafeRun(r, "Платформы", delegate { CollectPlatforms.Run(r); });
            Report1(progress, "Поиск файлов программных лицензий…");
            Diag.Log("scan: licenses");
            SafeRun(r, "Лицензии", delegate { CollectLicenses.Run(r); });
            Report1(progress, "Опрос менеджера лицензий Sentinel LDK…");
            Diag.Log("scan: sentinel");
            SafeRun(r, "Sentinel", delegate { CollectSentinel.Run(r); });
            Report1(progress, "Проверка HASP4 и служб…");
            Diag.Log("scan: hasp4");
            SafeRun(r, "HASP4", delegate { CollectHasp4.Run(r); });
            Report1(progress, "Сбор сведений об использовании…");
            Diag.Log("scan: usage");
            SafeRun(r, "Использование", delegate { CollectUsage.Run(r); });
            Report1(progress, "Опрос кластера 1С…");
            SafeRun(r, "Кластер", delegate { CollectCluster.Run(r, Program.RasEndpoint, Program.ClusterUser, Program.ClusterPwd); });
            try { AutoConsole82.TryAutoCollect(r); } catch { }

            r.Finished = DateTime.Now;
            CrossCheck(r);
            AutoDrop.Write(r);
            Diag.Log("scan: done");
            r.MachineInfo = r.System.ComputerName + " · " + r.System.OsCaption + " " + r.System.OsArch;
            Report1(progress, "Готово");
            return r;
        }

        private static void SafeRun(ScanResult r, string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                r.AddLog(LogLevel.Error, name, "Сбой сбора: " + ex.GetType().Name + ": " + ex.Message);
                r.AddWarning(LogLevel.Warn, "Ошибка в разделе «" + name + "»", ex.Message);
            }
        }

        private static void Report1(Action<string> progress, string text)
        {
            if (progress != null)
            {
                try { progress(text); }
                catch { }
            }
        }

        private static void CrossCheck(ScanResult r)
        {
            bool has8 = false;
            bool has77 = false;
            foreach (PlatformInfo p in r.Platforms)
            {
                if (p.Family.IndexOf("7.7", StringComparison.Ordinal) >= 0) has77 = true;
                else has8 = true;
            }

            if (has77 && !r.Hasp4.ServiceInstalled && !r.Hasp4.PortListening)
            {
                r.AddWarning(LogLevel.Info, "Для 1С 7.7 не найден менеджер лицензий",
                    "Платформа 1С 7.7 установлена, но ни служба HASP License Manager (nhsrvice), ни порт 475 не обнаружены. " +
                    "Если используется локальный USB-ключ, проверьте, что он вставлен и установлен драйвер HASP.");
            }

            if (has8 && r.Sentinel.Keys.Count == 0 && r.LicenseFiles.Count == 0 && !r.Hasp4.PortListening)
            {
                r.AddWarning(LogLevel.Warn, "Не найдено ни одной лицензии 1С",
                    "Установлена платформа 1С:Предприятие 8, но не обнаружены ни аппаратные ключи, ни файлы программных лицензий. " +
                    "Возможные причины: ключ не вставлен, службы защиты остановлены, лицензии выдаются по сети с другого сервера, " +
                    "либо используется базовая версия с ограничениями.");
            }

            if (r.Sentinel.Available && r.Sentinel.Features.Count > 0)
            {
                int expired = 0;
                foreach (SentinelFeature f in r.Sentinel.Features)
                {
                    if (!string.IsNullOrEmpty(f.Unusable) && f.Unusable != "0") expired++;
                }
                if (expired > 0)
                {
                    r.AddWarning(LogLevel.Warn, "Есть недоступные компоненты",
                        "Компонентов с признаком недоступности: " + expired + ". Возможно истечение срока или блокировка.");
                }
            }
        }
    }

    internal static class Program
    {
        public const string Version = "1.5.34";
        public static string LastReportPath = "";
        public static string RacOverride = "";
        public static string HaspInfoOverride = "";
        public static string RasEndpoint = "";
        public static string ClusterUser = "";
        public static string ClusterPwd = "";
        public static int InitialUsageTab = 0;
        public static int OnlyPage = -1;
        public static bool NoPages = false;
        public static int BareLevel = 4;
        public static bool NoIcon;
        public static bool NoKeyPreview;
        public static bool NoMinSize;
        public static bool NoCenter;
        public static bool NoFont;
        public static bool PlainForm;
        public static bool NavTest;
        public static bool DebugLog;

        [STAThread]
        private static int Main(string[] args)
        {
            Boot("enter: " + string.Join(" ", args));
            try
            {
                Application.EnableVisualStyles();
                Boot("styles ok");
                Application.SetCompatibleTextRenderingDefault(false);

                RacOverride = Arg(args, "/rac");
                HaspInfoOverride = Arg(args, "/haspinfo");
                RasEndpoint = Arg(args, "/ras");
                ClusterUser = Arg(args, "/clusteruser");
                ClusterPwd = Arg(args, "/clusterpwd");
                int usageTab = 0;
                string usageTabText = Arg(args, "/usagetab");
                if (!string.IsNullOrEmpty(usageTabText)) int.TryParse(usageTabText, out usageTab);
                InitialUsageTab = usageTab;
                int onlyPage = -1;
                string onlyText = Arg(args, "/only");
                if (!string.IsNullOrEmpty(onlyText)) int.TryParse(onlyText, out onlyPage);
                OnlyPage = onlyPage;
                NoPages = Has(args, "/nopages");
                int bare = 4;
                string bareText = Arg(args, "/bare");
                if (!string.IsNullOrEmpty(bareText)) int.TryParse(bareText, out bare);
                BareLevel = bare;
                NoIcon = Has(args, "/noicon");
                NoKeyPreview = Has(args, "/nokeyprev");
                NoMinSize = Has(args, "/nominsize");
                NoCenter = Has(args, "/nocenterscreen");
                NoFont = Has(args, "/nofont");
                PlainForm = Has(args, "/plainform");
                NavTest = Has(args, "/navtest");
                DebugLog = Has(args, "/debug");
                Boot("text rendering ok");

                string mmc20 = Arg(args, "/mmc20");
                if (!string.IsNullOrEmpty(mmc20))
                {
                    string node = Arg(args, "/mmcnode");
                    if (string.IsNullOrEmpty(node)) node = "Рабочие процессы";
                    string mmcError;
                    string table = Mmc20Reader.ReadTable(mmc20, node, out mmcError);
                    string mmcOut = Arg(args, "/mmcout");
                    string dump = table != null ? table : ("ERROR: " + mmcError);
                    if (!string.IsNullOrEmpty(mmcOut)) File.WriteAllText(mmcOut, dump, Encoding.UTF8);
                    return 0;
                }
                string imp82 = Arg(args, "/imp82");
                if (!string.IsNullOrEmpty(imp82))
                {
                    string txt = File.ReadAllText(imp82, Encoding.UTF8);
                    string summary = Console82Import.LooksLikeProcesses(txt)
                        ? Console82Import.ProcessSummary(Console82Import.ParseProcesses(txt))
                        : Console82Import.Summary(Console82Import.Parse(txt));
                    string outPath = Arg(args, "/impout");
                    if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, summary, Encoding.UTF8);
                    else Console.WriteLine(summary);
                    return 0;
                }
                string licFile = Arg(args, "/lic");
                if (!string.IsNullOrEmpty(licFile))
                {
                    LicenseFileInfo li = CollectLicenses.ParseFile(licFile);
                    string dump = "pin=" + li.Pin + Environment.NewLine +
                                  "product=" + li.Product + Environment.NewLine +
                                  "type=" + li.LicenseType + Environment.NewLine +
                                  "users=" + li.Users + Environment.NewLine +
                                  "expires=" + li.Expires + Environment.NewLine +
                                  "bound=" + li.BoundTo;
                    string outFile = Arg(args, "/licout");
                    if (!string.IsNullOrEmpty(outFile)) File.WriteAllText(outFile, dump, Encoding.UTF8);
                    else Console.WriteLine(dump);
                    return 0;
                }
                string report = Arg(args, "/report");
                string csv = Arg(args, "/csv");
                string digest = Arg(args, "/digest");
                bool silent = Has(args, "/silent") || Has(args, "/quiet");
                Boot("args: report=" + report + " csv=" + csv + " silent=" + silent);

                if (Has(args, "/racprobe") || Arg(args, "/racprobe").Length > 0)
                {
                    ScanResult pr = new ScanResult();
                    pr.Started = DateTime.Now;
                    try { CollectSystem.Run(pr); } catch { }
                    try { CollectPlatforms.Run(pr); } catch { }
                    try { CollectCluster.Run(pr, RasEndpoint, ClusterUser, ClusterPwd); } catch { }
                    string probePath = Arg(args, "/racprobe");
                    string text = RacProbe.Build(pr, RasEndpoint, ClusterUser, ClusterPwd);
                    if (string.IsNullOrEmpty(probePath)) probePath = RacProbe.DefaultPath();
                    WriteText(probePath, text, true);
                    try
                    {
                        string dir = Path.GetDirectoryName(probePath);
                        if (!string.IsNullOrEmpty(dir)) WriteText(Path.Combine(dir, "latest-racprobe.txt"), text, true);
                    }
                    catch { }
                    if (!silent) Console.WriteLine("Диагностика rac: " + probePath);
                    return 0;
                }

                if (!string.IsNullOrEmpty(digest))
                {
                    ScanResult dr = Scanner.Run(null);
                    WriteText(digest, Report.BuildDigest(dr, Diag.Tail(60000)), true);
                    return 0;
                }

                if (!string.IsNullOrEmpty(report) || !string.IsNullOrEmpty(csv))
                {
                    ScanResult r = Scanner.Run(null);
                    Boot("scan done");
                    if (!string.IsNullOrEmpty(report))
                    {
                        WriteText(report, Report.BuildHtml(r), true);
                        LastReportPath = report;
                    }
                    if (!string.IsNullOrEmpty(csv))
                    {
                        WriteText(csv, Report.BuildCsv(r), true);
                    }
                    Boot("report written");
                    return 0;
                }

                if (silent) return 0;

                Boot("creating form");
                string shot = Arg(args, "/shot");
                int page = 0;
                string pageArg = Arg(args, "/page");
                int.TryParse(pageArg, out page);
                if (!string.IsNullOrEmpty(shot))
                {
                    ScanResult sr = Scanner.Run(null);
                MaskForDemo(sr);
                        SanitizeForDemo(sr);                    using (Ui.MainForm f = new Ui.MainForm(page))
                    {
                        int sw = 1220, sh = 780;
                        string sizeArg = Arg(args, "/shotsize");
                        if (!string.IsNullOrEmpty(sizeArg))
                        {
                            string[] parts = sizeArg.Split('x');
                            if (parts.Length == 2)
                            {
                                int.TryParse(parts[0], out sw);
                                int.TryParse(parts[1], out sh);
                            }
                        }
                        if (sw < 800) sw = 1220;
                        if (sh < 600) sh = 780;
                        f.Size = new System.Drawing.Size(sw, sh);
                        f.AutoScan = false;
                        bool interactive = System.Diagnostics.Process.GetCurrentProcess().SessionId != 0;
                        f.PerformLayout();
                        if (interactive)
                        {
                            f.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                            f.Location = new System.Drawing.Point(-5000, -5000);
                            f.ShowInTaskbar = false;
                            f.Show();
                        }
                        else
                        {
                            f.CreateControl();
                            MakeHandles(f);
                        }
                        f.ApplyForScreenshot(sr);
                        f.ApplyDemoHeader();
                        f.PerformLayout();
                        Application.DoEvents();
                        if (interactive) System.Threading.Thread.Sleep(250);
                        using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                            bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                            try { System.IO.File.Delete(shot + ".txt"); } catch { }
                            DumpControls(f, shot + ".txt", 0);
                        }
                    }
                    return 0;
                }
                Diag.Reset();
                if (PlainForm)
                {
                    System.Windows.Forms.Form plain = new System.Windows.Forms.Form();
                    plain.Text = "plain";
                    plain.ClientSize = new System.Drawing.Size(800, 600);
                    plain.Shown += delegate { Diag.Log("plain: shown"); plain.Close(); };
                    Diag.Log("plain: created");
                    System.Windows.Forms.Application.Run(plain);
                    Diag.Log("plain: closed");
                    return 0;
                }
                Diag.Log("main: creating form");
                Ui.MainForm form = new Ui.MainForm(page);
                Diag.Log("main: form created");
                Application.Run(form);
                Diag.Log("main: form closed");
                Boot("form closed");
                return 0;
            }
            catch (Exception ex)
            {
                Boot("EXCEPTION: " + ex);
                try
                {
                    string log = Path.Combine(Path.GetTempPath(), "1c-license-inspector-crash.log");
                    File.AppendAllText(log, DateTime.Now.ToString("s") + " " + ex + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
                if (!Has(args, "/silent")) MessageBox.Show(ex.Message, "Инспектор лицензий 1С", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        public static void Boot(string text)
        {
            if (!DebugLog) return;
            string[] paths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.log"),
                Path.Combine(Path.GetTempPath(), "1c-license-inspector-boot.log")
            };
            foreach (string p in paths)
            {
                try
                {
                    File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine);
                    return;
                }
                catch { }
            }
        }

        private static void SanitizeForDemo(ScanResult r)
        {
            string pc = r.System.ComputerName;
            string os = r.System.OsCaption;
            string user = r.System.UserName;
            string domain = r.System.Domain;

            r.System.ComputerName = "COMPUTER";
            r.System.OsCaption = "Windows Server";
            r.System.UserName = "user";
            r.System.Domain = "WORKGROUP";
            r.System.Processor = "Intel(R) Xeon(R) CPU";
            r.System.Model = "";
            r.System.Manufacturer = "";
            r.MachineInfo = "COMPUTER · Windows Server · x64";

            foreach (LogEntry e in r.Log)
            {
                if (string.IsNullOrEmpty(e.Text)) continue;
                if (!string.IsNullOrEmpty(pc)) e.Text = e.Text.Replace(pc, "COMPUTER");
                if (!string.IsNullOrEmpty(os)) e.Text = e.Text.Replace(os, "Windows Server");
                if (!string.IsNullOrEmpty(user)) e.Text = e.Text.Replace(user, "user");
                if (!string.IsNullOrEmpty(domain)) e.Text = e.Text.Replace(domain, "WORKGROUP");
            }
        }
        private static void DumpControls(System.Windows.Forms.Control c, string path, int depth)
        {
            try
            {
                string line = new string(' ', depth * 2) + c.GetType().Name + " " + c.Bounds + (c.Visible ? "" : " [hidden]");
                System.IO.File.AppendAllText(path, line + Environment.NewLine);
                if (depth < 6)
                {
                    foreach (System.Windows.Forms.Control child in c.Controls) DumpControls(child, path, depth + 1);
                }
            }
            catch { }
        }
        private static void MakeHandles(System.Windows.Forms.Control c)
        {
            try
            {
                c.CreateControl();
                foreach (System.Windows.Forms.Control child in c.Controls) MakeHandles(child);
            }
            catch { }
        }
        private static void MaskForDemo(ScanResult r)
        {
            foreach (SentinelKey k in r.Sentinel.Keys)
            {
                k.Id = MaskId(k.Id);
                k.Name = string.IsNullOrEmpty(k.TypeName) ? "аппаратный ключ (демо)" : k.TypeName + " (демо)";
                k.HostName = "DEMO-PC";
            }
            foreach (SentinelFeature f in r.Sentinel.Features) f.KeyId = MaskId(f.KeyId);
            if (!string.IsNullOrEmpty(r.JavaPath)) r.JavaPath = @"C:\Program Files\Java\jre\bin\java.exe";
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                lf.Pin = MaskId(lf.Pin);
                lf.BoundTo = "DEMO-PC";
            }
            if (r.Hasp4.UsbKeys.Count > 0)
            {
                r.Hasp4.UsbKeys.Clear();
                r.Hasp4.UsbKeys.Add("Sentinel HASP Key (демо)");
            }
        }

        private static string MaskId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "****";
            return id.Length > 4 ? id.Substring(0, 4) + "-****" : "****";
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)) return a.Substring(name.Length + 1).Trim('"');
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("/", StringComparison.Ordinal)) return args[i + 1].Trim('"');
                    return "";
                }
            }
            return "";
        }

        private static bool Has(string[] args, string name)
        {
            foreach (string a in args)
            {
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void WriteText(string path, string text, bool utf8Bom)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Encoding enc = utf8Bom ? new UTF8Encoding(true) : new UTF8Encoding(false);
            File.WriteAllText(path, text, enc);
        }
    }
}

