using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Lite
{
    /// <summary>
    /// Точка входа краткого приложения «Лицензии 1С» (две вкладки).
    /// Собирается из тех же исходников, что и полный инспектор, но с `/main:LiteProgram`,
    /// поэтому сборщики данных и таблицы у приложений общие.
    /// </summary>
    internal static class LiteProgram
    {
        [STAThread]
        internal static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Program.RacOverride = Arg(args, "/rac");
            Program.RasEndpoint = Arg(args, "/ras");
            Program.ClusterUser = Arg(args, "/clusteruser");
            Program.ClusterPwd = Arg(args, "/clusterpwd");

            string report = Arg(args, "/report");
            string shot = Arg(args, "/shot");
            bool navTest = Has(args, "/navtest");
            bool demo = Has(args, "/demo82") || Has(args, "/demo");
            bool silent = Has(args, "/silent") || Has(args, "/quiet");
            int tab = 0;
            int.TryParse(Arg(args, "/page"), out tab);

            try
            {
                if (!string.IsNullOrEmpty(report))
                {
                    ScanResult r = Scanner.RunQuick(silent ? null : new Action<string>(delegate (string t) { Console.WriteLine(t); }));
                    if (demo) Program.AddDemo82(r);
                    File.WriteAllText(report, Report.BuildHtml(r), new UTF8Encoding(true));
                    if (!silent) Console.WriteLine("Отчёт: " + report);
                    return 0;
                }

                if (!string.IsNullOrEmpty(shot))
                {
                    ScanResult r = Scanner.RunQuick(null);
                    if (demo) Program.AddDemo82(r);
                    using (LiteForm f = new LiteForm(tab))
                    {
                        int w = 1160, h = 760;
                        string size = Arg(args, "/shotsize");
                        if (!string.IsNullOrEmpty(size))
                        {
                            string[] parts = size.Split('x');
                            if (parts.Length == 2) { int.TryParse(parts[0], out w); int.TryParse(parts[1], out h); }
                        }
                        if (w < 900) w = 1160;
                        if (h < 560) h = 760;
                        f.AutoScan = false;
                        f.Size = new Size(w, h);
                        f.CreateControl();
                        f.ApplyResult(r);
                        f.PerformLayout();
                        bool interactive = System.Diagnostics.Process.GetCurrentProcess().SessionId != 0;
                        if (interactive)
                        {
                            f.StartPosition = FormStartPosition.Manual;
                            f.Location = new Point(-5000, -5000);
                            f.ShowInTaskbar = false;
                            f.Show();
                        }
                        f.PerformLayout();
                        Application.DoEvents();
                        if (interactive) System.Threading.Thread.Sleep(250);
                        using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                            bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    return 0;
                }

                if (navTest)
                {
                    using (LiteForm f = new LiteForm(0))
                    {
                        f.AutoScan = false;
                        f.Shown += delegate
                        {
                            try
                            {
                                ScanResult r = Scanner.RunQuick(null);
                                f.ApplyResult(r);
                                for (int i = 0; i < 2; i++)
                                {
                                    f.SelectTabForTest(i);
                                    Application.DoEvents();
                                }
                            }
                            catch (Exception ex) { Diag.Log("lite navtest: " + ex.Message); }
                            finally { f.BeginInvoke(new Action(delegate { f.Close(); })); }
                        };
                        Application.Run(f);
                    }
                    return 0;
                }

                Application.Run(new LiteForm(tab));
                return 0;
            }
            catch (Exception ex)
            {
                Diag.Log("lite: фатальная ошибка: " + ex);
                if (!silent)
                {
                    try { MessageBox.Show(ex.Message, "Лицензии 1С — кратко", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    catch { }
                }
                return 2;
            }
        }

        private static bool Has(string[] args, string name)
        {
            foreach (string a in args)
            {
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)) return a.Substring(name.Length + 1).Trim('"');
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && !args[i + 1].StartsWith("/", StringComparison.Ordinal))
                    return args[i + 1].Trim('"');
            }
            return "";
        }
    }
}
