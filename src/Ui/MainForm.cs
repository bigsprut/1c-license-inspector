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
        private ToolTip _copyTip;
        public FlatButton Copy;
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
            Copy = new FlatButton();
            Copy.Text = "Копировать";
            Copy.Icon = "copy";
            Copy.Kind = ButtonKind.Ghost;
            Copy.Size = new Size(132, 36);
            Controls.Add(RefreshButton);
            Controls.Add(Export);
            Controls.Add(Copy);
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
            Copy.Location = new Point(Export.Left - Copy.Width - 8, (Height - Copy.Height) / 2 + 2);
            RefreshButton.Location = new Point(Copy.Left - RefreshButton.Width - 8, (Height - RefreshButton.Height) / 2 + 2);
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
            int rightW = Dpi.S(g, 210);
            TextRenderer.DrawText(g, LeftText, Theme.Small, new Rectangle(dotX + dotSize + Dpi.S(g, 8), 0, Width - rightW, Height), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, RightText, Theme.Small, new Rectangle(Width - rightW + Dpi.S(g, 14), 0, rightW - Dpi.S(g, 20), Height), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    public class MainForm : Form
    {
        private ScanResult _result = new ScanResult();
        private ToolTip _copyTip;
        private HeaderPanel _header;
        private BufferedPanel _nav;
        private Panel _content;
        private StatusBarEx _status;
        private List<NavButton> _navButtons = new List<NavButton>();
        private List<PageBase> _pages = new List<PageBase>();
        private int _current = -1;
        private bool _scanning;
        private System.Windows.Forms.Timer _clipTimer;
        private string _lastClip = "";
        public bool AutoScan = true;

        private int _initialPage;

        public MainForm() : this(0) { }

        public MainForm(int initialPage)
        {
            _initialPage = initialPage;
            Diag.Log("ctor: begin");
            SuspendLayout();
            Text = "Инспектор лицензий 1С";
            BackColor = Theme.Canvas;
            Diag.Log("ctor: colors");
            if (!Program.NoFont) Font = Theme.Body;
            Diag.Log("ctor: font");
            AutoScaleMode = AutoScaleMode.None;
            if (!Program.NoCenter) StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1220, 780);
            if (!Program.NoMinSize) MinimumSize = new Size(1020, 660);
            if (!Program.NoKeyPreview) KeyPreview = true;
            if (!Program.NoIcon)
            {
                try
                {
                    Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }
            }

            Diag.Log("ctor: props done");
            if (Program.BareLevel == 0)
            {
                ResumeLayout(true);
                return;
            }

            _content = new Panel();
            if (Program.BareLevel >= 4)
            {
                _content.Dock = DockStyle.Fill;
                _content.BackColor = Theme.Canvas;
                _content.Padding = new Padding(0);
                Controls.Add(_content);
            }

            _status = new StatusBarEx();
            if (Program.BareLevel >= 3)
            {
                _status.Dock = DockStyle.Bottom;
                Controls.Add(_status);
            }

            _nav = new BufferedPanel();
            if (Program.BareLevel >= 2)
            {
                _nav.Dock = DockStyle.Left;
                _nav.Width = 236;
                _nav.BackColor = Color.White;
                _nav.Padding = new Padding(8, 12, 8, 8);
                _nav.Paint += NavPaint;
                Controls.Add(_nav);
            }

            _header = new HeaderPanel();
            if (Program.BareLevel >= 1)
            {
                _header.Dock = DockStyle.Top;
                _header.Subtitle = "Аудит лицензий на этом компьютере";
                _header.RefreshButton.Click += delegate { StartScan(); };
                _header.Export.Click += delegate { ExportReport(); };
            _header.Copy.Click += delegate { CopyCurrentPage(); };
                Controls.Add(_header);
            }

            Shown += delegate
            {
                Diag.Log("form: shown");
                StartClipWatch();
                if (Program.NavTest)
                {
                    for (int i = 0; i < _factories.Count; i++)
                    {
                        SelectPage(i);
                        Diag.Log("nav " + i + " done");
                    }
                    BeginInvoke(new Action(delegate { Close(); }));
                    return;
                }
                if (AutoScan && Program.BareLevel >= 4) StartScan();
            };

            if (Program.BareLevel < 4)
            {
                ResumeLayout(true);
                return;
            }

            Diag.Log("ctor: before pages");
            ContextMenuStrip copyMenu = new ContextMenuStrip();
            copyMenu.Items.Add("Копировать страницу (Ctrl+C)", null, delegate { CopyCurrentPage(); });
            _content.ContextMenuStrip = copyMenu;
            BuildPages();
            Diag.Log("ctor: pages done");
            ResumeLayout(true);
            Diag.Log("ctor: layout resumed");
            KeyDown += MainFormKeyDown;
        }

        public void PrepareHandles()
        {
            try { CreateControl(); Diag.Log("form: own handle ok"); }
            catch (Exception ex) { Diag.Log("form: OWN HANDLE FAILED " + ex.Message); return; }
            ProbeChild(_header, "header");
            ProbeChild(_nav, "nav");
            ProbeChild(_status, "status");
            for (int i = 0; i < _pages.Count; i++) ProbeChild(_pages[i], "page" + i);
        }

        private void ProbeHandles()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                try
                {
                    _pages[i].CreateControl();
                    Diag.Log("page " + i + ": handle ok");
                }
                catch (Exception ex)
                {
                    Diag.Log("page " + i + ": FAILED " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            ProbeChild(_header, "header");
            ProbeChild(_nav, "nav");
            ProbeChild(_status, "status");
        }

        private static void ProbeChild(Control c, string path)
        {
            try
            {
                c.CreateControl();
            }
            catch (Exception ex)
            {
                Diag.Log("FAIL " + path + " [" + c.GetType().Name + "]: " + ex.Message);
            }
            foreach (Control child in c.Controls)
            {
                ProbeChild(child, path + "/" + child.GetType().Name);
            }
        }

        private void NavPaint(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(Theme.Line))
            {
                e.Graphics.DrawLine(p, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height);
            }
        }

        private readonly List<Func<PageBase>> _factories = new List<Func<PageBase>>();

        private void BuildPages()
        {
            _nav.Resize += delegate { LayoutNav(); };

            RegisterPage("Обзор", "overview", delegate { return new OverviewPage(); });
            RegisterPage("Платформы 1С", "platform", delegate { return new PlatformsPage(); });
            RegisterPage("Аппаратные ключи", "key", delegate { return new HardwarePage(); });
            RegisterPage("Программные лицензии", "file", delegate { return new SoftwarePage(); });
            RegisterPage("Использование", "usage", delegate { return new UsagePage(); });
            RegisterPage("Журнал", "journal", delegate { return new JournalPage(); });

            SelectPage(Math.Max(0, Math.Min(_factories.Count - 1, _initialPage)));
            LayoutNav();

            Label footer = new Label();
            footer.Text = "Версия " + Program.Version + "  ·  Ctrl+C — копировать страницу";
            footer.ForeColor = Color.FromArgb(160, 172, 184);
            footer.Font = Theme.Small;
            footer.AutoSize = false;
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 40;
            footer.Padding = new Padding(10, 0, 0, 6);
            _nav.Controls.Add(footer);
        }

        private void RegisterPage(string title, string icon, Func<PageBase> factory)
        {
            int index = _factories.Count;
            _factories.Add(factory);
            _pages.Add(null);

            NavButton b = new NavButton();
            b.Text = title;
            b.Icon = icon;
            b.Width = 200;
            b.Click += delegate { SelectPage(index); };
            _nav.Controls.Add(b);
            _navButtons.Add(b);
        }

        private PageBase EnsurePage(int index)
        {
            if (index < 0 || index >= _factories.Count) return null;
            PageBase page = _pages[index];
            if (page != null) return page;
            try
            {
                page = _factories[index]();
                page.Visible = false;
                page.Dock = DockStyle.Fill;
                _content.Controls.Add(page);
                _pages[index] = page;
                Diag.Log("page " + index + " ok");
                if (_result != null)
                {
                    page.SetSnapshot(_result);
                    page.Bind(_result);
                    page.ScrollTop();
                }
            }
            catch (Exception ex)
            {
                Diag.Log("page " + index + " create failed: " + ex);
                return null;
            }
            return page;
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
            if (index < 0 || index >= _factories.Count) return;
            PageBase page = EnsurePage(index);
            if (page == null) return;

            _content.SuspendLayout();
            for (int i = 0; i < _pages.Count; i++)
            {
                bool on = i == index;
                if (_pages[i] != null) _pages[i].Visible = on;
                _navButtons[i].Selected = on;
                _navButtons[i].Invalidate();
            }
            _current = index;
            _content.ResumeLayout();
            page.BringToFront();
            page.Activated();
        }

        private void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) StartScan();
            if (e.Control && e.KeyCode == Keys.E) ExportReport();
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (ActiveControl is TextBoxBase) return;
                CopyCurrentPage();
            }
        }

        private void StartClipWatch()
        {
            _clipTimer = new System.Windows.Forms.Timer();
            _clipTimer.Interval = 1000;
            _clipTimer.Tick += delegate { CheckClipboard82(); };
            _clipTimer.Start();
        }

        private void CheckClipboard82()
        {
            if (_result == null) return;
            try
            {
                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text) || text == _lastClip) return;
                _lastClip = text;
                string summary;
                if (!Console82Apply.TryApply(_result, text, out summary)) return;
                Diag.Log("clipboard 8.2: " + summary);
                foreach (PageBase page in _pages)
                {
                    if (page == null) continue;
                    try { page.SetSnapshot(_result); page.Bind(_result); }
                    catch { }
                }
                _status.LeftText = "Из консоли 8.2: " + summary;
                _status.Invalidate();
            }
            catch { }
        }
        private void CopyCurrentPage()
        {
            try
            {
                if (_current < 0 || _current >= _pages.Count) return;
                PageBase page = _pages[_current];
                if (page == null) return;
                string text = page.CopyText();
                if (string.IsNullOrEmpty(text)) return;
                Clipboard.SetText(text);
                _status.LeftText = "Скопировано в буфер обмена";
                _status.Invalidate();
            }
            catch (Exception ex) { Diag.Log("copy failed: " + ex.Message); }
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

            foreach (PageBase p in _pages) { if (p != null) { p.SetSnapshot(r); p.Bind(r); p.ScrollTop(); } }
            BeginInvoke(new Action(delegate { foreach (PageBase p in _pages) { if (p != null) p.ScrollTop(); } }));

            _status.Busy = false;
            _status.LeftText = "Готово · найдено платформ: " + r.Platforms.Count + ", ключей: " + r.Sentinel.Keys.Count +
                               ", файлов лицензий: " + r.LicenseFiles.Count +
                               ", предупреждений: " + r.Warnings.Count;
            _status.RightText = "обновлено " + r.Finished.ToString("HH:mm") + "  ·  " + Fmt.Duration(r.Finished - r.Started);
            _status.Invalidate();
        }

        private void ExportReport()
        {
            try
            {
                if (_result == null) return;
                string text = Report.BuildDigest(_result, Diag.Tail(60000));
                Clipboard.SetText(text);
                if (_copyTip == null) _copyTip = new ToolTip();
                _copyTip.Show("Отчёт скопирован — вставь в чат", _header.Export, 0, -44, 2500);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось скопировать отчёт: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return string.IsNullOrEmpty(s) ? "server" : s;
        }
    }
}

