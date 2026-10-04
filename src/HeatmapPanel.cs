using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 日历热力图。
    ///
    ///   "本习惯"模式：打卡日记为习惯色实心，未打卡为浅灰；
    ///                 有备注的日期右下角小三角，有心情的日期右上角心情色圆点。
    ///   "全部习惯"模式：颜色深浅 = 当天完成的习惯比例（4 档）。
    ///
    /// 点击过去/今天的日期即可补打卡或取消打卡；未来日期置灰且不可点击。
    /// </summary>
    public class HeatmapPanel : Control
    {
        private const int HeaderHeight = 30;
        private const int WeekHeaderHeight = 20;
        private const int FooterHeight = 26;
        /// <summary>当前月份实际需要的周数（4~6），在 BuildLayout 中计算。</summary>
        private int _rows = 6;

        private sealed class Cell
        {
            public DateTime Day;
            public Rectangle Rect;
            public bool InMonth;
        }

        private readonly List<Cell> _cells = new List<Cell>();

        private Habit _habit;
        private List<Habit> _allHabits = new List<Habit>();
        private string _mode = "habit";
        private DateTime _month;
        private int _hoverIndex = -1;

        private Rectangle _prevRect;
        private Rectangle _nextRect;
        private Rectangle _todayRect;
        private Rectangle _modeHabitRect;
        private Rectangle _modeAllRect;

        /// <summary>用户点击了某一天（参数为该日期）。</summary>
        public event EventHandler<DateTime> DayToggled;
        /// <summary>切换了 本习惯 / 全部习惯 视角。</summary>
        public event EventHandler ModeChanged;

        public HeatmapPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            DateTime today = DateTime.Today;
            _month = new DateTime(today.Year, today.Month, 1);
        }

        public string Mode
        {
            get { return _mode; }
        }

        public DateTime Month
        {
            get { return _month; }
        }

        public void SetMode(string mode)
        {
            string normalized = string.Equals(mode, "all", StringComparison.Ordinal) ? "all" : "habit";
            if (!string.Equals(_mode, normalized, StringComparison.Ordinal))
            {
                _mode = normalized;
                this.Invalidate();
            }
        }

        public void SetData(Habit habit, List<Habit> allHabits)
        {
            _habit = habit;
            _allHabits = allHabits == null ? new List<Habit>() : allHabits;
            this.Invalidate();
        }

        public void ShowCurrentMonth()
        {
            DateTime today = DateTime.Today;
            _month = new DateTime(today.Year, today.Month, 1);
            this.Invalidate();
        }

        // ---------------- 布局 ----------------

        private void BuildLayout()
        {
            _cells.Clear();

            _prevRect = new Rectangle(0, 6, 24, 22);
            _nextRect = new Rectangle(136, 6, 24, 22);
            _todayRect = new Rectangle(166, 6, 68, 22);

            int pillW = 132;
            int pillX = Math.Max(_todayRect.Right + 8, Width - pillW - 2);
            _modeHabitRect = new Rectangle(pillX, 6, 66, 22);
            _modeAllRect = new Rectangle(pillX + 66, 6, 66, 22);

            DateTime first = new DateTime(_month.Year, _month.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7; // 周一为第一列
            DateTime start = first.AddDays(-offset);

            int gridTop = HeaderHeight + WeekHeaderHeight;
            int gridBottom = Height - FooterHeight;
            int gridHeight = gridBottom - gridTop;
            int gridWidth = Width;
            if (gridHeight < 20 || gridWidth < 60)
            {
                return;
            }

            int daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
            _rows = (offset + daysInMonth + 6) / 7;
            if (_rows < 4)
            {
                _rows = 4;
            }
            if (_rows > 6)
            {
                _rows = 6;
            }

            int gapX = 6;
            int gapY = 6;

            int cellH = (gridHeight - gapY * (_rows - 1)) / _rows;
            int byWidth = (gridWidth - gapX * 6) / 7;

            // 日历卡片通常"扁而宽"，正方形格子会在两侧留下大片空白。
            // 因此在高度允许的前提下适度拉宽单元格（最多 2.4:1）：既填满空间，看起来仍像日历。
            int maxByAspect = (int)Math.Round(cellH * 2.4);
            int cellW = byWidth < maxByAspect ? byWidth : maxByAspect;
            if (cellW < 10)
            {
                cellW = 10;
            }
            if (cellH < 10)
            {
                cellH = 10;
            }

            int totalW = cellW * 7 + gapX * 6;
            int totalH = cellH * _rows + gapY * (_rows - 1);
            int originX = (gridWidth - totalW) / 2;
            int originY = gridTop + Math.Max(0, (gridHeight - totalH) / 2);

            for (int k = 0; k < _rows * 7; k++)
            {
                DateTime day = start.AddDays(k);
                int row = k / 7;
                int col = k % 7;

                Cell cell = new Cell();
                cell.Day = day;
                cell.InMonth = day.Year == _month.Year && day.Month == _month.Month;
                cell.Rect = new Rectangle(
                    originX + col * (cellW + gapX),
                    originY + row * (cellH + gapY),
                    cellW, cellH);
                _cells.Add(cell);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            BuildLayout();
            base.OnResize(e);
        }

        // ---------------- 绘制 ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor == Color.Transparent ? Theme.Card : BackColor);

            BuildLayout();

            DrawHeader(g);
            DrawWeekHeader(g);
            DrawGrid(g);
            DrawFooter(g);
        }

        private void DrawHeader(Graphics g)
        {
            DrawChevron(g, _prevRect, false);
            DrawChevron(g, _nextRect, true);

            Rectangle titleRect = new Rectangle(28, 6, 104, 22);
            Theme.DrawText(g, Dates.ShortChinese(_month), Theme.H2, Theme.TextMain, titleRect, Theme.LeftCenter);

            DateTime today = DateTime.Today;
            bool isCurrentMonth = _month.Year == today.Year && _month.Month == today.Month;
            if (!isCurrentMonth)
            {
                Theme.DrawText(g, "回到本月", Theme.Tiny, Theme.Primary, _todayRect, Theme.LeftCenter);
            }

            // 视角切换胶囊
            Theme.FillRounded(g, new Rectangle(_modeHabitRect.X, 6, 132, 22), 6, Theme.HeatEmpty);

            Rectangle active = string.Equals(_mode, "all", StringComparison.Ordinal) ? _modeAllRect : _modeHabitRect;
            Theme.FillRounded(g, active, 6, Theme.Primary);

            Theme.DrawText(g, "本习惯",
                string.Equals(_mode, "all", StringComparison.Ordinal) ? Theme.Base : Theme.Bold,
                string.Equals(_mode, "all", StringComparison.Ordinal) ? Theme.TextMuted : Color.White,
                _modeHabitRect, Theme.CenterAll);

            Theme.DrawText(g, "全部习惯",
                string.Equals(_mode, "all", StringComparison.Ordinal) ? Theme.Bold : Theme.Base,
                string.Equals(_mode, "all", StringComparison.Ordinal) ? Color.White : Theme.TextMuted,
                _modeAllRect, Theme.CenterAll);
        }

        private static void DrawChevron(Graphics g, Rectangle r, bool pointRight)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            PointF[] pts = pointRight
                ? new PointF[]
                  {
                      new PointF(cx - 2.5f, cy - 5f),
                      new PointF(cx + 2.5f, cy),
                      new PointF(cx - 2.5f, cy + 5f)
                  }
                : new PointF[]
                  {
                      new PointF(cx + 2.5f, cy - 5f),
                      new PointF(cx - 2.5f, cy),
                      new PointF(cx + 2.5f, cy + 5f)
                  };

            using (Pen pen = new Pen(Theme.TextMuted, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        private void DrawWeekHeader(Graphics g)
        {
            if (_cells.Count == 0)
            {
                return;
            }

            string[] names = new string[] { "一", "二", "三", "四", "五", "六", "日" };
            int y = HeaderHeight;
            for (int col = 0; col < 7; col++)
            {
                Rectangle box = new Rectangle(_cells[col].Rect.X, y, _cells[col].Rect.Width, WeekHeaderHeight);
                Theme.DrawText(g, names[col], Theme.Tiny, Theme.TextFaint, box, Theme.CenterAll);
            }
        }

        private void DrawGrid(Graphics g)
        {
            if (_cells.Count == 0)
            {
                return;
            }

            DateTime today = DateTime.Today;
            Color habitColor = _habit == null ? Theme.Primary : Theme.Hex(_habit.Color);

            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                bool future = cell.Day.Date > today;
                Color fill;
                bool interactive;

                if (!cell.InMonth)
                {
                    fill = Theme.Blend(Theme.HeatEmpty, Theme.Card, 0.55);
                    interactive = false;
                }
                else if (future)
                {
                    fill = Theme.Blend(Theme.HeatEmpty, Theme.Card, 0.35);
                    interactive = false;
                }
                else if (string.Equals(_mode, "all", StringComparison.Ordinal))
                {
                    double ratio = Stats.DayCompletionRatio(_allHabits, cell.Day);
                    int level = Stats.HeatLevel(ratio);
                    fill = level == 0
                        ? Theme.HeatEmpty
                        : Theme.Blend(Theme.PrimarySoft, Theme.Primary, level / 4.0);
                    interactive = true;
                }
                else
                {
                    bool on = _habit != null && _habit.IsChecked(Dates.Key(cell.Day));
                    fill = on ? habitColor : Theme.HeatEmpty;
                    interactive = true;
                }

                Theme.FillRounded(g, cell.Rect, 6, fill);

                // 日期数字
                Color textColor = cell.InMonth && interactive ? Theme.ContrastText(fill) : Theme.TextFaint;
                Rectangle textRect = new Rectangle(cell.Rect.X, cell.Rect.Y, cell.Rect.Width, cell.Rect.Height);
                Theme.DrawText(g, cell.Day.Day.ToString(), Theme.Tiny, textColor, textRect, Theme.CenterAll);

                // 本习惯模式的额外标记：心情圆点 + 备注三角
                if (cell.InMonth && string.Equals(_mode, "habit", StringComparison.Ordinal) && _habit != null)
                {
                    DayEntry entry;
                    if (_habit.Entries.TryGetValue(Dates.Key(cell.Day), out entry) && entry != null)
                    {
                        if (entry.Mood.HasValue && cell.Rect.Width >= 22)
                        {
                            int d = 7;
                            Rectangle moodDot = new Rectangle(
                                cell.Rect.Right - d - 3, cell.Rect.Y + 3, d, d);
                            using (SolidBrush brush = new SolidBrush(Moods.Color(entry.Mood.Value)))
                            {
                                g.FillEllipse(brush, moodDot);
                            }
                            using (Pen pen = new Pen(Color.White, 1f))
                            {
                                g.DrawEllipse(pen, moodDot);
                            }
                        }

                        if (!string.IsNullOrEmpty(entry.Note) && cell.Rect.Width >= 18)
                        {
                            PointF[] tri = new PointF[]
                            {
                                new PointF(cell.Rect.Right - 3, cell.Rect.Bottom - 3),
                                new PointF(cell.Rect.Right - 3, cell.Rect.Bottom - 11),
                                new PointF(cell.Rect.Right - 11, cell.Rect.Bottom - 3)
                            };
                            using (SolidBrush brush = new SolidBrush(
                                Theme.Blend(Theme.ContrastText(fill), fill, 0.35)))
                            {
                                g.FillPolygon(brush, tri);
                            }
                        }
                    }
                }

                // 今天 / 悬停描边
                bool isToday = cell.Day.Date == today;
                if (isToday)
                {
                    Theme.DrawRoundedBorder(g, cell.Rect, 6, Theme.Blend(habitColor, Color.Black, 0.25), 2f);
                }
                if (i == _hoverIndex && cell.InMonth && interactive)
                {
                    Theme.DrawRoundedBorder(g, cell.Rect, 6, Theme.Primary, 2f);
                }
            }
        }

        private void DrawFooter(Graphics g)
        {
            int y = Height - FooterHeight + 4;
            if (y < 0)
            {
                return;
            }

            string left = string.Equals(_mode, "all", StringComparison.Ordinal)
                ? "颜色越深，当天完成的习惯越多"
                : "点击任意日期即可补打卡 / 取消打卡";
            Rectangle leftRect = new Rectangle(0, y, Math.Max(10, Width / 2), FooterHeight - 4);
            Theme.DrawText(g, left, Theme.Tiny, Theme.TextFaint, leftRect, Theme.LeftCenter);

            string right = BuildDetailText();
            Rectangle rightRect = new Rectangle(Width / 2, y, Math.Max(10, Width - Width / 2 - 2), FooterHeight - 4);
            Theme.DrawText(g, right, Theme.Tiny, Theme.TextMuted, rightRect, Theme.RightCenter);
        }

        private string BuildDetailText()
        {
            DateTime today = DateTime.Today;

            if (_hoverIndex >= 0 && _hoverIndex < _cells.Count && _cells[_hoverIndex].InMonth)
            {
                DateTime day = _cells[_hoverIndex].Day;
                if (string.Equals(_mode, "all", StringComparison.Ordinal))
                {
                    double ratio = Stats.DayCompletionRatio(_allHabits, day);
                    return string.Format("{0} · 完成 {1}%",
                        Dates.MonthDay(day), (int)Math.Round(ratio * 100.0));
                }

                if (_habit != null)
                {
                    string key = Dates.Key(day);
                    bool on = _habit.IsChecked(key);
                    if (!on)
                    {
                        return Dates.MonthDay(day) + " · 未打卡";
                    }

                    string text = Dates.MonthDay(day) + " · 已打卡";
                    DayEntry entry;
                    if (_habit.Entries.TryGetValue(key, out entry) && entry != null)
                    {
                        if (entry.Mood.HasValue)
                        {
                            text += " · 心情：" + Moods.Label(entry.Mood.Value);
                        }
                        if (!string.IsNullOrEmpty(entry.Note))
                        {
                            string note = entry.Note.Replace("\r", " ").Replace("\n", " ");
                            if (note.Length > 22)
                            {
                                note = note.Substring(0, 22) + "…";
                            }
                            text += " · 备注：" + note;
                        }
                    }
                    return text;
                }
            }

            if (_habit == null)
            {
                return "";
            }

            int monthCount = Stats.CountInMonth(_habit.DateSet(), _month.Year, _month.Month);
            int daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
            if (_month.Year == today.Year && _month.Month == today.Month)
            {
                return string.Format("{0} 月已打卡 {1} / {2} 天", _month.Month, monthCount, today.Day);
            }
            return string.Format("{0} 月已打卡 {1} / {2} 天", _month.Month, monthCount, daysInMonth);
        }

        // ---------------- 交互 ----------------

        private int CellIndexAt(Point p)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].Rect.Contains(p))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = CellIndexAt(e.Location);
            bool overHeader = _prevRect.Contains(e.Location) || _nextRect.Contains(e.Location)
                || _modeHabitRect.Contains(e.Location) || _modeAllRect.Contains(e.Location)
                || (!_todayRect.IsEmpty && _month.Year != DateTime.Today.Year
                    && _todayRect.Contains(e.Location));

            bool clickable = idx >= 0 && _cells[idx].InMonth && _cells[idx].Day.Date <= DateTime.Today;
            Cursor = clickable || overHeader ? Cursors.Hand : Cursors.Default;

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

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Point p = e.Location;

            if (_prevRect.Contains(p))
            {
                _month = _month.AddMonths(-1);
                _hoverIndex = -1;
                this.Invalidate();
                base.OnMouseClick(e);
                return;
            }

            if (_nextRect.Contains(p))
            {
                _month = _month.AddMonths(1);
                _hoverIndex = -1;
                this.Invalidate();
                base.OnMouseClick(e);
                return;
            }

            DateTime today = DateTime.Today;
            if (_month.Year != today.Year || _month.Month != today.Month)
            {
                if (_todayRect.Contains(p))
                {
                    ShowCurrentMonth();
                    base.OnMouseClick(e);
                    return;
                }
            }

            if (_modeHabitRect.Contains(p) || _modeAllRect.Contains(p))
            {
                string wanted = _modeHabitRect.Contains(p) ? "habit" : "all";
                if (!string.Equals(_mode, wanted, StringComparison.Ordinal))
                {
                    _mode = wanted;
                    this.Invalidate();
                    EventHandler handler = ModeChanged;
                    if (handler != null)
                    {
                        handler(this, EventArgs.Empty);
                    }
                }
                base.OnMouseClick(e);
                return;
            }

            int idx = CellIndexAt(p);
            if (idx >= 0)
            {
                Cell cell = _cells[idx];
                if (cell.InMonth && cell.Day.Date <= today)
                {
                    EventHandler<DateTime> handler = DayToggled;
                    if (handler != null)
                    {
                        handler(this, cell.Day.Date);
                    }
                }
            }

            base.OnMouseClick(e);
        }
    }
}
