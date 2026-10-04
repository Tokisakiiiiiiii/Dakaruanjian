using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>配色、字体与 GDI+ 绘制辅助。</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("#F7F8FA");
        public static readonly Color Card = Color.White;
        public static readonly Color CardBorder = Hex("#E4E7EE");
        public static readonly Color Primary = Hex("#2F6FED");
        public static readonly Color PrimaryDark = Hex("#2559C4");
        public static readonly Color PrimarySoft = Hex("#EAF1FE");
        public static readonly Color TextMain = Hex("#1F2430");
        public static readonly Color TextMuted = Hex("#6B7280");
        public static readonly Color TextFaint = Hex("#9AA1AE");
        public static readonly Color HeatEmpty = Hex("#EBEDF0");
        public static readonly Color Divider = Hex("#EEF1F5");
        public static readonly Color Danger = Hex("#D64545");
        public static readonly Color DangerSoft = Hex("#FDECEC");
        public static readonly Color Success = Hex("#22A06B");
        public static readonly Color Hover = Hex("#F2F5FA");

        private static readonly Font _base;
        private static readonly Font _small;
        private static readonly Font _tiny;
        private static readonly Font _bold;
        private static readonly Font _h2;
        private static readonly Font _h1;
        private static readonly Font _num;
        private static readonly Font _numSmall;
        private static readonly Font _big;

        public static Font Base { get { return _base; } }
        public static Font Small { get { return _small; } }
        public static Font Tiny { get { return _tiny; } }
        public static Font Bold { get { return _bold; } }
        public static Font H2 { get { return _h2; } }
        public static Font H1 { get { return _h1; } }
        public static Font Num { get { return _num; } }
        public static Font NumSmall { get { return _numSmall; } }
        public static Font Big { get { return _big; } }

        static Theme()
        {
            string family = PickFamily(new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                "DengXian", "等线", "SimSun"
            });

            _base = new Font(family, 9f, FontStyle.Regular, GraphicsUnit.Point);
            _small = new Font(family, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _tiny = new Font(family, 8f, FontStyle.Regular, GraphicsUnit.Point);
            _bold = new Font(family, 9f, FontStyle.Bold, GraphicsUnit.Point);
            _h2 = new Font(family, 12f, FontStyle.Bold, GraphicsUnit.Point);
            _h1 = new Font(family, 15f, FontStyle.Bold, GraphicsUnit.Point);
            _num = new Font(family, 19f, FontStyle.Bold, GraphicsUnit.Point);
            _numSmall = new Font(family, 12.5f, FontStyle.Bold, GraphicsUnit.Point);
            _big = new Font(family, 13.5f, FontStyle.Bold, GraphicsUnit.Point);
        }

        private static string PickFamily(string[] wanted)
        {
            try
            {
                FontFamily[] families = FontFamily.Families;
                for (int w = 0; w < wanted.Length; w++)
                {
                    for (int f = 0; f < families.Length; f++)
                    {
                        if (string.Equals(families[f].Name, wanted[w], StringComparison.OrdinalIgnoreCase))
                        {
                            return families[f].Name;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return "Microsoft Sans Serif";
        }

        /// <summary>#RRGGBB / #RGB -> Color，非法输入返回灰色。</summary>
        public static Color Hex(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                {
                    return Color.Gray;
                }
                string h = hex.Trim();
                if (h.StartsWith("#", StringComparison.Ordinal))
                {
                    h = h.Substring(1);
                }
                if (h.Length == 3)
                {
                    h = new string(new char[]
                    {
                        h[0], h[0], h[1], h[1], h[2], h[2]
                    });
                }
                if (h.Length != 6)
                {
                    return Color.Gray;
                }
                int r = Convert.ToInt32(h.Substring(0, 2), 16);
                int g = Convert.ToInt32(h.Substring(2, 2), 16);
                int b = Convert.ToInt32(h.Substring(4, 2), 16);
                return Color.FromArgb(r, g, b);
            }
            catch (Exception)
            {
                return Color.Gray;
            }
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        /// <summary>把颜色按 amount(-1..1) 向白或黑混合。</summary>
        public static Color Shade(Color c, double amount)
        {
            if (amount > 0)
            {
                return Blend(c, Color.White, amount);
            }
            if (amount < 0)
            {
                return Blend(c, Color.Black, -amount);
            }
            return c;
        }

        public static Color Blend(Color a, Color b, double t)
        {
            if (t < 0)
            {
                t = 0;
            }
            if (t > 1)
            {
                t = 1;
            }
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>根据背景亮度选择黑或白文字，保证对比度。</summary>
        public static Color ContrastText(Color background)
        {
            double luma = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
            if (luma > 0.6)
            {
                return TextMain;
            }
            return Color.White;
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0)
            {
                return path;
            }
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = radius * 2;
            if (d > r.Width)
            {
                d = r.Width;
            }
            if (d > r.Height)
            {
                d = r.Height;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath path = RoundedRect(r, radius))
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        public static void DrawRoundedBorder(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath path = RoundedRect(r, radius))
            {
                using (Pen pen = new Pen(color, width))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        public static readonly TextFormatFlags LeftCenter =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        public static readonly TextFormatFlags RightCenter =
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            | TextFormatFlags.SingleLine;

        public static readonly TextFormatFlags CenterAll =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        public static readonly TextFormatFlags CenterWrap =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.WordBreak;
    }

    /// <summary>带 1px 边框与圆角的白底卡片容器。</summary>
    public class CardPanel : Panel
    {
        private string _title = "";
        private int _radius = 8;

        public string Title
        {
            get { return _title; }
            set { _title = value == null ? "" : value; this.Invalidate(); }
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; this.Invalidate(); }
        }

        /// <summary>标题占据的顶部高度，0 表示无标题。</summary>
        public int TitleHeight
        {
            get { return _title.Length == 0 ? 0 : 30; }
        }

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Padding = new Padding(14, 12, 14, 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            using (SolidBrush brush = new SolidBrush(Theme.Card))
            {
                using (GraphicsPath path = Theme.RoundedRect(r, _radius))
                {
                    g.FillPath(brush, path);
                }
            }
            Theme.DrawRoundedBorder(g, r, _radius, Theme.CardBorder, 1f);

            if (_title.Length > 0)
            {
                Rectangle tr = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, 22);
                Theme.DrawText(g, _title, Theme.Bold, Theme.TextMain, tr, Theme.LeftCenter);
            }

            base.OnPaint(e);
        }
    }

    /// <summary>圆角扁平按钮，支持"选中态"（用于打卡开关）。</summary>
    public class ModernButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _isOn;
        private int _radius = 6;
        private Color _accent = Theme.Primary;
        private Color _onAccent = Theme.Success;
        private bool _useOnState;

        public Color Accent
        {
            get { return _accent; }
            set { _accent = value; this.Invalidate(); }
        }

        public Color OnAccent
        {
            get { return _onAccent; }
            set { _onAccent = value; this.Invalidate(); }
        }

        /// <summary>为 true 时根据 IsOn 切换配色。</summary>
        public bool UseOnState
        {
            get { return _useOnState; }
            set { _useOnState = value; this.Invalidate(); }
        }

        public bool IsOn
        {
            get { return _isOn; }
            set { if (_isOn != value) { _isOn = value; this.Invalidate(); } }
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; this.Invalidate(); }
        }

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor | ControlStyles.StandardClick
                | ControlStyles.StandardDoubleClick, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(96, 32);
            Font = Theme.Base;
            ForeColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color baseColor = _useOnState && _isOn ? _onAccent : _accent;
            Color fill = baseColor;
            Color textColor = ForeColor;

            if (!Enabled)
            {
                fill = Theme.Blend(baseColor, Theme.Bg, 0.62);
                textColor = Theme.TextFaint;
            }
            else if (_pressed)
            {
                fill = Theme.Shade(baseColor, -0.18);
            }
            else if (_hover)
            {
                fill = Theme.Shade(baseColor, 0.12);
            }

            Theme.FillRounded(g, r, _radius, fill);
            Theme.DrawText(g, Text, Font, textColor, r, Theme.CenterAll);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            this.Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            this.Invalidate();
            base.OnMouseUp(e);
        }
    }

    /// <summary>心情档位：文字标签与配色。</summary>
    public static class Moods
    {
        public static readonly string[] Labels = new string[]
        {
            "很差", "不好", "一般", "不错", "很好"
        };

        private static readonly Color[] Palette = new Color[]
        {
            Theme.Hex("#D64545"),
            Theme.Hex("#E8894A"),
            Theme.Hex("#D9A62E"),
            Theme.Hex("#5AA469"),
            Theme.Hex("#22A06B")
        };

        public static string Label(int value)
        {
            if (value < 1 || value > 5)
            {
                return "";
            }
            return Labels[value - 1];
        }

        public static Color Color(int value)
        {
            if (value < 1 || value > 5)
            {
                return Theme.TextFaint;
            }
            return Palette[value - 1];
        }
    }
}
