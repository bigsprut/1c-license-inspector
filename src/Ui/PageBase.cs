using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Text;

namespace OneCLicenseInspector.Ui
{
    internal class PageBase : BufferedPanel
    {
        protected ScanResult Snapshot;
        protected TableLayoutPanel Root;
        protected int _row;
        private int _totalHeight;

        public void SetSnapshot(ScanResult r) { Snapshot = r; }

        /// <summary>Текст текущей страницы для копирования в буфер (Ctrl+C).</summary>
        public virtual string CopyText() { return BuildSummary(); }

        protected string BuildSummary()
        {
            ScanResult r = Snapshot;
            if (r == null) return "";
            StringBuilder b = new StringBuilder();
            b.AppendLine("Инспектор лицензий 1С " + Program.Version);
            b.AppendLine("Компьютер: " + r.System.ComputerName + " · " + r.System.OsCaption + " " + r.System.OsArch);
            b.AppendLine("Проверка: " + Fmt.Date(r.Finished));
            b.AppendLine();
            b.AppendLine("Платформы 1С (" + r.Platforms.Count + "):");
            foreach (PlatformInfo p in r.Platforms)
                b.AppendLine("  " + p.Version + "  " + p.Bitness + "  " + p.BinPath);
            b.AppendLine();
            b.AppendLine("Аппаратные ключи (" + r.Sentinel.Keys.Count + "):");
            foreach (SentinelKey k in r.Sentinel.Keys)
                b.AppendLine("  " + k.Id + "  " + k.TypeName + "  " + k.Name + "  прошивка " + k.Firmware);
            foreach (SentinelFeature f in r.Sentinel.Features)
                b.AppendLine("  компонент " + f.FeatureId + " " + f.Name + ": занято " + f.Logins + " из " + (f.IsUnlimited ? "∞" : f.Limit));
            if (r.Hasp4.UsbKeys.Count > 0)
                b.AppendLine("  USB: " + string.Join("; ", r.Hasp4.UsbKeys.ToArray()));
            b.AppendLine();
            b.AppendLine("Программные лицензии (" + r.LicenseFiles.Count + "):");
            int users = 0;
            foreach (LicenseFileInfo lf in r.LicenseFiles)
            {
                users += lf.UsersNum;
                b.AppendLine("  " + lf.FileName + " · PIN " + lf.Pin + " · " + lf.Product + " · " + lf.LicenseType +
                             " · пользователей: " + lf.Users + " · " + lf.Expires) ;
                b.AppendLine("    привязка: " + lf.BoundTo);
            }
            b.AppendLine("  Итого пользователей: " + users);
            b.AppendLine();
            if (r.Cluster != null)
            {
                b.AppendLine("Сеансы (" + r.Cluster.Sessions.Count + "):");
                foreach (SessionEntry s in r.Cluster.Sessions)
                    b.AppendLine("  " + s.User + " · " + s.Host + " · " + s.AppId + " · " + s.LicenseShort);
                if (!string.IsNullOrEmpty(r.Cluster.Error)) b.AppendLine("  " + r.Cluster.Error);
            }
            if (r.Warnings.Count > 0)
            {
                b.AppendLine();
                b.AppendLine("Предупреждения:");
                foreach (Warning w in r.Warnings) b.AppendLine("  " + w.Title + ": " + w.Text);
            }
            return b.ToString();
        }
        public PageBase()
        {
            BackColor = Theme.Canvas;
            AutoScroll = true;
            Padding = new Padding(26, 12, 26, 22);
            DoubleBuffered = true;

            Root = new TableLayoutPanel();
            Root.Dock = DockStyle.Top;
            Root.AutoSize = false;
            Root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Root.ColumnCount = 1;
            Root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Root.BackColor = Color.Transparent;
            Root.Margin = new Padding(0);
            Root.Padding = new Padding(0, 0, 6, 0);
            Root.Height = 0;
            Controls.Add(Root);
            _row = 0;
        }

        public virtual void Bind(ScanResult r) { }
        public virtual void Activated() { }

        public void ScrollTop()
        {
            try { AutoScrollPosition = new Point(0, 0); }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            try
            {
                int w = ClientSize.Width - Padding.Horizontal;
                if (w > 120 && Root.Width != w) Root.Width = w;
            }
            catch { }
        }

        protected int AddRow(Control c, int height)
        {
            int row = _row;
            _totalHeight += height;
            Root.Height = _totalHeight;
            Root.RowCount = _row + 1;
            Root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0);
            Root.Controls.Add(c, 0, _row);
            _row++;
            return row;
        }

        protected void SetRowHeight(int row, int height)
        {
            try
            {
                if (row < 0 || row >= Root.RowStyles.Count) return;
                int old = (int)Root.RowStyles[row].Height;
                if (old == height) return;
                Root.RowStyles[row].Height = height;
                Root.Height += (height - old);
                Root.PerformLayout();
            }
            catch { }
        }

        protected void AddAutoRow(Control c)
        {
            _totalHeight += c.Height;
            Root.Height = _totalHeight;
            Root.RowCount = _row + 1;
            Root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c.Dock = DockStyle.Top;
            c.Margin = new Padding(0);
            Root.Controls.Add(c, 0, _row);
            _row++;
        }

        protected void AddSpacer(int height)
        {
            BufferedPanel p = new BufferedPanel();
            p.BackColor = Theme.Canvas;
            AddRow(p, height);
        }

        protected PageTitle AddHeader(string title, string subtitle)
        {
            PageTitle h = new PageTitle();
            h.Title = title;
            h.Subtitle = subtitle;
            h.Height = 62;
            AddRow(h, 68);
            return h;
        }

        protected void EnableCopy(TextBox tb)
        {
            if (tb == null) return;
            tb.ShortcutsEnabled = true;
            tb.TabStop = true;
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Копировать выделенное", null, delegate
            {
                try
                {
                    string text = tb.SelectionLength > 0 ? tb.SelectedText : tb.Text;
                    if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
                }
                catch { }
            });
            m.Items.Add("Выделить всё", null, delegate { tb.SelectAll(); tb.Focus(); });
            tb.ContextMenuStrip = m;
        }
        protected Panel Card(Control inner, Padding pad)
        {
            BufferedPanel card = new BufferedPanel();
            card.BackColor = Theme.Paper;
            card.Padding = pad;
            card.Paint += delegate (object s, PaintEventArgs e)
            {
                Theme.DrawRound(e.Graphics, new Rectangle(0, 0, card.Width - 1, card.Height - 1), Dpi.S(e.Graphics, 8), Theme.Line, 1f);
            };
            inner.Dock = DockStyle.Fill;
            card.Controls.Add(inner);
            return card;
        }

        protected DataGridView MakeGrid()
        {
            DataGridView g = new DataGridView();
            Theme.StyleGrid(g);
            g.TabStop = true;
            g.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            return g;
        }

        protected DataGridViewColumn Col(DataGridView g, string header, string property, int weight, string format)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.DataPropertyName = property;
            c.Name = property + "_" + g.Columns.Count;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            c.DefaultCellStyle.Format = format;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            g.Columns.Add(c);
            return c;
        }
    }

    internal class StatusItem
    {
        public Color Dot;
        public string Title;
        public string Text;

        public StatusItem(Color dot, string title, string text)
        {
            Dot = dot;
            Title = title;
            Text = text;
        }
    }

    internal class StatusListPanel : BufferedPanel
    {
        public List<StatusItem> Items = new List<StatusItem>();
        public bool ShowFrame = true;

        public StatusListPanel()
        {
            BackColor = Color.Transparent;
            Height = 40;
        }

        public int Measure(int width)
        {
            if (width < 200) width = 600;
            Graphics g = TextMeasure.G;
            int w = width - Dpi.S(g, 62);
            int lineH = (int)Theme.Body.GetHeight(g);
            int h = Dpi.S(g, 20);
            using (Font f = Theme.Body)
            {
                foreach (StatusItem it in Items)
                {
                    int th = TextRenderer.MeasureText(g, it.Text, f, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                    h += Math.Max(Dpi.S(g, 34), th + lineH + Dpi.S(g, 20)) + Dpi.S(g, 8);
                }
            }
            return h + Dpi.S(g, 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Canvas);
            Rectangle frame = new Rectangle(0, 0, Width - 1, Height - 1);
            if (ShowFrame)
            {
                Theme.FillRound(g, frame, Dpi.S(g, 8), Theme.Paper);
                Theme.DrawRound(g, frame, Dpi.S(g, 8), Theme.Line, 1f);
            }

            int y = Dpi.S(g, 14);
            int w = Width - Dpi.S(g, 62);
            int lineH = (int)Theme.Body.GetHeight(g);
            using (Font fb = Theme.BodyBold)
            using (Font f = Theme.Body)
            {
                foreach (StatusItem it in Items)
                {
                    int dotSize = Dpi.S(g, 9);
                    using (SolidBrush b = new SolidBrush(it.Dot))
                    {
                        g.FillEllipse(b, Dpi.S(g, 18), y + Dpi.S(g, 4), dotSize, dotSize);
                    }
                    TextRenderer.DrawText(g, it.Title, fb, new Rectangle(Dpi.S(g, 40), y, w, lineH + Dpi.S(g, 3)), Theme.Ink,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    int th = TextRenderer.MeasureText(g, it.Text, f, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                    TextRenderer.DrawText(g, it.Text, f, new Rectangle(Dpi.S(g, 40), y + lineH + Dpi.S(g, 3), w, th + Dpi.S(g, 4)), Theme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                    y += Math.Max(Dpi.S(g, 34), th + lineH + Dpi.S(g, 20)) + Dpi.S(g, 8);
                }
            }
        }
    }

    internal class WarnPanel : BufferedPanel
    {
        public List<Warning> Items = new List<Warning>();

        public WarnPanel()
        {
            BackColor = Color.Transparent;
        }

        public int Measure(int width)
        {
            if (width < 200) width = 500;
            Graphics g = TextMeasure.G;
            int w = width - Dpi.S(g, 46);
            int lineH = (int)Theme.Body.GetHeight(g);
            int h = Dpi.S(g, 8);
            using (Font f = Theme.Body)
            {
                foreach (Warning it in Items)
                {
                    int th = TextRenderer.MeasureText(g, it.Text, f, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                    h += th + lineH + Dpi.S(g, 34) + Dpi.S(g, 8);
                }
            }
            return Math.Max(h + Dpi.S(g, 8), Dpi.S(g, 90));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Canvas);
            if (Items.Count == 0)
            {
                Rectangle empty = new Rectangle(0, 0, Width - 1, Height - 1);
                Theme.FillRound(g, empty, Dpi.S(g, 8), Theme.Paper);
                Theme.DrawRound(g, empty, Dpi.S(g, 8), Theme.Line, 1f);
                TextRenderer.DrawText(g, "Предупреждений нет", Theme.BodyBold, new Rectangle(Dpi.S(g, 16), Dpi.S(g, 14), Width - Dpi.S(g, 32), Dpi.S(g, 20)), Theme.Green,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Все проверенные компоненты лицензирования в порядке.", Theme.Body, new Rectangle(Dpi.S(g, 16), Dpi.S(g, 36), Width - Dpi.S(g, 32), Dpi.S(g, 40)), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.WordBreak);
                return;
            }

            int y = 0;
            int w = Width - Dpi.S(g, 46);
            int lineH = (int)Theme.Body.GetHeight(g);
            using (Font fb = Theme.BodyBold)
            using (Font f = Theme.Body)
            {
                foreach (Warning it in Items)
                {
                    Color accent = it.Level == LogLevel.Error ? Theme.Red : (it.Level == LogLevel.Warn ? Theme.Accent : Theme.Blue);
                    int th = TextRenderer.MeasureText(g, it.Text, f, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                    int h = th + lineH + Dpi.S(g, 34);
                    Rectangle card = new Rectangle(0, y, Width - 1, h);
                    Theme.FillRound(g, card, Dpi.S(g, 8), Theme.Paper);
                    Theme.DrawRound(g, card, Dpi.S(g, 8), Theme.Line, 1f);
                    Theme.FillRound(g, new Rectangle(0, Dpi.S(g, 12), Dpi.S(g, 4), h - Dpi.S(g, 24)), Dpi.S(g, 2), accent);
                    TextRenderer.DrawText(g, it.Title, fb, new Rectangle(Dpi.S(g, 16), y + Dpi.S(g, 10), w, lineH + Dpi.S(g, 3)), Theme.Ink,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, it.Text, f, new Rectangle(Dpi.S(g, 16), y + Dpi.S(g, 10) + lineH + Dpi.S(g, 3), w, th + Dpi.S(g, 4)), Theme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                    y += h + Dpi.S(g, 8);
                }
            }
        }
    }
}
