using System;
using System.Collections.Generic;

namespace DakaHelper
{
    /// <summary>
    /// 统计逻辑。全部为纯函数，不触碰界面与磁盘，便于单元测试。
    ///
    /// 关键语义（与 README 保持一致）：
    ///   当前连续 —— 今天已打卡则从今天往前数；今天未打卡但昨天已打卡，则从昨天往前数
    ///                （"今日宽限"：当天还没打卡不算断，避免早上打开软件就看到归零）；
    ///                两天都没打卡则为 0。
    ///   最长连续 —— 所有连续区段长度的最大值。
    ///   本月完成率 —— 分母 = max(本月1日, 创建日) 到 今天（含）的天数；分子 = 该区间打卡天数。
    /// </summary>
    public static class Stats
    {
        /// <summary>当前连续天数。</summary>
        public static int CurrentStreak(ICollection<string> dates, DateTime today)
        {
            HashSet<string> set = AsSet(dates);
            DateTime cursor = today.Date;

            if (!set.Contains(Dates.Key(cursor)))
            {
                cursor = cursor.AddDays(-1);
                if (!set.Contains(Dates.Key(cursor)))
                {
                    return 0;
                }
            }

            int n = 0;
            while (set.Contains(Dates.Key(cursor)))
            {
                n++;
                cursor = cursor.AddDays(-1);
            }
            return n;
        }

        /// <summary>历史最长连续天数。</summary>
        public static int LongestStreak(ICollection<string> dates)
        {
            HashSet<string> set = AsSet(dates);
            if (set.Count == 0)
            {
                return 0;
            }

            List<DateTime> list = new List<DateTime>();
            foreach (string s in set)
            {
                DateTime d;
                if (Dates.TryParse(s, out d))
                {
                    list.Add(d.Date);
                }
            }
            if (list.Count == 0)
            {
                return 0;
            }

            list.Sort();

            int best = 0;
            int run = 0;
            DateTime prev = DateTime.MinValue;

            for (int i = 0; i < list.Count; i++)
            {
                DateTime d = list[i];
                if (i == 0)
                {
                    run = 1;
                }
                else if (d.Equals(prev.AddDays(1)))
                {
                    run++;
                }
                else if (d.Equals(prev))
                {
                    // 理论上不会出现（日期键唯一），保留以防脏数据
                }
                else
                {
                    run = 1;
                }

                if (run > best)
                {
                    best = run;
                }
                prev = d;
            }
            return best;
        }

        /// <summary>累计打卡天数。</summary>
        public static int TotalCount(ICollection<string> dates)
        {
            return AsSet(dates).Count;
        }

        /// <summary>本月完成率，0.0 ~ 1.0。</summary>
        public static double MonthRate(ICollection<string> dates, DateTime createdAt, DateTime today)
        {
            DateTime to = today.Date;
            DateTime monthStart = new DateTime(to.Year, to.Month, 1);
            DateTime created = createdAt.Date;
            DateTime from = created > monthStart ? created : monthStart;

            if (from > to)
            {
                return 0.0;
            }

            int elapsed = (int)(to - from).TotalDays + 1;
            if (elapsed <= 0)
            {
                return 0.0;
            }

            HashSet<string> set = AsSet(dates);
            int hits = 0;
            DateTime cursor = from;
            for (int i = 0; i < elapsed; i++)
            {
                if (set.Contains(Dates.Key(cursor)))
                {
                    hits++;
                }
                cursor = cursor.AddDays(1);
            }
            return (double)hits / elapsed;
        }

        /// <summary>指定月份的已完成天数。</summary>
        public static int CountInMonth(ICollection<string> dates, int year, int month)
        {
            HashSet<string> set = AsSet(dates);
            int n = 0;
            foreach (string s in set)
            {
                DateTime d;
                if (Dates.TryParse(s, out d) && d.Year == year && d.Month == month)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>最近 n 天（含今天）的打卡布尔序列，索引 0 是最早的一天。</summary>
        public static bool[] RecentDays(ICollection<string> dates, DateTime today, int n)
        {
            HashSet<string> set = AsSet(dates);
            bool[] result = new bool[n];
            DateTime start = today.Date.AddDays(-(n - 1));
            for (int i = 0; i < n; i++)
            {
                result[i] = set.Contains(Dates.Key(start.AddDays(i)));
            }
            return result;
        }

        /// <summary>
        /// 某一天在“全部习惯”视角下的完成比例（0.0 ~ 1.0）。
        /// 只统计在该日期当天或之前就已创建的习惯。
        /// </summary>
        public static double DayCompletionRatio(List<Habit> habits, DateTime day)
        {
            string key = Dates.Key(day);
            int eligible = 0;
            int done = 0;
            foreach (Habit h in habits)
            {
                if (h.CreatedDate() <= day.Date)
                {
                    eligible++;
                    if (h.IsChecked(key))
                    {
                        done++;
                    }
                }
            }
            if (eligible == 0)
            {
                return 0.0;
            }
            return (double)done / eligible;
        }

        /// <summary>把 0.0~1.0 的比例映射到热力档位 0..4。</summary>
        public static int HeatLevel(double ratio)
        {
            if (ratio <= 0.0)
            {
                return 0;
            }
            if (ratio <= 0.25)
            {
                return 1;
            }
            if (ratio <= 0.50)
            {
                return 2;
            }
            if (ratio <= 0.75)
            {
                return 3;
            }
            return 4;
        }

        private static HashSet<string> AsSet(ICollection<string> dates)
        {
            HashSet<string> set = dates as HashSet<string>;
            if (set != null)
            {
                return set;
            }
            set = new HashSet<string>(StringComparer.Ordinal);
            if (dates != null)
            {
                foreach (string s in dates)
                {
                    set.Add(s);
                }
            }
            return set;
        }
    }
}
