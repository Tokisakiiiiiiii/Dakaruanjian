using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 心情选择器：5 个 GDI+ 手绘表情。
    /// 刻意不使用 emoji 字符 —— WinForms 下 Segoe UI Emoji 常常渲染成豆腐块或单色，
    /// 手绘可以保证任何 Windows 上都稳定显示。
    /// 再点一次已选中的档位即取消选择。
    /// </summary>
    public class MoodPicker : Control
    {
        private const int Count = 5;
        private const int LabelHeight = 16;

        private int? _selected;
        private int _hoverIndex = -1;

        public event EventHandler SelectionChanged;

        public int? SelectedValue
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    this.Invalidate();
                }
            }
        }

        public MoodPicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 62;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cellW = Width / Count;
            if (cellW <= 4)
            {
                return;
            }

            int faceArea = Height - LabelHeight;
            if (faceArea < 12)
            {
                faceArea = Height;
            }

            for (int i = 1; i <= Count; i++)
            {
                Rectangle cell = new Rectangle((i - 1) * cellW, 0, cellW, Height);
                int d = Math.Min(cellW - 20, faceArea - 8);
                if (d < 14)
                {
                    d = 14;
                }
                Rectangle face = new Rectangle(
                    cell.X + (cellW - d) / 2,
                    (faceArea - d) / 2,
                    d, d);

                bool selected = _selected.HasValue && _selected.Value == i;
                bool hovered = Enabled && _hoverIndex == i;
                Color moodColor = Moods.Color(i);

                Color fill;
                if (!Enabled)
                {
                    fill = Theme.Blend(moodColor, Color.White, 0.88);
                }
                else if (selected)
                {
                    fill = moodColor;
                }
                else
                {
                    fill = Theme.Blend(moodColor, Color.White, hovered ? 0.55 : 0.74);
                }

                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillEllipse(brush, face);
                }

                if (Enabled && (selected || hovered))
                {
                    using (Pen pen = new Pen(moodColor, selected ? 2.4f : 1.4f))
                    {
                        g.DrawEllipse(pen, Rectangle.Inflate(face, 3, 3));
                    }
                }

                DrawFace(g, face, Theme.ContrastText(fill), fill, i);

                if (faceArea < Height)
                {
                    Rectangle labelRect = new Rectangle(cell.X, Height - LabelHeight, cellW, LabelHeight);
                    Theme.DrawText(g, Moods.Label(i), Theme.Tiny,
                        Enabled && selected ? moodColor : Theme.TextFaint, labelRect, Theme.CenterAll);
                }
            }
        }

        /// <summary>画一张脸：两只眼睛 + 一条嘴。嘴的弧度由心情档位决定。</summary>
        private static void DrawFace(Graphics g, Rectangle face, Color ink, Color fill, int mood)
        {
            float cx = face.X + face.Width / 2f;
            float cy = face.Y + face.Height / 2f;
            float r = face.Width / 2f;

            float eyeR = Math.Max(1.3f, r * 0.11f);
            float eyeDx = r * 0.36f;
            float eyeY = cy - r * 0.26f;

            using (SolidBrush brush = new SolidBrush(ink))
            {
                g.FillEllipse(brush, cx - eyeDx - eyeR, eyeY - eyeR, eyeR * 2f, eyeR * 2f);
                g.FillEllipse(brush, cx + eyeDx - eyeR, eyeY - eyeR, eyeR * 2f, eyeR * 2f);
            }

            RectangleF mouth = new RectangleF(cx - r * 0.46f, cy - r * 0.14f, r * 0.92f, r * 0.62f);
            using (Pen pen = new Pen(ink, Math.Max(1.4f, r * 0.12f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (mood <= 2)
                {
                    // 上半弧线 = 嘴角向下
                    g.DrawArc(pen, mouth, 205f, mood == 1 ? 130f : 105f);
                }
                else if (mood == 3)
                {
                    float y = cy + r * 0.20f;
                    g.DrawLine(pen, cx - r * 0.34f, y, cx + r * 0.34f, y);
                }
                else
                {
                    // 下半弧线 = 嘴角向上
                    g.DrawArc(pen, mouth, mood == 5 ? 25f : 45f, mood == 5 ? 130f : 90f);
                }
            }
        }

        private int IndexAt(Point p)
        {
            if (Width <= 0)
            {
                return -1;
            }
            int cellW = Width / Count;
            if (cellW <= 0)
            {
                return -1;
            }
            int i = p.X / cellW + 1;
            if (i < 1)
            {
                i = 1;
            }
            if (i > Count)
            {
                i = Count;
            }
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i != _hoverIndex)
            {
                _hoverIndex = i;
                this.Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                this.Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i >= 1)
            {
                if (_selected.HasValue && _selected.Value == i)
                {
                    _selected = null;
                }
                else
                {
                    _selected = i;
                }
                this.Invalidate();
                RaiseSelectionChanged();
            }
            base.OnMouseClick(e);
        }

        private void RaiseSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
