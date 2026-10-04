using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 指标卡 + 最近 30 天条带。四个指标：当前连续 / 最长连续 / 累计打卡 / 本月完成率。
    /// </summary>
    public class StatsStrip : Control
    {
        private const int TilesBottom = 52;
        private const int StripTop = 60;
        private const int StripHeight = 20;

        private Habit _habit;

        public StatsStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 84;
        }

        public void SetHabit(Habit habit)
        {
            _habit = habit;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor == Color.Transparent ? Theme.Card : BackColor);

            if (_habit == null)
            {
                Rectangle hint = new Rectangle(0, 0, Math.Max(10, Width), Math.Max(10, Height));
                Theme.DrawText(g, "请先在左侧选择或新建一个习惯项目", Theme.Base,
                    Theme.TextFaint, hint, Theme.CenterAll);
                return;
            }

            DateTime today = DateTime.Today;
            HashSet<string> dates = _habit.DateSet();

            string[] values = new string[]
            {
                Stats.CurrentStreak(dates, today).ToString() + " 天",
                Stats.LongestStreak(dates).ToString() + " 天",
                Stats.TotalCount(dates).ToString() + " 次",
                ((int)Math.Round(Stats.MonthRate(dates, _habit.CreatedDate(), today) * 100.0)).ToString() + "%"
            };
            string[] labels = new string[] { "当前连续", "最长连续", "累计打卡", "本月完成率" };
            Color[] colors = new Color[]
            {
                Theme.Primary, Theme.Hex("#7C4DFF"), Theme.Success, Theme.Hex("#E8894A")
            };

            int tileW = Width / 4;
            if (tileW < 20)
            {
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                Rectangle tile = new Rectangle(i * tileW, 0, tileW, TilesBottom);

                Rectangle numRect = new Rectangle(tile.X, 2, tileW, 28);
                Theme.DrawText(g, values[i], Theme.Num, colors[i], numRect, Theme.CenterAll);

                Rectangle labRect = new Rectangle(tile.X, 30, tileW, 16);
                Theme.DrawText(g, labels[i], Theme.Tiny, Theme.TextMuted, labRect, Theme.CenterAll);

                if (i > 0)
                {
                    using (Pen pen = new Pen(Theme.Divider, 1f))
                    {
                        g.DrawLine(pen, tile.X, 8, tile.X, TilesBottom - 8);
                    }
                }
            }

            // 最近 30 天条带
            Rectangle stripLabel = new Rectangle(0, StripTop, 62, StripHeight);
            Theme.DrawText(g, "最近 30 天", Theme.Tiny, Theme.TextMuted, stripLabel, Theme.LeftCenter);

            bool[] recent = Stats.RecentDays(dates, today, 30);
            Color habitColor = Theme.Hex(_habit.Color);

            int left = 64;
            int avail = Width - left - 2;
            int gap = 2;
            int cellW = (avail - gap * 29) / 30;
            if (cellW < 3)
            {
                cellW = 3;
                gap = 0;
            }

            for (int i = 0; i < 30; i++)
            {
                int x = left + i * (cellW + gap);
                if (x + cellW > Width)
                {
                    break;
                }

                Rectangle cell = new Rectangle(x, StripTop, cellW, StripHeight);
                bool on = recent[i];
                Theme.FillRounded(g, cell, 2, on ? habitColor : Theme.HeatEmpty);

                if (i == 29)
                {
                    Theme.DrawRoundedBorder(g, cell, 2, Theme.Blend(habitColor, Color.Black, 0.2), 1.4f);
                }
            }
        }
    }
}
