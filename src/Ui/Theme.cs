using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace OneCLicenseInspector.Ui
{
    internal static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(26, 38, 50);
        public static readonly Color InkSoft = Color.FromArgb(51, 70, 90);
        // Приглушённый текст: #6B7A8A давал контраст 4,1–4,4:1 при требовании WCAG AA 4,5:1
        // для мелкого текста. #5F6E7E — 4,9–5,2:1 на белом и на подложке Canvas.
        public static readonly Color Muted = Color.FromArgb(95, 110, 126);
        public static readonly Color Line = Color.FromArgb(227, 232, 237);
        public static readonly Color Canvas = Color.FromArgb(245, 247, 249);
        public static readonly Color Paper = Color.White;
        public static readonly Color Accent = Color.FromArgb(227, 163, 60);
        public static readonly Color AccentDark = Color.FromArgb(198, 137, 36);
        // Тёмный янтарный для текста на светлом фоне: 5,9:1 на белом (AccentDark — только 3,0:1).
        public static readonly Color AccentText = Color.FromArgb(138, 90, 18);
        public static readonly Color AccentSoft = Color.FromArgb(253, 243, 227);
        public static readonly Color Green = Color.FromArgb(46, 125, 91);
        public static readonly Color GreenSoft = Color.FromArgb(230, 243, 236);
        public static readonly Color Red = Color.FromArgb(179, 58, 43);
        public static readonly Color RedSoft = Color.FromArgb(251, 233, 231);
        public static readonly Color Blue = Color.FromArgb(61, 90, 128);
        public static readonly Color BlueSoft = Color.FromArgb(233, 238, 245);

        private static string _uiFamily;
        private static string _monoFamily;
        private static bool _resolved;

        public static string UiFamily
        {
            get
            {
                if (!_resolved) Resolve();
                return _uiFamily;
            }
        }

        public static string MonoFamily
        {
            get
            {
                if (!_resolved) Resolve();
                return _monoFamily;
            }
        }

        private static void Resolve()
        {
            _resolved = true;
            _uiFamily = FirstInstalled(new string[] { "Segoe UI", "Tahoma", "Microsoft Sans Serif", "Arial" });
            _monoFamily = FirstInstalled(new string[] { "Consolas", "Courier New" });
        }

        /// <summary>
        /// Проверяем шрифты без InstalledFontCollection: его перечисление на серверах
        /// может занимать секунды и подвисать. Просто пробуем создать FontFamily по имени.
        /// </summary>
        private static string FirstInstalled(string[] names)
        {
            foreach (string want in names)
            {
                try
                {
                    using (FontFamily f = new FontFamily(want))
                    {
                        if (string.Equals(f.Name, want, StringComparison.OrdinalIgnoreCase)) return f.Name;
                    }
                }
                catch { }
            }
            return names[names.Length - 1];
        }

        public static Font Font(float size, FontStyle style)
        {
            // Размеры заданы в «пунктах макета» 96 DPI и переводятся в пиксели, чтобы шрифты
            // не увеличивались на мониторах с масштабированием и не рвали фиксированную вёрстку.
            float px = size * 96f / 72f;
            try { return new Font(UiFamily, px, style, GraphicsUnit.Pixel); }
            catch { return new Font(FontFamily.GenericSansSerif, px, style, GraphicsUnit.Pixel); }
        }

        public static Font MonoFont(float size)
        {
            float px = size * 96f / 72f;
            try { return new Font(MonoFamily, px, FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { return new Font(FontFamily.GenericMonospace, px, FontStyle.Regular, GraphicsUnit.Pixel); }
        }

        public static Font Body { get { return Font(9f, FontStyle.Regular); } }
        public static Font BodyBold { get { return Font(9f, FontStyle.Bold); } }
        public static Font Small { get { return Font(8.25f, FontStyle.Regular); } }
        public static Font SmallBold { get { return Font(8.25f, FontStyle.Bold); } }
        public static Font Section { get { return Font(10.5f, FontStyle.Bold); } }
        public static Font Title { get { return Font(16f, FontStyle.Bold); } }
        public static Font StatValue { get { return Font(22f, FontStyle.Bold); } }
        public static Font Logo { get { return Font(13f, FontStyle.Bold); } }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > Math.Min(r.Width, r.Height))
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, p);
            }
        }

        public static void DrawCard(Graphics g, Rectangle r)
        {
            FillRound(g, r, 8, Paper);
            DrawRound(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 8, Line, 1f);
        }

        public static void DrawPill(Graphics g, Rectangle r, string text, Color back, Color fore, Font font)
        {
            FillRound(g, r, r.Height / 2, back);
            TextRenderer.DrawText(g, text, font, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static void DrawIcon(Graphics g, string kind, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float sx = r.Width / 16f;
            float sy = r.Height / 16f;
            using (Pen pen = new Pen(color, 1.6f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                switch (kind)
                {
                    case "overview":
                        g.FillRectangle(brush, r.X + 1 * sx, r.Y + 1 * sy, 6 * sx, 6 * sy);
                        g.FillRectangle(brush, r.X + 9 * sx, r.Y + 1 * sy, 6 * sx, 6 * sy);
                        g.FillRectangle(brush, r.X + 1 * sx, r.Y + 9 * sy, 6 * sx, 6 * sy);
                        g.FillRectangle(brush, r.X + 9 * sx, r.Y + 9 * sy, 6 * sx, 6 * sy);
                        break;
                    case "platform":
                        g.DrawRectangle(pen, r.X + 1 * sx, r.Y + 3 * sy, 14 * sx, 4 * sy);
                        g.DrawRectangle(pen, r.X + 1 * sx, r.Y + 9 * sy, 14 * sx, 4 * sy);
                        break;
                    case "key":
                        g.DrawEllipse(pen, r.X + 1 * sx, r.Y + 2 * sy, 7 * sx, 7 * sy);
                        g.DrawLine(pen, r.X + 7 * sx, r.Y + 8 * sy, r.X + 14 * sx, r.Y + 15 * sy);
                        g.DrawLine(pen, r.X + 11 * sx, r.Y + 12 * sy, r.X + 13 * sx, r.Y + 10 * sy);
                        break;
                    case "file":
                        g.DrawRectangle(pen, r.X + 3 * sx, r.Y + 1 * sy, 9 * sx, 14 * sy);
                        g.DrawLine(pen, r.X + 6 * sx, r.Y + 5 * sy, r.X + 10 * sx, r.Y + 5 * sy);
                        g.DrawLine(pen, r.X + 6 * sx, r.Y + 8 * sy, r.X + 10 * sx, r.Y + 8 * sy);
                        g.DrawLine(pen, r.X + 6 * sx, r.Y + 11 * sy, r.X + 9 * sx, r.Y + 11 * sy);
                        break;
                    case "usage":
                        g.DrawLine(pen, r.X + 1 * sx, r.Y + 15 * sy, r.X + 15 * sx, r.Y + 15 * sy);
                        g.FillRectangle(brush, r.X + 2 * sx, r.Y + 9 * sy, 3 * sx, 6 * sy);
                        g.FillRectangle(brush, r.X + 6.5f * sx, r.Y + 5 * sy, 3 * sx, 10 * sy);
                        g.FillRectangle(brush, r.X + 11 * sx, r.Y + 2 * sy, 3 * sx, 13 * sy);
                        break;
                    case "journal":
                        g.DrawLine(pen, r.X + 2 * sx, r.Y + 3 * sy, r.X + 14 * sx, r.Y + 3 * sy);
                        g.DrawLine(pen, r.X + 2 * sx, r.Y + 8 * sy, r.X + 14 * sx, r.Y + 8 * sy);
                        g.DrawLine(pen, r.X + 2 * sx, r.Y + 13 * sy, r.X + 10 * sx, r.Y + 13 * sy);
                        break;
                    case "refresh":
                        g.DrawArc(pen, r.X + 2 * sx, r.Y + 2 * sy, 12 * sx, 12 * sy, 40, 280);
                        PointF[] arrow = new PointF[] { new PointF(r.X + 13 * sx, r.Y + 1 * sy), new PointF(r.X + 15 * sx, r.Y + 5 * sy), new PointF(r.X + 10.5f * sx, r.Y + 5 * sy) };
                        g.FillPolygon(brush, arrow);
                        break;
                    case "export":
                        g.DrawLine(pen, r.X + 8 * sx, r.Y + 1 * sy, r.X + 8 * sx, r.Y + 10 * sy);
                        g.DrawLine(pen, r.X + 4 * sx, r.Y + 6 * sy, r.X + 8 * sx, r.Y + 10 * sy);
                        g.DrawLine(pen, r.X + 12 * sx, r.Y + 6 * sy, r.X + 8 * sx, r.Y + 10 * sy);
                        g.DrawLine(pen, r.X + 2 * sx, r.Y + 12 * sy, r.X + 2 * sx, r.Y + 15 * sy);
                        g.DrawLine(pen, r.X + 2 * sx, r.Y + 15 * sy, r.X + 14 * sx, r.Y + 15 * sy);
                        g.DrawLine(pen, r.X + 14 * sx, r.Y + 12 * sy, r.X + 14 * sx, r.Y + 15 * sy);
                        break;
                }
            }
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Paper;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Line;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 242, 246);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(70, 88, 106);
            g.ColumnHeadersDefaultCellStyle.Font = SmallBold;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            // ColumnHeadersHeight нельзя задавать до создания окна: на Server 2003/2008 это
            // бросает Win32Exception "Error creating window handle" и приложение не стартует.
            g.HandleCreated += delegate
            {
                try
                {
                    int headerHeight = Math.Max(28, (int)(TextRenderer.MeasureText("Ag", SmallBold).Height * 1.7));
                    if (g.ColumnHeadersHeight != headerHeight) g.ColumnHeadersHeight = headerHeight;
                }
                catch { }
            };
            g.DefaultCellStyle.BackColor = Paper;
            g.DefaultCellStyle.ForeColor = InkSoft;
            g.DefaultCellStyle.SelectionBackColor = AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = Ink;
            g.DefaultCellStyle.Font = Body;
            g.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
            g.RowTemplate.Height = Math.Max(24, (int)(TextRenderer.MeasureText("Ag", Body).Height * 1.75));
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.AutoGenerateColumns = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        }
    }

    internal class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
