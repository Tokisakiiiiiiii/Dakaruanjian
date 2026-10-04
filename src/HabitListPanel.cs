using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 自绘习惯列表。每行显示：习惯色点、名称、连续/累计摘要、今日打卡圆环。
    /// 相比 OwnerDraw 的 ListBox，自绘 Panel 的排版与命中测试完全可控，也便于截图自检。
    /// 支持鼠标选择、双击改名、↑↓ 键切换、滚轮滚动。
    /// </summary>
    public class HabitListPanel : Control
    {
        public const int RowHeight = 56;

        private List<Habit> _habits = new List<Habit>();
        private string _selectedId;
        private int _hoverIndex = -1;
        private int _scrollY;

        public event EventHandler SelectionChanged;
        /// <summary>双击某一行（通常用于改名）。</summary>
        public event EventHandler HabitActivated;

        public HabitListPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = true;
        }

        public string SelectedId
        {
            get { return _selectedId; }
        }

        public Habit SelectedHabit
        {
            get
            {
                foreach (Habit h in _habits)
                {
                    if (string.Equals(h.Id, _selectedId, StringComparison.Ordinal))
                    {
                        return h;
                    }
                }
                return null;
            }
        }

        /// <summary>刷新数据源，并尽量保持当前选中项有效。</summary>
        public void SetData(List<Habit> habits, string preferredId)
        {
            _habits = habits == null ? new List<Habit>() : habits;

            bool stillThere = false;
            foreach (Habit h in _habits)
            {
                if (string.Equals(h.Id, preferredId, StringComparison.Ordinal))
                {
                    stillThere = true;
                    break;
                }
            }

            if (stillThere)
            {
                _selectedId = preferredId;
            }
            else if (_habits.Count > 0)
            {
                _selectedId = _habits[0].Id;
            }
            else
            {
                _selectedId = null;
            }

            ClampScroll();
            this.Invalidate();
        }

        public void SelectById(string id)
        {
            if (!string.Equals(_selectedId, id, StringComparison.Ordinal))
            {
                _selectedId = id;
                this.Invalidate();
            }
        }

        private int TotalHeight
        {
            get { return _habits.Count * RowHeight; }
        }

        private int MaxScroll
        {
            get
            {
                int m = TotalHeight - Height;
                return m < 0 ? 0 : m;
            }
        }

        private void ClampScroll()
        {
            if (_scrollY < 0)
            {
                _scrollY = 0;
            }
            int max = MaxScroll;
            if (_scrollY > max)
            {
                _scrollY = max;
            }
        }

        private int IndexAt(Point p)
        {
            int y = p.Y + _scrollY;
            if (y < 0)
            {
                return -1;
            }
            int idx = y / RowHeight;
            if (idx < 0 || idx >= _habits.Count)
            {
                return -1;
            }
            return idx;
        }

        public void EnsureVisible(int index)
        {
            if (index < 0)
            {
                return;
            }
            int top = index * RowHeight;
            int bottom = top + RowHeight;
            if (top < _scrollY)
            {
                _scrollY = top;
            }
            else if (bottom > _scrollY + Height)
            {
                _scrollY = bottom - Height;
            }
            ClampScroll();
        }

        private int SelectedIndex()
        {
            for (int i = 0; i < _habits.Count; i++)
            {
                if (string.Equals(_habits[i].Id, _selectedId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor == Color.Transparent ? Theme.Card : BackColor);

            if (_habits.Count == 0)
            {
                Rectangle empty = new Rectangle(12, 0, Math.Max(10, Width - 24), Math.Max(10, Height));
                Theme.DrawText(g, "还没有习惯项目\r\n点击下方「+ 新建习惯」开始", Theme.Base,
                    Theme.TextFaint, empty, Theme.CenterWrap);
                return;
            }

            string todayKey = Dates.TodayKey();
            DateTime today = DateTime.Today;

            for (int i = 0; i < _habits.Count; i++)
            {
                int y = i * RowHeight - _scrollY;
                if (y + RowHeight < 0 || y > Height)
                {
                    continue;
                }

                Habit h = _habits[i];
                Rectangle row = new Rectangle(4, y + 2, Math.Max(10, Width - 8), RowHeight - 4);
                bool selected = string.Equals(h.Id, _selectedId, StringComparison.Ordinal);
                bool hovered = i == _hoverIndex;

                Color habitColor = Theme.Hex(h.Color);

                if (selected)
                {
                    Theme.FillRounded(g, row, 7, Theme.PrimarySoft);
                    Rectangle accent = new Rectangle(row.X, row.Y + 6, 3, row.Height - 12);
                    Theme.FillRounded(g, accent, 2, habitColor);
                }
                else if (hovered)
                {
                    Theme.FillRounded(g, row, 7, Theme.Hover);
                }

                // 习惯色点
                Rectangle dot = new Rectangle(row.X + 13, row.Y + row.Height / 2 - 5, 10, 10);
                using (SolidBrush brush = new SolidBrush(habitColor))
                {
                    g.FillEllipse(brush, dot);
                }

                bool checkedToday = h.IsChecked(todayKey);
                int streak = Stats.CurrentStreak(h.DateSet(), today);

                int textLeft = dot.Right + 10;
                int checkSize = 20;
                int textRight = row.Right - checkSize - 14;
                int textWidth = textRight - textLeft;
                if (textWidth < 20)
                {
                    textWidth = 20;
                }

                Rectangle nameRect = new Rectangle(textLeft, row.Y + 9, textWidth, 19);
                Theme.DrawText(g, h.EffectiveName(), Theme.Bold,
                    selected ? Theme.TextMain : Theme.TextMain, nameRect, Theme.LeftCenter);

                string summary = streak > 0
                    ? string.Format("连续 {0} 天 · 累计 {1} 次", streak, h.Entries.Count)
                    : string.Format("尚未开始 · 累计 {0} 次", h.Entries.Count);
                Rectangle subRect = new Rectangle(textLeft, row.Y + 30, textWidth, 16);
                Theme.DrawText(g, summary, Theme.Tiny, Theme.TextMuted, subRect, Theme.LeftCenter);

                // 今日打卡圆环
                Rectangle ring = new Rectangle(row.Right - checkSize - 13,
                    row.Y + row.Height / 2 - checkSize / 2, checkSize, checkSize);
                if (checkedToday)
                {
                    using (SolidBrush brush = new SolidBrush(habitColor))
                    {
                        g.FillEllipse(brush, ring);
                    }
                    using (Pen pen = new Pen(Color.White, 2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(ring.X + ring.Width * 0.27f, ring.Y + ring.Height * 0.52f),
                            new PointF(ring.X + ring.Width * 0.44f, ring.Y + ring.Height * 0.70f),
                            new PointF(ring.X + ring.Width * 0.75f, ring.Y + ring.Height * 0.31f)
                        });
                    }
                }
                else
                {
                    using (Pen pen = new Pen(Theme.Blend(habitColor, Color.White, 0.45), 1.6f))
                    {
                        g.DrawEllipse(pen, ring);
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            this.Focus();
            int idx = IndexAt(e.Location);
            if (idx >= 0)
            {
                _selectedId = _habits[idx].Id;
                this.Invalidate();
                RaiseSelectionChanged();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx >= 0)
            {
                EventHandler handler = HabitActivated;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Location);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = e.Delta > 0 ? -RowHeight : RowHeight;
            _scrollY += step;
            ClampScroll();
            this.Invalidate();
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int idx = SelectedIndex();
            if (e.KeyCode == Keys.Down)
            {
                if (idx < _habits.Count - 1)
                {
                    _selectedId = _habits[idx + 1].Id;
                    EnsureVisible(idx + 1);
                    this.Invalidate();
                    RaiseSelectionChanged();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (idx > 0)
                {
                    _selectedId = _habits[idx - 1].Id;
                    EnsureVisible(idx - 1);
                    this.Invalidate();
                    RaiseSelectionChanged();
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
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
