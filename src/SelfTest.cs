#if SELFTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DakaHelper
{
    /// <summary>
    /// 逻辑单元测试。只在定义了 SELFTEST 的「打卡助手-测试.exe」里编译出内容。
    /// 覆盖统计语义、日期边界、JSON 往返、原子保存与损坏恢复。
    /// </summary>
    internal static class SelfTest
    {
        private static int _passed;
        private static int _failed;
        private static readonly StringBuilder Log = new StringBuilder();
        private static string _scratchRoot;

        public static int Run()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (Exception)
            {
            }

            SafeGroup("日期工具", RunDateTests);
            SafeGroup("当前连续天数", RunStreakTests);
            SafeGroup("最长连续天数", RunLongestTests);
            SafeGroup("本月完成率", RunRateTests);
            SafeGroup("日期边界", RunBoundaryTests);
            SafeGroup("热力档位映射", RunHeatLevelTests);
            SafeGroup("全部习惯完成比例", RunRatioTests);
            SafeGroup("打卡切换幂等性", RunToggleTests);
            SafeGroup("JSON 往返与容错", RunJsonTests);
            SafeGroup("保存 / 读取 / 备份", RunStorageTests);
            SafeGroup("损坏文件恢复", RunCorruptTests);
            SafeGroup("设置与数据位置", RunSettingsTests);
            SafeGroup("打开文件夹的兜底", RunFolderOpenerTests);

            try
            {
                Cleanup();
            }
            catch (Exception)
            {
            }

            string summary = string.Format("通过 {0} 项，失败 {1} 项。", _passed, _failed);

            // 报告始终落盘：即便控制台编码或重定向出问题，也一定能拿到完整结果
            string reportPath = Path.Combine(Environment.CurrentDirectory, "_selftest_report.txt");
            string report = "打卡助手 · 逻辑自测\r\n"
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n"
                + Log.ToString() + "\r\n" + summary + "\r\n";
            try
            {
                File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }

            try
            {
                Console.WriteLine("打卡助手 · 逻辑自测");
                Console.WriteLine();
                Console.WriteLine(Log.ToString());
                Console.WriteLine(summary);
                Console.WriteLine("报告文件：" + reportPath);
            }
            catch (Exception)
            {
            }

            return _failed == 0 ? 0 : 1;
        }

        /// <summary>跑一个测试组；组内抛出任何异常都不中断整轮自测。</summary>
        private static void SafeGroup(string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                _failed++;
                Log.AppendLine("  × 测试组「" + name + "」抛出异常：" + Describe(ex));
            }
        }

        /// <summary>异常描述。连 ToString() 都可能失败，所以逐层防御式拼接。</summary>
        private static string Describe(Exception ex)
        {
            if (ex == null)
            {
                return "(null)";
            }

            StringBuilder sb = new StringBuilder();
            Exception cur = ex;
            int depth = 0;
            while (cur != null && depth < 6)
            {
                if (depth > 0)
                {
                    sb.Append("  <-  ");
                }
                sb.Append(cur.GetType().FullName);
                try
                {
                    sb.Append(": ").Append(cur.Message);
                }
                catch (Exception)
                {
                    sb.Append(": (消息无法读取)");
                }
                try
                {
                    cur = cur.InnerException;
                }
                catch (Exception)
                {
                    cur = null;
                }
                depth++;
            }
            return sb.ToString();
        }

        // ---------------- 测试组 ----------------

        private static void RunDateTests()
        {
            Section("日期工具");

            Check("Key 格式为 yyyy-MM-dd", Dates.Key(new DateTime(2025, 6, 5)) == "2025-06-05");

            DateTime parsed;
            Check("解析合法日期",
                Dates.TryParse("2025-06-05", out parsed) && parsed.Year == 2025
                && parsed.Month == 6 && parsed.Day == 5);
            Check("拒绝单位数月/日", !Dates.TryParse("2025-6-5", out parsed));
            Check("拒绝空串", !Dates.TryParse("", out parsed));
            Check("拒绝乱码", !Dates.TryParse("hello", out parsed));
            Check("拒绝不存在的日期 2/30", !Dates.TryParse("2025-02-30", out parsed));
            Check("接受闰日 2024-02-29", Dates.TryParse("2024-02-29", out parsed));
            Check("拒绝非闰年 2025-02-29", !Dates.TryParse("2025-02-29", out parsed));

            Check("中文长日期",
                Dates.LongChinese(new DateTime(2025, 6, 12)) == "2025年6月12日 星期四");
            Check("中文短日期",
                Dates.ShortChinese(new DateTime(2025, 6, 12)) == "2025年6月");
        }

        private static void RunStreakTests()
        {
            Section("当前连续天数");
            DateTime t = new DateTime(2025, 6, 12);

            CheckEqual("空集合", 0, Stats.CurrentStreak(Set(), t));
            CheckEqual("仅今天", 1, Stats.CurrentStreak(Set(t), t));
            CheckEqual("今天 + 昨天", 2, Stats.CurrentStreak(Set(t, t.AddDays(-1)), t));
            CheckEqual("仅昨天（今日宽限，不算断）", 1, Stats.CurrentStreak(Set(t.AddDays(-1)), t));
            CheckEqual("昨天 + 前天（今日宽限）", 2,
                Stats.CurrentStreak(Set(t.AddDays(-1), t.AddDays(-2)), t));
            CheckEqual("今天打卡但昨天缺", 1, Stats.CurrentStreak(Set(t, t.AddDays(-2)), t));
            CheckEqual("前天及更早已断", 0,
                Stats.CurrentStreak(Set(t.AddDays(-2), t.AddDays(-3)), t));
            CheckEqual("连续 7 天至今天", 7, Stats.CurrentStreak(Set(
                t, t.AddDays(-1), t.AddDays(-2), t.AddDays(-3),
                t.AddDays(-4), t.AddDays(-5), t.AddDays(-6)), t));
            CheckEqual("只有未来日期", 0, Stats.CurrentStreak(Set(t.AddDays(1)), t));
        }

        private static void RunLongestTests()
        {
            Section("最长连续天数");
            DateTime t = new DateTime(2025, 6, 12);

            CheckEqual("空集合", 0, Stats.LongestStreak(Set()));
            CheckEqual("单日", 1, Stats.LongestStreak(Set(t)));
            CheckEqual("连续 5 天", 5, Stats.LongestStreak(Set(
                t, t.AddDays(-1), t.AddDays(-2), t.AddDays(-3), t.AddDays(-4))));
            CheckEqual("两段取较长（3 天 vs 5 天）", 5, Stats.LongestStreak(Set(
                t, t.AddDays(-1), t.AddDays(-2),
                t.AddDays(-5), t.AddDays(-6), t.AddDays(-7), t.AddDays(-8), t.AddDays(-9))));
            CheckEqual("两段取较长（2 天 vs 2 天）", 2, Stats.LongestStreak(Set(
                t, t.AddDays(-1), t.AddDays(-7), t.AddDays(-8))));
            CheckEqual("乱序输入结果不变", 4, Stats.LongestStreak(Set(
                t.AddDays(-3), t, t.AddDays(-2), t.AddDays(-1))));
            CheckEqual("重复日期只算一次", 1, Stats.LongestStreak(Set(t, t)));
        }

        private static void RunRateTests()
        {
            Section("本月完成率");
            DateTime today = new DateTime(2025, 6, 12);
            DateTime monthStart = new DateTime(2025, 6, 1);

            HashSet<string> six = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i <= 6; i++)
            {
                six.Add(Dates.Key(new DateTime(2025, 6, i)));
            }

            CheckNear("月初创建，12 天里打卡 6 天 = 50%", 0.5,
                Stats.MonthRate(six, monthStart, today));
            CheckNear("上月创建时分母从本月 1 日起算", 0.5,
                Stats.MonthRate(six, new DateTime(2025, 5, 20), today));
            CheckNear("月中创建，5 天里打卡 2 天 = 40%", 0.4,
                Stats.MonthRate(Set(new DateTime(2025, 6, 8), new DateTime(2025, 6, 10)),
                    new DateTime(2025, 6, 8), today));
            CheckNear("创建日在未来 = 0", 0.0,
                Stats.MonthRate(six, new DateTime(2025, 7, 1), today));
            CheckNear("完全没有打卡 = 0", 0.0,
                Stats.MonthRate(Set(), monthStart, today));

            HashSet<string> full = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i <= 12; i++)
            {
                full.Add(Dates.Key(new DateTime(2025, 6, i)));
            }
            CheckNear("全勤 = 100%", 1.0, Stats.MonthRate(full, monthStart, today));

            CheckEqual("本月计数", 6, Stats.CountInMonth(six, 2025, 6));
            CheckEqual("其它月份计数为 0", 0, Stats.CountInMonth(six, 2025, 7));

            bool[] recent = Stats.RecentDays(Set(today, today.AddDays(-3)), today, 30);
            CheckEqual("最近 30 天长度", 30, recent.Length);
            Check("最近 30 天末位为今天", recent[29]);
            Check("最近 30 天第 27 位（3 天前）", recent[26]);
            Check("最近 30 天第 28 位（2 天前）为空", !recent[27]);
        }

        private static void RunBoundaryTests()
        {
            Section("日期边界");
            DateTime t = new DateTime(2025, 6, 12);

            CheckEqual("连续 100 天", 100, Stats.LongestStreak(HundredDays(t)));
            CheckEqual("连续 100 天的当前连续", 100, Stats.CurrentStreak(HundredDays(t), t));

            CheckEqual("闰年 2/29 与 3/1 连续", 2, Stats.LongestStreak(Set(
                new DateTime(2024, 2, 29), new DateTime(2024, 3, 1))));
            CheckEqual("含闰日的三天连续", 3, Stats.LongestStreak(Set(
                new DateTime(2024, 2, 28), new DateTime(2024, 2, 29), new DateTime(2024, 3, 1))));
            CheckEqual("非闰年 2/28 与 3/1 连续", 2, Stats.LongestStreak(Set(
                new DateTime(2025, 2, 28), new DateTime(2025, 3, 1))));
            CheckEqual("跨年连续", 2, Stats.CurrentStreak(Set(
                new DateTime(2024, 12, 31), new DateTime(2025, 1, 1)), new DateTime(2025, 1, 1)));
            CheckEqual("跨月连续", 2, Stats.CurrentStreak(Set(
                new DateTime(2025, 5, 31), new DateTime(2025, 6, 1)), new DateTime(2025, 6, 1)));
            CheckEqual("大月 31 日接次月 1 日", 2, Stats.LongestStreak(Set(
                new DateTime(2025, 1, 31), new DateTime(2025, 2, 1))));
        }

        private static void RunHeatLevelTests()
        {
            Section("热力档位映射");
            CheckEqual("0.00 -> 0", 0, Stats.HeatLevel(0.0));
            CheckEqual("0.01 -> 1", 1, Stats.HeatLevel(0.01));
            CheckEqual("0.25 -> 1", 1, Stats.HeatLevel(0.25));
            CheckEqual("0.26 -> 2", 2, Stats.HeatLevel(0.26));
            CheckEqual("0.50 -> 2", 2, Stats.HeatLevel(0.50));
            CheckEqual("0.51 -> 3", 3, Stats.HeatLevel(0.51));
            CheckEqual("0.75 -> 3", 3, Stats.HeatLevel(0.75));
            CheckEqual("0.76 -> 4", 4, Stats.HeatLevel(0.76));
            CheckEqual("1.00 -> 4", 4, Stats.HeatLevel(1.0));
        }

        private static void RunRatioTests()
        {
            Section("全部习惯完成比例");
            DateTime day = new DateTime(2025, 6, 10);

            List<Habit> habits = new List<Habit>();
            Habit a = MakeHabit("A", new DateTime(2025, 6, 1));
            Habit b = MakeHabit("B", new DateTime(2025, 6, 1));
            habits.Add(a);
            habits.Add(b);

            CheckNear("都没打卡 = 0", 0.0, Stats.DayCompletionRatio(habits, day));
            a.Entries[Dates.Key(day)] = new DayEntry(Dates.Key(day));
            CheckNear("一个打卡 = 0.5", 0.5, Stats.DayCompletionRatio(habits, day));
            b.Entries[Dates.Key(day)] = new DayEntry(Dates.Key(day));
            CheckNear("两个都打卡 = 1.0", 1.0, Stats.DayCompletionRatio(habits, day));

            habits.Add(MakeHabit("C", new DateTime(2025, 6, 20)));
            CheckNear("当天之后才创建的习惯不计入分母", 1.0,
                Stats.DayCompletionRatio(habits, day));
            CheckNear("没有任何合格习惯 = 0", 0.0,
                Stats.DayCompletionRatio(new List<Habit>(), day));
        }

        private static void RunToggleTests()
        {
            Section("打卡切换幂等性");
            DateTime today = DateTime.Today;
            string key = Dates.Key(today);
            string yesterdayKey = Dates.Key(today.AddDays(-1));

            Habit h = MakeHabit("切换测试", today);
            h.Entries[key] = new DayEntry(key);
            h.Entries[key] = new DayEntry(key);
            CheckEqual("同一天连续打卡两次只记一条", 1, h.Entries.Count);
            CheckEqual("累计次数为 1", 1, Stats.TotalCount(h.DateSet()));

            h.Entries.Remove(key);
            CheckEqual("取消后记录为 0", 0, h.Entries.Count);
            CheckEqual("取消后当前连续为 0", 0, Stats.CurrentStreak(h.DateSet(), today));
            h.Entries.Remove(key);
            CheckEqual("重复取消不报错", 0, h.Entries.Count);

            h.Entries[yesterdayKey] = new DayEntry(yesterdayKey);
            CheckEqual("补打卡昨天后当前连续为 1", 1, Stats.CurrentStreak(h.DateSet(), today));

            h.Entries[key] = new DayEntry(key);
            CheckEqual("再打卡今天后当前连续为 2", 2, Stats.CurrentStreak(h.DateSet(), today));
        }

        private static void RunJsonTests()
        {
            Section("JSON 往返与容错");

            AppData original = new AppData();
            original.HeatmapMode = "all";

            Habit h1 = MakeHabit("早起", new DateTime(2025, 6, 1));
            h1.Color = "#2F6FED";

            DayEntry e1 = new DayEntry("2025-06-01");
            e1.Mood = 5;
            e1.Note = "六点半起床，状态很好";
            h1.Entries["2025-06-01"] = e1;

            DayEntry e2 = new DayEntry("2025-06-02");
            e2.Mood = null;
            e2.Note = "";
            h1.Entries["2025-06-02"] = e2;

            DayEntry e3 = new DayEntry("2025-06-03");
            e3.Mood = 2;
            e3.Note = "含\"引号\"、反斜杠\\、换行\n制表符\t以及星号 ★";
            h1.Entries["2025-06-03"] = e3;

            original.Habits.Add(h1);

            Habit h2 = MakeHabit("阅读30分钟", new DateTime(2025, 5, 20));
            h2.Color = "#7C4DFF";
            original.Habits.Add(h2);

            // 软件在加载与增删后都会调用 NormalizeOrder，让 order 变成 0..n-1 的规范值。
            // 这里先规范化，写入/读回才是幂等的（否则断言会误报）。
            original.NormalizeOrder();

            string json = Json.Write(original);
            AppData back = Json.ReadAppData(json);

            string diff = Diff(original, back);
            Check("往返后数据完全一致" + (diff == null ? "" : "（差异：" + diff + "）"), diff == null);
            Check("中文原样保留", json.IndexOf("六点半起床", StringComparison.Ordinal) >= 0);
            Check("换行被转义", json.IndexOf("\\n", StringComparison.Ordinal) >= 0);
            Check("双引号被转义", json.IndexOf("\\\"引号\\\"", StringComparison.Ordinal) >= 0);
            Check("视角模式被保留", back.HeatmapMode == "all");

            // 注意：习惯顺序由 (Order, Id) 决定，而 Id 是随机 GUID。
            // 两个习惯的 Order 相同时，排序会退回比较 Id，因此读回后的顺序
            // 不保证与写入前一致 —— 这里必须按名字查找，不能按下标。
            Habit backEarly = FindByName(back, "早起");
            Check("读回后能找到「早起」", backEarly != null);

            DayEntry backEntry = GetEntry(backEarly, "2025-06-01");
            Check("心情档位被保留", backEntry != null && backEntry.Mood == 5);

            DayEntry backEmpty = GetEntry(backEarly, "2025-06-02");
            Check("空心情保留为 null", backEmpty != null && backEmpty.Mood == null);

            AppData empty = new AppData();
            Check("空数据往返", Diff(empty, Json.ReadAppData(Json.Write(empty))) == null);

            AppData partial = Json.ReadAppData("{\"schema\":1,\"habits\":[{\"name\":\"只有名字\"}]}");
            Check("字段缺失的习惯仍能读出",
                partial.Habits.Count == 1 && partial.Habits[0].Name == "只有名字");
            Check("缺失 id 时自动补一个",
                partial.Habits[0].Id != null && partial.Habits[0].Id.Length > 0);
            Check("缺失颜色时用默认色", partial.Habits[0].Color == "#2F6FED");

            AppData badKey = Json.ReadAppData(
                "{\"habits\":[{\"name\":\"x\",\"entries\":{"
                + "\"not-a-date\":{\"note\":\"a\"},\"2025-06-01\":{\"note\":\"b\"}}}]}");
            Check("非法日期键被丢弃",
                badKey.Habits.Count == 1 && badKey.Habits[0].Entries.Count == 1);
            Check("合法日期键被保留", badKey.Habits[0].Entries.ContainsKey("2025-06-01"));

            AppData badMood = Json.ReadAppData(
                "{\"habits\":[{\"name\":\"x\",\"entries\":{"
                + "\"2025-06-01\":{\"mood\":99,\"note\":\"\"}}}]}");
            Check("越界心情值被丢弃", badMood.Habits[0].Entries["2025-06-01"].Mood == null);

            bool threw = false;
            try
            {
                Json.ReadAppData("这不是 JSON");
            }
            catch (Exception)
            {
                threw = true;
            }
            Check("非法 JSON 抛出异常（由 Storage 负责兜底）", threw);
        }

        private static void RunStorageTests()
        {
            Section("保存 / 读取 / 备份");

            string dir = ScratchDir();
            string path = Path.Combine(dir, "data.json");
            string error;

            AppData d1 = new AppData();
            d1.Habits.Add(MakeHabit("第一版", new DateTime(2025, 6, 1)));

            Check("首次保存成功", Storage.Save(d1, path, out error));
            Check("数据文件已生成", File.Exists(path));
            Check("首次保存后无 .bak", !File.Exists(path + ".bak"));
            Check("保存后无 .tmp 残留", !File.Exists(path + ".tmp"));

            string warning;
            AppData loaded = Storage.Load(path, out warning);
            Check("读取时无警告", warning == null);
            Check("读取内容与写入一致", Diff(d1, loaded) == null);

            d1.Habits[0].Name = "第二版";
            Check("第二次保存成功", Storage.Save(d1, path, out error));
            Check("第二次保存生成 .bak", File.Exists(path + ".bak"));

            AppData bak = Storage.Load(path + ".bak", out warning);
            Check("备份内容是上一版", bak.Habits.Count == 1 && bak.Habits[0].Name == "第一版");
            AppData current = Storage.Load(path, out warning);
            Check("当前文件是新版", current.Habits.Count == 1 && current.Habits[0].Name == "第二版");

            string nested = Path.Combine(dir, "深", "一层", "data.json");
            Check("自动创建多级目录", Storage.Save(d1, nested, out error) && File.Exists(nested));

            AppData none = Storage.Load(Path.Combine(dir, "不存在.json"), out warning);
            Check("文件不存在时返回空数据且无警告",
                none.Habits.Count == 0 && warning == null);

            string bigName = new string('长', 200);
            AppData d2 = new AppData();
            Habit longHabit = MakeHabit(bigName, new DateTime(2025, 6, 1));
            DayEntry longNote = new DayEntry("2025-06-01");
            longNote.Note = new string('备', 2000);
            longNote.Mood = 3;
            longHabit.Entries["2025-06-01"] = longNote;
            d2.Habits.Add(longHabit);
            Check("超长中文名称与备注保存成功", Storage.Save(d2, path, out error));
            Check("超长内容往返一致", Diff(d2, Storage.Load(path, out warning)) == null);
        }

        private static void RunCorruptTests()
        {
            Section("损坏文件恢复");

            string dir = ScratchDir();
            string path = Path.Combine(dir, "corrupt.json");
            File.WriteAllText(path, "{ 这不是合法的 JSON ][", new UTF8Encoding(false));

            string warning;
            AppData data = Storage.Load(path, out warning);
            Check("损坏时给出警告", !string.IsNullOrEmpty(warning));
            Check("损坏时以空数据启动", data.Habits.Count == 0);
            Check("损坏的原文件已被移走", !File.Exists(path));

            string[] corrupts = Directory.GetFiles(dir, "corrupt.json.corrupt-*");
            CheckEqual("生成了 .corrupt-* 备份", 1, corrupts.Length);

            string emptyPath = Path.Combine(dir, "empty.json");
            File.WriteAllText(emptyPath, "", new UTF8Encoding(false));
            string w2;
            AppData emptyData = Storage.Load(emptyPath, out w2);
            Check("空文件视为空数据且不报警",
                emptyData.Habits.Count == 0 && w2 == null);

            string dirPath = Path.Combine(dir, "是一个目录.json");
            Directory.CreateDirectory(dirPath);
            string w3;
            AppData dirData = Storage.Load(dirPath, out w3);
            Check("读取目录不崩溃", dirData.Habits.Count == 0);
        }

        private static void RunSettingsTests()
        {
            Section("设置与数据位置");

            string dir = ScratchDir();
            string configPath = Path.Combine(dir, "config.json");
            string error;

            Check("目录可写性检测通过", Storage.EnsureDirectoryWritable(dir, out error));

            AppSettings settings = new AppSettings();
            settings.DataPath = Path.Combine(dir, "数据", "data.json");
            Check("保存设置成功", Storage.SaveSettings(settings, configPath, out error));
            Check("配置文件已生成", File.Exists(configPath));
            Check("保存设置后无 .tmp 残留", !File.Exists(configPath + ".tmp"));

            string warning;
            AppSettings loaded = Storage.LoadSettings(configPath, out warning);
            Check("读取设置无警告", warning == null);
            Check("数据路径往返一致", loaded != null && loaded.DataPath == settings.DataPath);

            settings.DataPath = Path.Combine(dir, "另一个位置", "data.json");
            Check("覆盖保存成功", Storage.SaveSettings(settings, configPath, out error));
            AppSettings loaded2 = Storage.LoadSettings(configPath, out warning);
            Check("覆盖后内容正确", loaded2 != null && loaded2.DataPath == settings.DataPath);

            AppSettings missing = Storage.LoadSettings(
                Path.Combine(dir, "无此文件.json"), out warning);
            Check("配置不存在时返回默认设置且无警告",
                missing != null && missing.DataPath == null && warning == null);

            string badConfig = Path.Combine(dir, "bad.json");
            File.WriteAllText(badConfig, "{ 这不是合法 json ][", new UTF8Encoding(false));
            string badWarning;
            AppSettings fromBad = Storage.LoadSettings(badConfig, out badWarning);
            Check("配置损坏时给出警告", !string.IsNullOrEmpty(badWarning));
            Check("配置损坏时回退默认设置", fromBad != null && fromBad.DataPath == null);
            CheckEqual("损坏的配置已改名备份", 1,
                Directory.GetFiles(dir, "bad.json.corrupt-*").Length);

            // 路径优先级：命令行 > 配置 > 默认
            AppSettings empty = new AppSettings();
            string def = Storage.ResolveDataPath(null, empty);
            Check("无自定义路径时使用默认位置", def == Storage.DefaultDataPath());
            Check("默认位置位于程序目录下",
                def.StartsWith(Storage.ExeDirectory(), StringComparison.OrdinalIgnoreCase));
            Check("默认位置位于「数据」文件夹内",
                def.IndexOf(Storage.DataFolderName, StringComparison.Ordinal) >= 0);
            Check("默认文件名为 data.json",
                def.EndsWith(Storage.DataFileName, StringComparison.OrdinalIgnoreCase));

            AppSettings custom = new AppSettings();
            custom.DataPath = Path.Combine(dir, "自定义", "data.json");
            Check("有自定义路径时优先使用它",
                Storage.ResolveDataPath(null, custom) == Path.GetFullPath(custom.DataPath));

            string overridePath = Path.Combine(dir, "命令行覆盖.json");
            Check("命令行 --data 优先级最高",
                Storage.ResolveDataPath(overridePath, custom) == Path.GetFullPath(overridePath));

            AppSettings relative = new AppSettings();
            relative.DataPath = Path.Combine("子目录", "data.json");
            Check("相对路径按程序目录解析",
                Storage.ResolveDataPath(null, relative) == Path.GetFullPath(
                    Path.Combine(Storage.ExeDirectory(), "子目录", "data.json")));

            Check("空设置 HasCustomPath 为 false", !empty.HasCustomPath());
            Check("有路径时 HasCustomPath 为 true", custom.HasCustomPath());

            Check("默认情况下会尝试启动资源管理器", new AppSettings().TryLaunchExplorer);

            AppSettings noLaunch = new AppSettings();
            noLaunch.DataPath = Path.Combine(dir, "x", "data.json");
            noLaunch.TryLaunchExplorer = false;
            Check("保存「不尝试启动」设置成功",
                Storage.SaveSettings(noLaunch, configPath, out error));
            AppSettings reloaded = Storage.LoadSettings(configPath, out warning);
            Check("「不尝试启动」往返一致",
                reloaded != null && !reloaded.TryLaunchExplorer && reloaded.DataPath == noLaunch.DataPath);

            // 旧版 config.json 没有这个字段，应当回退为默认的 true
            AppSettings legacy = Json.ReadSettings("{\"dataPath\":\"数据\\\\data.json\"}");
            Check("旧配置缺少字段时默认为尝试启动", legacy != null && legacy.TryLaunchExplorer);

            // 不可写的目标要被识别出来（用非法字符让 CreateDirectory 失败）
            string unwritable;
            bool bad = Storage.EnsureDirectoryWritable(
                Path.Combine(dir, "非法*字符"), out unwritable);
            Check("不可写目录被识别出来", !bad && !string.IsNullOrEmpty(unwritable));

            string nullError;
            Check("路径为空时返回失败", !Storage.EnsureDirectoryWritable(null, out nullError));
        }

        private static void RunFolderOpenerTests()
        {
            Section("打开文件夹的兜底");

            string reported = null;
            bool started = FolderOpener.Open(null, null, delegate(string reason) { reported = reason; });
            Check("空路径不启动也不崩溃", !started);
            Check("空路径会回调失败原因", !string.IsNullOrEmpty(reported));

            Check("复制空路径返回失败", !FolderOpener.CopyPath(null));
            Check("复制空字符串返回失败", !FolderOpener.CopyPath(""));
        }

        // ---------------- 基础设施 ----------------

        private static void Section(string name)
        {
            Log.AppendLine("【" + name + "】");
        }

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                _passed++;
                Log.AppendLine("  √ " + name);
            }
            else
            {
                _failed++;
                Log.AppendLine("  × " + name + "   <<< 失败");
            }
        }

        private static void CheckEqual(string name, int expected, int actual)
        {
            Check(name + "（期望 " + expected + "，实际 " + actual + "）", expected == actual);
        }

        private static void CheckNear(string name, double expected, double actual)
        {
            bool ok = Math.Abs(expected - actual) < 1e-9;
            Check(name + "（期望 " + expected + "，实际 " + actual + "）", ok);
        }

        private static HashSet<string> Set(params DateTime[] days)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < days.Length; i++)
            {
                set.Add(Dates.Key(days[i]));
            }
            return set;
        }

        private static HashSet<string> HundredDays(DateTime end)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 100; i++)
            {
                set.Add(Dates.Key(end.AddDays(-i)));
            }
            return set;
        }

        private static Habit MakeHabit(string name, DateTime created)
        {
            Habit h = new Habit();
            h.Name = name;
            h.CreatedAt = Dates.Key(created);
            return h;
        }

        private static Habit FindByName(AppData data, string name)
        {
            foreach (Habit h in data.Habits)
            {
                if (string.Equals(h.Name, name, StringComparison.Ordinal))
                {
                    return h;
                }
            }
            return null;
        }

        private static DayEntry GetEntry(Habit habit, string dateKey)
        {
            if (habit == null)
            {
                return null;
            }
            DayEntry entry;
            if (habit.Entries.TryGetValue(dateKey, out entry))
            {
                return entry;
            }
            return null;
        }

        /// <summary>比较两份数据，一致返回 null，否则返回第一处差异的描述。</summary>
        private static string Diff(AppData a, AppData b)
        {
            if (a.Schema != b.Schema)
            {
                return "schema 不同";
            }
            if (a.HeatmapMode != b.HeatmapMode)
            {
                return "heatmapMode 不同";
            }
            if (a.Habits.Count != b.Habits.Count)
            {
                return "习惯数量不同 " + a.Habits.Count + " vs " + b.Habits.Count;
            }

            for (int i = 0; i < a.Habits.Count; i++)
            {
                Habit x = a.Habits[i];
                Habit y = b.Habits[i];

                if (x.Id != y.Id)
                {
                    return "id 不同 @" + i;
                }
                if (x.Name != y.Name)
                {
                    return "名称不同 @" + i + " [" + x.Name + "] vs [" + y.Name + "]";
                }
                if (x.Color != y.Color)
                {
                    return "颜色不同 @" + i;
                }
                if (x.CreatedAt != y.CreatedAt)
                {
                    return "创建日期不同 @" + i;
                }
                if (x.Order != y.Order)
                {
                    return "排序值不同 @" + i + " " + x.Order + " vs " + y.Order;
                }
                if (x.Entries.Count != y.Entries.Count)
                {
                    return "记录数不同 @" + i + " " + x.Entries.Count + " vs " + y.Entries.Count;
                }

                foreach (KeyValuePair<string, DayEntry> kv in x.Entries)
                {
                    DayEntry ye;
                    if (!y.Entries.TryGetValue(kv.Key, out ye))
                    {
                        return "缺少日期 " + kv.Key + " @" + i;
                    }
                    if (kv.Value.Mood != ye.Mood)
                    {
                        return "心情不同 " + kv.Key;
                    }
                    if (kv.Value.Note != ye.Note)
                    {
                        return "备注不同 " + kv.Key + " [" + kv.Value.Note + "] vs [" + ye.Note + "]";
                    }
                }
            }
            return null;
        }

        private static string ScratchDir()
        {
            if (_scratchRoot == null)
            {
                _scratchRoot = Path.Combine(Environment.CurrentDirectory, "_selftest_tmp");
                if (Directory.Exists(_scratchRoot))
                {
                    try
                    {
                        Directory.Delete(_scratchRoot, true);
                    }
                    catch (Exception)
                    {
                    }
                }
                Directory.CreateDirectory(_scratchRoot);
            }

            string sub = Path.Combine(_scratchRoot,
                Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(sub);
            return sub;
        }

        private static void Cleanup()
        {
            if (_scratchRoot != null && Directory.Exists(_scratchRoot))
            {
                try
                {
                    Directory.Delete(_scratchRoot, true);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
#endif
