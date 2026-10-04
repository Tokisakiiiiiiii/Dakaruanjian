using System;
using System.Collections.Generic;
using System.Globalization;

namespace DakaHelper
{
    /// <summary>
    /// 日期键工具。统一使用本地日期字符串 yyyy-MM-dd，
    /// 绝不比较 Ticks / DateTimeOffset，以规避夏令时与跨时区问题。
    /// </summary>
    public static class Dates
    {
        public const string Format = "yyyy-MM-dd";

        private static readonly string[] WeekNames =
            new string[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };

        public static string Key(DateTime d)
        {
            return d.ToString(Format, CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string s, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            return DateTime.TryParseExact(s, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result);
        }

        public static string TodayKey()
        {
            return Key(DateTime.Today);
        }

        /// <summary>例如：2025年6月12日 星期四</summary>
        public static string LongChinese(DateTime d)
        {
            return string.Format("{0}年{1}月{2}日 {3}",
                d.Year, d.Month, d.Day, WeekNames[(int)d.DayOfWeek]);
        }

        /// <summary>例如：2025年6月</summary>
        public static string ShortChinese(DateTime d)
        {
            return string.Format("{0}年{1}月", d.Year, d.Month);
        }

        /// <summary>例如：6月12日</summary>
        public static string MonthDay(DateTime d)
        {
            return string.Format("{0}月{1}日", d.Month, d.Day);
        }
    }

    /// <summary>某一天的打卡记录。键存在即代表当天已打卡。</summary>
    public class DayEntry
    {
        public string Date;
        /// <summary>1..5，null 表示未记录心情。</summary>
        public int? Mood;
        public string Note;

        public DayEntry()
        {
            Note = "";
        }

        public DayEntry(string date)
        {
            Date = date;
            Note = "";
        }
    }

    /// <summary>一个习惯项目。</summary>
    public class Habit
    {
        public string Id;
        public string Name;
        /// <summary>#RRGGBB</summary>
        public string Color;
        /// <summary>yyyy-MM-dd</summary>
        public string CreatedAt;
        public int Order;
        /// <summary>日期键 -> 当天记录。用 SortedDictionary 保证写出顺序稳定。</summary>
        public SortedDictionary<string, DayEntry> Entries;

        public Habit()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "";
            Color = "#2F6FED";
            CreatedAt = Dates.TodayKey();
            Order = 0;
            Entries = new SortedDictionary<string, DayEntry>(StringComparer.Ordinal);
        }

        public bool IsChecked(string dateKey)
        {
            return Entries.ContainsKey(dateKey);
        }

        public HashSet<string> DateSet()
        {
            return new HashSet<string>(Entries.Keys, StringComparer.Ordinal);
        }

        public DateTime CreatedDate()
        {
            DateTime d;
            if (Dates.TryParse(CreatedAt, out d))
            {
                return d.Date;
            }
            return DateTime.Today;
        }

        public string EffectiveName()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return "未命名习惯";
            }
            return Name;
        }
    }

    /// <summary>整个应用的数据根。</summary>
    public class AppData
    {
        public int Schema;
        public List<Habit> Habits;
        /// <summary>"habit" 本习惯模式 / "all" 全部习惯模式</summary>
        public string HeatmapMode;

        public AppData()
        {
            Schema = 1;
            Habits = new List<Habit>();
            HeatmapMode = "habit";
        }

        private static int CompareByOrder(Habit a, Habit b)
        {
            int c = a.Order.CompareTo(b.Order);
            if (c != 0)
            {
                return c;
            }
            return string.CompareOrdinal(a.Id, b.Id);
        }

        public void SortHabits()
        {
            Habits.Sort(CompareByOrder);
        }

        public void NormalizeOrder()
        {
            SortHabits();
            for (int i = 0; i < Habits.Count; i++)
            {
                Habits[i].Order = i;
            }
        }

        public Habit Find(string id)
        {
            if (id == null)
            {
                return null;
            }
            foreach (Habit h in Habits)
            {
                if (string.Equals(h.Id, id, StringComparison.Ordinal))
                {
                    return h;
                }
            }
            return null;
        }

        public int CheckedCountToday()
        {
            string today = Dates.TodayKey();
            int n = 0;
            foreach (Habit h in Habits)
            {
                if (h.IsChecked(today))
                {
                    n++;
                }
            }
            return n;
        }
    }
}
