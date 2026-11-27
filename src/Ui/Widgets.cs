using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal static class Dpi
    {
        public static int S(Graphics g, int v)
        {
            if (g == null) return v;
            return (int)Math.Round(v * g.DpiX / 96f);
        }

        public static float S(Graphics g, float v)
        {
            if (g == null) return v;
            return v * g.DpiX / 96f;
        }

        public static Font Font(Graphics g, Font baseFont)
        {
            return baseFont;
        }
    }

    internal enum ButtonKind { Primary, Secondary, Ghost }

    internal class FlatButton : Control
    {
        public ButtonKind Kind = ButtonKind.Secondary;
        public string Icon = "";
        private bool _hover;
        private bool _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = Theme.BodyBold;
            Size = new Size(130, 34);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);

            Color back, fore, border;
            if (Kind == ButtonKind.Primary)
            {
                back = _down ? Theme.AccentDark : (_hover ? Color.FromArgb(237, 178, 84) : Theme.Accent);
                fore = Theme.Ink;
                border = back;
            }
            else if (Kind == ButtonKind.Ghost)
            {
                back = _hover ? Color.FromArgb(40, 255, 255, 255) : Color.Transparent;
                fore = Color.White;
                border = Color.Transparent;
            }
            else
            {
                back = _hover ? Color.FromArgb(246, 248, 250) : Color.White;
                fore = Theme.InkSoft;
                border = Theme.Line;
            }

            int radius = Dpi.S(g, 6);
            if (back != Color.Transparent) Theme.FillRound(g, r, radius, back);
            if (border != Color.Transparent) Theme.DrawRound(g, new Rectangle(0, 0, Width - 1, Height - 1), radius, border, 1f);

            int iconW = string.IsNullOrEmpty(Icon) ? 0 : Dpi.S(g, 21);
            Size textSize = TextRenderer.MeasureText(Text, Font);
            int totalW = textSize.Width + iconW;
            int x = (Width - totalW) / 2;
            if (!string.IsNullOrEmpty(Icon))
            {
                int isz = Dpi.S(g, 16);
                Rectangle ir = new Rectangle(x, (Height - isz) / 2, isz, isz);
                Theme.DrawIcon(g, Icon, ir, fore);
                x += iconW;
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x - Dpi.S(g, 6), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    internal class NavButton : Control
    {
        public string Icon = "overview";
        public bool Selected;
        private bool _hover;

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 42;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 1, Width - 1, Height - 2);
            if (Selected)
            {
                Theme.FillRound(g, r, Dpi.S(g, 6), Theme.AccentSoft);
                Theme.FillRound(g, new Rectangle(0, Dpi.S(g, 9), Dpi.S(g, 3), Height - Dpi.S(g, 18)), Dpi.S(g, 1), Theme.Accent);
            }
            else if (_hover)
            {
                Theme.FillRound(g, r, Dpi.S(g, 6), Color.FromArgb(247, 249, 251));
            }

            Color fore = Selected ? Theme.Ink : Theme.Muted;
            Color iconColor = Selected ? Theme.AccentDark : Color.FromArgb(139, 154, 168);
            int isz = Dpi.S(g, 16);
            Theme.DrawIcon(g, Icon, new Rectangle(Dpi.S(g, 14), (Height - isz) / 2, isz, isz), iconColor);
            TextRenderer.DrawText(g, Text, Selected ? Theme.BodyBold : Theme.Body,
                new Rectangle(Dpi.S(g, 42), 0, Width - Dpi.S(g, 50), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    internal class StatCard : BufferedPanel
    {
        public string Value = "—";
        public string Label = "";
        public string Hint = "";
        public Color Accent = Theme.Accent;

        public StatCard()
        {
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawCard(g, r);
            using (SolidBrush b = new SolidBrush(Accent))
            {
                Theme.FillRound(g, new Rectangle(0, Dpi.S(g, 14), Dpi.S(g, 4), Height - Dpi.S(g, 28)), Dpi.S(g, 2), Accent);
            }

            int left = Dpi.S(g, 19);
            int valueHeight = (int)(Theme.StatValue.GetHeight(g)) + Dpi.S(g, 4);
            TextRenderer.DrawText(g, Value, Theme.StatValue, new Rectangle(left, Dpi.S(g, 6), Width - left - Dpi.S(g, 12), valueHeight), Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            int labelTop = Dpi.S(g, 6) + valueHeight - Dpi.S(g, 2);
            TextRenderer.DrawText(g, Label, Theme.BodyBold, new Rectangle(left, labelTop, Width - left - Dpi.S(g, 12), Dpi.S(g, 20)), Theme.InkSoft,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(Hint))
            {
                TextRenderer.DrawText(g, Hint, Theme.Small, new Rectangle(left, labelTop + Dpi.S(g, 19), Width - left - Dpi.S(g, 12), Dpi.S(g, 18)), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal class PageTitle : BufferedPanel
    {
        public string Title = "";
        public string Subtitle = "";

        public PageTitle()
        {
            BackColor = Color.Transparent;
            Height = 56;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int titleHeight = (int)Theme.Title.GetHeight(g) + Dpi.S(g, 2);
            TextRenderer.DrawText(g, Title, Theme.Title, new Rectangle(0, 0, Width, titleHeight), Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(Subtitle))
            {
                TextRenderer.DrawText(g, Subtitle, Theme.Body, new Rectangle(0, titleHeight + Dpi.S(g, 2), Width, Dpi.S(g, 22)), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal class SectionTitle : BufferedPanel
    {
        public string Text2 = "";
        public string Note = "";

        public SectionTitle()
        {
            BackColor = Color.Transparent;
            Height = 26;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            TextRenderer.DrawText(g, Text2, Theme.Section, new Rectangle(0, 0, Width, Height), Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(Note))
            {
                Size s = TextRenderer.MeasureText(Text2, Theme.Section);
                TextRenderer.DrawText(g, Note, Theme.Small, new Rectangle(s.Width + Dpi.S(g, 12), 0, Width - s.Width - Dpi.S(g, 12), Height), Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal class KeyValueList : BufferedPanel
    {
        public List<KeyValuePair<string, string>> Items = new List<KeyValuePair<string, string>>();

        public KeyValueList()
        {
            BackColor = Color.White;
            AutoScroll = true;
        }

        public void Set(List<KeyValuePair<string, string>> items)
        {
            Items = items ?? new List<KeyValuePair<string, string>>();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Paper);
            int y = Dpi.S(g, 8);
            int labelW = Dpi.S(g, 186);
            int lineH = (int)(Theme.Body.GetHeight(g)) + Dpi.S(g, 5);
            using (Font f = Theme.Body)
            using (Font fb = Theme.BodyBold)
            {
                foreach (KeyValuePair<string, string> kv in Items)
                {
                    string value = kv.Value ?? "";
                    Rectangle vr = new Rectangle(labelW, y, Math.Max(10, Width - labelW - Dpi.S(g, 14)), lineH);
                    Size vs = TextRenderer.MeasureText(g, value, f, new Size(vr.Width, int.MaxValue), TextFormatFlags.WordBreak);
                    int h = Math.Max(lineH, vs.Height + Dpi.S(g, 2));
                    if (y + h > Height) break;

                    TextRenderer.DrawText(g, kv.Key, fb, new Rectangle(Dpi.S(g, 12), y, labelW - Dpi.S(g, 18), lineH), Theme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, value, f, new Rectangle(labelW, y, vr.Width, h), Theme.InkSoft,
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                    y += h + Dpi.S(g, 1);
                }
            }
        }
    }

    internal class SegmentedTabs : BufferedPanel
    {
        public List<string> Items = new List<string>();
        public int SelectedIndex;
        public event EventHandler SelectedIndexChanged;
        private int _hover = -1;

        public SegmentedTabs()
        {
            BackColor = Color.White;
            Height = 38;
        }

        public void SetSelected(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            if (SelectedIndex == index) return;
            SelectedIndex = index;
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitTest(e.X);
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int idx = HitTest(e.X);
            if (idx >= 0) SetSelected(idx);
            base.OnMouseDown(e);
        }

        private int HitTest(int x)
        {
            int cx = Dpi.S(CreateGraphics(), 8);
            using (Font f = Theme.BodyBold)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    int w = TextRenderer.MeasureText(Items[i], f).Width + Dpi.S(CreateGraphics(), 28);
                    if (x >= cx && x <= cx + w) return i;
                    cx += w + Dpi.S(CreateGraphics(), 4);
                }
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Paper);
            int cx = Dpi.S(g, 8);
            int pad = Dpi.S(g, 14);
            using (Font f = Theme.BodyBold)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    int w = TextRenderer.MeasureText(Items[i], f).Width + pad * 2;
                    Rectangle r = new Rectangle(cx, Dpi.S(g, 5), w, Height - Dpi.S(g, 10));
                    if (i == SelectedIndex) Theme.FillRound(g, r, Dpi.S(g, 6), Theme.AccentSoft);
                    else if (i == _hover) Theme.FillRound(g, r, Dpi.S(g, 6), Color.FromArgb(246, 248, 250));
                    TextRenderer.DrawText(g, Items[i], f, r, i == SelectedIndex ? Theme.Ink : Theme.Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    cx += w + Dpi.S(g, 4);
                }
            }
        }
    }

    internal class EmptyState : BufferedPanel
    {
        public string Title = "Нет данных";
        public string Text2 = "";
        public string Icon = "file";

        public EmptyState()
        {
            BackColor = Theme.Paper;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Paper);
            int cy = Height / 2 - Dpi.S(g, 50);
            Theme.DrawIcon(g, Icon, new Rectangle(Width / 2 - Dpi.S(g, 16), cy, Dpi.S(g, 32), Dpi.S(g, 32)), Color.FromArgb(180, 192, 204));
            TextRenderer.DrawText(g, Title, Theme.Section, new Rectangle(Dpi.S(g, 10), cy + Dpi.S(g, 44), Width - Dpi.S(g, 20), Dpi.S(g, 24)), Theme.InkSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Text2, Theme.Body, new Rectangle(Dpi.S(g, 10), cy + Dpi.S(g, 70), Width - Dpi.S(g, 20), Dpi.S(g, 60)), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }
}
