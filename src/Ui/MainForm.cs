using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace OneCLicenseInspector.Ui
{
    internal class HeaderPanel : BufferedPanel
    {
        public FlatButton RefreshButton;
        public FlatButton Export;
        public string Machine = "";
        public string Subtitle = "";
        private bool _busy;

        public HeaderPanel()
        {
            Height = 88;
            BackColor = Theme.Ink;
            RefreshButton = new FlatButton();
            RefreshButton.Text = "Обновить";
            RefreshButton.Icon = "refresh";
            RefreshButton.Kind = ButtonKind.Primary;
            RefreshButton.Size = new Size(132, 36);
            Export = new FlatButton();
            Export.Text = "Отчёт";
            Export.Icon = "export";
            Export.Kind = ButtonKind.Ghost;
            Export.Size = new Size(118, 36);
            Controls.Add(RefreshButton);
            Controls.Add(Export);
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            RefreshButton.Enabled = !busy;
            Export.Enabled = !busy;
            RefreshButton.Text = busy ? "Сканирование…" : "Обновить";
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (RefreshButton == null || Export == null) return;
            int right = Width - 20;
            Export.Location = new Point(right - Export.Width, (Height - Export.Height) / 2 + 2);
            RefreshButton.Location = new Point(Export.Left - RefreshButton.Width - 8, (Height - RefreshButton.Height) / 2 + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = ClientRectangle;
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                Color.FromArgb(23, 34, 46), Color.FromArgb(38, 56, 74), LinearGradientMode.Vertical))
            {
                g.FillRectangle(b, r);
            }
            using (SolidBrush accent = new SolidBrush(Theme.Accent))
            {
                g.FillRectangle(accent, new Rectangle(0, Height - 3, Width, 3));
            }

            int logoSize = Dpi.S(g, 46);
            Rectangle logo = new Rectangle(Dpi.S(g, 22), (Height - logoSize) / 2 - Dpi.S(g, 1), logoSize, logoSize);
            Theme.FillRound(g, logo, Dpi.S(g, 10), Theme.Accent);
            using (Font lf = Theme.Font(12f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "1С", lf, logo, Theme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            int textLeft = Dpi.S(g, 82);
            int textW = Math.Max(Dpi.S(g, 80), RefreshButton.Left - textLeft - Dpi.S(g, 12));
            int titleH = Dpi.S(g, 34);
            using (Font tf = Theme.Font(14f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "Инспектор лицензий 1С", tf, new Rectangle(textLeft, Dpi.S(g, 12), textW, titleH), Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(g, Subtitle, Theme.Body, new Rectangle(textLeft + Dpi.S(g, 1), Dpi.S(g, 12) + titleH, textW, Dpi.S(g, 26)), Color.FromArgb(168, 183, 198),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    internal class StatusBarEx : BufferedPanel
    {
        public string LeftText = "Готово";
        public string RightText = "";
        public bool Busy;

        public StatusBarEx()
        {
            Height = 30;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            using (Pen p = new Pen(Theme.Line))
            {
                g.DrawLine(p, 0, 0, Width, 0);
            }
            int dotSize = Dpi.S(g, 8);
            int dotX = Dpi.S(g, 14);
            using (SolidBrush b = new SolidBrush(Busy ? Theme.Accent : Theme.Green))
            {
                g.FillEllipse(b, dotX, (Height - dotSize) / 2 + Dpi.S(g, 1), dotSize, dotSize);
            }
            int rightW = Dpi.S(g, 300);
            TextRenderer.DrawText(g, LeftText, Theme.Small, new Rectangle(dotX + dotSize + Dpi.S(g, 8), 0, Width - rightW, Height), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, RightText, Theme.Small, new Rectangle(Width - rightW + Dpi.S(g, 14), 0, rightW - Dpi.S(g, 20), Height), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    public class MainForm : Form
    {
        private ScanResult _result = new ScanResult();
        private HeaderPanel _header;
        private BufferedPanel _nav;
        private Panel _content;
        private StatusBarEx _status;
        private List<NavButton> _navButtons = new List<NavButton>();
        private List<PageBase> _pages = new List<PageBase>();
        private int _current = -1;
        private bool _scanning;
        public bool AutoScan = true;

        private int _initialPage;

        public MainForm() : this(0) { }

        public MainForm(int initialPage)
        {
            _initialPage = initialPage;
            SuspendLayout();
            Text = "Инспектор лицензий 1С";
            BackColor = Theme.Canvas;
            Font = Theme.Body;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1220, 780);
            MinimumSize = new Size(1020, 660);
            KeyPreview = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = Theme.Canvas;
            _content.Padding = new Padding(0);
            Controls.Add(_content);

            _status = new StatusBarEx();
            _status.Dock = DockStyle.Bottom;
            Controls.Add(_status);

            _nav = new BufferedPanel();
            _nav.Dock = DockStyle.Left;
            _nav.Width = 218;
            _nav.BackColor = Color.White;
            _nav.Padding = new Padding(8, 12, 8, 8);
            _nav.Paint += NavPaint;
            Controls.Add(_nav);

            _header = new HeaderPanel();
            _header.Dock = DockStyle.Top;
            _header.Subtitle = "Аудит лицензий на этом компьютере";
            _header.RefreshButton.Click += delegate { StartScan(); };
            _header.Export.Click += delegate { ExportReport(); };
            Controls.Add(_header);

            BuildPages();
            ResumeLayout(true);

            KeyDown += MainFormKeyDown;
            Shown += delegate
            {
                if (AutoScan) StartScan();
            };
        }

        private void NavPaint(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(Theme.Line))
            {
                e.Graphics.DrawLine(p, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height);
            }
        }

        private void BuildPages()
        {
            AddPage(new OverviewPage(), "Обзор", "overview");
            AddPage(new PlatformsPage(), "Платформы 1С", "platform");
            AddPage(new HardwarePage(), "Аппаратные ключи", "key");
            AddPage(new SoftwarePage(), "Программные лицензии", "file");
            AddPage(new UsagePage(), "Использование", "usage");
            AddPage(new JournalPage(), "Журнал", "journal");

            SelectPage(_initialPage);

            _nav.Resize += delegate { LayoutNav(); };
            LayoutNav();

            Label footer = new Label();
            footer.Text = "Версия " + Program.Version + "  ·  .NET " + Environment.Version.Major + "." + Environment.Version.Minor;
            footer.ForeColor = Color.FromArgb(160, 172, 184);
            footer.Font = Theme.Small;
            footer.AutoSize = false;
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 40;
            footer.Padding = new Padding(10, 0, 0, 6);
            _nav.Controls.Add(footer);
        }

        private void AddPage(PageBase page, string title, string icon)
        {
            page.Visible = false;
            page.Dock = DockStyle.Fill;
            _content.Controls.Add(page);
            _pages.Add(page);

            NavButton b = new NavButton();
            b.Text = title;
            b.Icon = icon;
            b.Width = 200;
            int index = _navButtons.Count;
            b.Click += delegate { SelectPage(index); };
            _nav.Controls.Add(b);
            _navButtons.Add(b);
        }

        private void LayoutNav()
        {
            try
            {
                int y = 10;
                int w = Math.Max(80, _nav.ClientSize.Width - 16);
                foreach (NavButton b in _navButtons)
                {
                    b.Location = new Point(8, y);
                    b.Width = w;
                    y += b.Height + 4;
                }
            }
            catch { }
        }

        private void SelectPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            if (_current == index)
            {
                _pages[index].Visible = true;
                return;
            }
            _content.SuspendLayout();
            for (int i = 0; i < _pages.Count; i++)
            {
                bool on = i == index;
                _pages[i].Visible = on;
                _navButtons[i].Selected = on;
                _navButtons[i].Invalidate();
            }
            _current = index;
            _content.ResumeLayout();
            _pages[index].BringToFront();
            _pages[index].Activated();
        }

        private void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) StartScan();
            if (e.Control && e.KeyCode == Keys.E) ExportReport();
        }

        private void StartScan()
        {
            if (_scanning) return;
            _scanning = true;
            _header.SetBusy(true);
            _status.Busy = true;
            _status.LeftText = "Сканирование…";
            _status.Invalidate();

            Thread t = new Thread(delegate ()
            {
                ScanResult r = Scanner.Run(delegate (string text)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            _status.LeftText = text;
                            _status.Invalidate();
                        }));
                    }
                    catch { }
                });
                try
                {
                    BeginInvoke(new Action(delegate { ApplyResult(r); }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Name = "scan";
            t.Start();
        }

        public void ApplyDemoHeader()
        {
            _header.Subtitle = "COMPUTER  ·  Windows Server  ·  x64";
            _header.Invalidate();
        }

        public void ApplyForScreenshot(ScanResult r)
        {
            ApplyResult(r);
        }

        private void ApplyResult(ScanResult r)
        {
            _result = r;
            _scanning = false;
            _header.SetBusy(false);
            _header.Subtitle = r.System.ComputerName + "  ·  " + r.System.OsCaption + "  ·  " + r.System.OsArch;
            _header.Invalidate();

            foreach (PageBase p in _pages) { p.Bind(r); p.ScrollTop(); }
            BeginInvoke(new Action(delegate { foreach (PageBase p in _pages) p.ScrollTop(); }));

            _status.Busy = false;
            _status.LeftText = "Готово · найдено платформ: " + r.Platforms.Count + ", ключей: " + r.Sentinel.Keys.Count +
                               ", файлов лицензий: " + r.LicenseFiles.Count +
                               ", предупреждений: " + r.Warnings.Count;
            _status.RightText = "Обновлено " + r.Finished.ToString("HH:mm:ss") + "  ·  " + Fmt.Duration(r.Finished - r.Started);
            _status.Invalidate();
        }

        private void ExportReport()
        {
            try
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = "Сохранить отчёт о лицензиях";
                    dlg.Filter = "HTML-отчёт (*.html)|*.html|CSV (*.csv)|*.csv";
                    dlg.FileName = "licenses-" + SafeName(_result.System.ComputerName) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".html";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    if (dlg.FilterIndex == 2)
                    {
                        Program.WriteText(dlg.FileName, Report.BuildCsv(_result), true);
                    }
                    else
                    {
                        Program.WriteText(dlg.FileName, Report.BuildHtml(_result), true);
                    }

                    if (MessageBox.Show(this, "Отчёт сохранён:\r\n" + dlg.FileName + "\r\n\r\nОткрыть его сейчас?", "Готово",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        Process.Start(dlg.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить отчёт: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return string.IsNullOrEmpty(s) ? "server" : s;
        }
    }
}

