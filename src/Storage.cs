using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DakaHelper
{
    /// <summary>
    /// 用户可配置项。保存在可执行文件旁的 config.json。
    /// </summary>
    public class AppSettings
    {
        /// <summary>数据文件路径；空表示使用默认位置（程序目录下的「数据」文件夹）。
        /// 允许写相对路径，相对路径按程序所在目录解析，便于整个文件夹搬走。</summary>
        public string DataPath;

        /// <summary>
        /// 点「打开数据文件夹」时是否先尝试启动资源管理器。
        /// 某些受限环境不允许本程序启动 explorer.exe（会以 0xC0000142 初始化失败），
        /// 关掉它可以避免每次点击都白试一次、只显示路径。
        /// </summary>
        public bool TryLaunchExplorer;

        public AppSettings()
        {
            DataPath = null;
            TryLaunchExplorer = true;
        }

        public bool HasCustomPath()
        {
            return !string.IsNullOrEmpty(DataPath);
        }
    }

    /// <summary>
    /// 持久化层。
    ///
    /// 设计要点：
    ///   1. 手写 JSON，零程序集引用风险（不依赖 System.Web.Extensions）。
    ///   2. 原子保存：先写 .tmp，再用 File.Replace 替换，同时自动生成 .bak 上一版备份。
    ///   3. 损坏恢复：解析失败时把原文件改名保存为 .corrupt-时间戳，并以空数据启动，
    ///      通过 warning 参数把情况告知用户，绝不静默丢数据。
    ///   4. 文件写成 UTF-8 with BOM，便于 Windows 记事本正确识别中文。
    /// </summary>
    public static class Storage
    {
        /// <summary>默认数据文件夹名（位于程序所在目录内）。</summary>
        public const string DataFolderName = "数据";
        public const string DataFileName = "data.json";
        public const string ConfigFileName = "config.json";

        /// <summary>
        /// 可执行文件所在目录。
        /// 用程序集位置而不是 CurrentDirectory —— 从快捷方式或其它工作目录启动时两者并不相同。
        /// </summary>
        public static string ExeDirectory()
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                if (asm != null)
                {
                    string location = asm.Location;
                    if (!string.IsNullOrEmpty(location))
                    {
                        string dir = Path.GetDirectoryName(location);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            return dir;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return Environment.CurrentDirectory;
        }

        /// <summary>默认数据文件夹：程序目录下的「数据」文件夹。</summary>
        public static string DefaultDataDirectory()
        {
            return Path.Combine(ExeDirectory(), DataFolderName);
        }

        /// <summary>默认数据文件：程序目录\数据\data.json</summary>
        public static string DefaultDataPath()
        {
            return Path.Combine(DefaultDataDirectory(), DataFileName);
        }

        /// <summary>配置文件（记录用户选择的保存位置）：程序目录\config.json</summary>
        public static string ConfigPath()
        {
            return Path.Combine(ExeDirectory(), ConfigFileName);
        }

        /// <summary>按优先级决定实际使用的数据文件：命令行参数 &gt; 配置文件 &gt; 默认位置。</summary>
        public static string ResolveDataPath(string overridePath, AppSettings settings)
        {
            if (!string.IsNullOrEmpty(overridePath))
            {
                try
                {
                    return Path.GetFullPath(overridePath);
                }
                catch (Exception)
                {
                    return DefaultDataPath();
                }
            }

            if (settings != null && !string.IsNullOrEmpty(settings.DataPath))
            {
                string p = settings.DataPath;
                if (!Path.IsPathRooted(p))
                {
                    p = Path.Combine(ExeDirectory(), p);
                }
                try
                {
                    return Path.GetFullPath(p);
                }
                catch (Exception)
                {
                    return DefaultDataPath();
                }
            }

            return DefaultDataPath();
        }

        /// <summary>确认目录存在且真的可写（建一个探针文件再删掉）。</summary>
        public static bool EnsureDirectoryWritable(string dir, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(dir))
            {
                error = "路径为空。";
                return false;
            }

            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string probe = Path.Combine(dir,
                    ".write-test-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
                File.WriteAllText(probe, "ok", new UTF8Encoding(false));
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>读取设置（默认配置文件位置）；文件不存在时返回默认设置。</summary>
        public static AppSettings LoadSettings(out string warning)
        {
            return LoadSettings(ConfigPath(), out warning);
        }

        public static AppSettings LoadSettings(string path, out string warning)
        {
            warning = null;
            AppSettings settings = new AppSettings();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return settings;
            }

            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                warning = "无法读取配置文件：" + ex.Message + "\r\n已使用默认设置。";
                return settings;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return settings;
            }

            try
            {
                AppSettings parsed = Json.ReadSettings(text);
                return parsed == null ? settings : parsed;
            }
            catch (Exception ex)
            {
                string corrupt = path + ".corrupt-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                try
                {
                    File.Move(path, corrupt);
                }
                catch (Exception)
                {
                    corrupt = path;
                }
                warning = "配置文件已损坏，原文已保留为：\r\n" + corrupt
                    + "\r\n\r\n已使用默认设置。原因：" + ex.Message;
                return settings;
            }
        }

        /// <summary>保存设置到默认配置文件位置。</summary>
        public static bool SaveSettings(AppSettings settings, out string error)
        {
            return SaveSettings(settings, ConfigPath(), out error);
        }

        /// <summary>保存设置。同样用 .tmp + 替换，避免写到一半留下半个文件。</summary>
        public static bool SaveSettings(AppSettings settings, string path, out string error)
        {
            error = null;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Json.WriteSettings(settings), new UTF8Encoding(true));

                if (File.Exists(path))
                {
                    try
                    {
                        // 配置文件不需要 .bak，第二个参数传 null 即可
                        File.Replace(tmp, path, null, true);
                        return true;
                    }
                    catch (Exception)
                    {
                        File.Copy(tmp, path, true);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                    return true;
                }

                if (File.Exists(tmp))
                {
                    try
                    {
                        File.Delete(tmp);
                    }
                    catch (Exception)
                    {
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>读取数据；失败时返回空 AppData 并通过 warning 说明原因。</summary>
        public static AppData Load(string path, out string warning)
        {
            warning = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new AppData();
            }

            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                warning = "无法读取数据文件：" + ex.Message + "\r\n已使用空白数据启动。";
                return new AppData();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return new AppData();
            }

            try
            {
                return Json.ReadAppData(text);
            }
            catch (Exception ex)
            {
                string corrupt = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                try
                {
                    File.Move(path, corrupt);
                }
                catch (Exception)
                {
                    corrupt = path;
                }
                warning = "数据文件已损坏，无法解析。\r\n原始文件已保留为：\r\n" + corrupt
                    + "\r\n\r\n已使用空白数据启动。原因：" + ex.Message;
                return new AppData();
            }
        }

        /// <summary>原子保存。返回 false 时 error 说明原因。</summary>
        public static bool Save(AppData data, string path, out string error)
        {
            error = null;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string text = Json.Write(data);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(true));

                if (File.Exists(path))
                {
                    try
                    {
                        // 原子替换，并自动把上一版留成 .bak
                        File.Replace(tmp, path, path + ".bak", true);
                        return true;
                    }
                    catch (Exception)
                    {
                        // 某些文件系统（如部分网络盘）不支持 Replace，退化为直接覆盖
                        File.Copy(tmp, path, true);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                    return true;
                }

                if (File.Exists(tmp))
                {
                    try
                    {
                        File.Delete(tmp);
                    }
                    catch (Exception)
                    {
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>生成演示数据，供 --shot 截图自检使用。</summary>
        public static AppData BuildDemo(int seed)
        {
            AppData d = new AppData();
            string[] names = new string[] { "早起", "阅读30分钟", "运动", "喝水2升" };
            string[] colors = new string[] { "#2F6FED", "#7C4DFF", "#22A06B", "#E8894A" };
            string[] notes = new string[] { "状态不错", "有点累但坚持了", "提前完成", "补打卡" };

            Random rnd = new Random(seed);
            DateTime today = DateTime.Today;

            for (int i = 0; i < names.Length; i++)
            {
                Habit h = new Habit();
                h.Name = names[i];
                h.Color = colors[i];
                h.Order = i;
                h.CreatedAt = Dates.Key(today.AddDays(-78 + i * 4));

                for (int back = 78; back >= 0; back--)
                {
                    DateTime day = today.AddDays(-back);
                    if (day < h.CreatedDate())
                    {
                        continue;
                    }

                    double p = 0.60 + i * 0.08;
                    bool hit = rnd.NextDouble() < p;
                    if (back <= 13 + i)
                    {
                        // 让最近形成连续，这样截图上能看到真实的连续天数与热力图色彩
                        hit = rnd.NextDouble() < 0.92;
                    }
                    if (!hit)
                    {
                        continue;
                    }

                    DayEntry e = new DayEntry(Dates.Key(day));
                    e.Mood = 1 + rnd.Next(5);
                    if (rnd.NextDouble() < 0.35)
                    {
                        e.Note = notes[rnd.Next(notes.Length)];
                    }
                    h.Entries[e.Date] = e;
                }
                d.Habits.Add(h);
            }

            d.NormalizeOrder();
            return d;
        }
    }

    /// <summary>极简 JSON 读写。写入严格转义；读取容错，遇到个别坏字段尽量跳过而不是整份丢弃。</summary>
    public static class Json
    {
        // ---------------- 写入 ----------------

        public static string Write(AppData d)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"schema\": ").Append(d.Schema.ToString(CultureInfo.InvariantCulture)).Append(",\r\n");
            sb.Append("  \"heatmapMode\": ");
            WriteString(sb, d.HeatmapMode == null ? "habit" : d.HeatmapMode);
            sb.Append(",\r\n");

            if (d.Habits.Count == 0)
            {
                sb.Append("  \"habits\": []\r\n");
            }
            else
            {
                sb.Append("  \"habits\": [\r\n");
                for (int i = 0; i < d.Habits.Count; i++)
                {
                    WriteHabit(sb, d.Habits[i]);
                    sb.Append(i == d.Habits.Count - 1 ? "\r\n" : ",\r\n");
                }
                sb.Append("  ]\r\n");
            }

            sb.Append("}\r\n");
            return sb.ToString();
        }

        private static void WriteHabit(StringBuilder sb, Habit h)
        {
            sb.Append("    {\r\n");
            sb.Append("      \"id\": ");
            WriteString(sb, h.Id);
            sb.Append(",\r\n");
            sb.Append("      \"name\": ");
            WriteString(sb, h.Name);
            sb.Append(",\r\n");
            sb.Append("      \"color\": ");
            WriteString(sb, h.Color);
            sb.Append(",\r\n");
            sb.Append("      \"createdAt\": ");
            WriteString(sb, h.CreatedAt);
            sb.Append(",\r\n");
            sb.Append("      \"order\": ").Append(h.Order.ToString(CultureInfo.InvariantCulture)).Append(",\r\n");

            if (h.Entries.Count == 0)
            {
                sb.Append("      \"entries\": {}\r\n");
            }
            else
            {
                sb.Append("      \"entries\": {\r\n");
                int n = 0;
                int total = h.Entries.Count;
                foreach (KeyValuePair<string, DayEntry> kv in h.Entries)
                {
                    n++;
                    sb.Append("        ");
                    WriteString(sb, kv.Key);
                    sb.Append(": { \"mood\": ");
                    if (kv.Value != null && kv.Value.Mood.HasValue)
                    {
                        sb.Append(kv.Value.Mood.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append("null");
                    }
                    sb.Append(", \"note\": ");
                    WriteString(sb, kv.Value == null ? "" : kv.Value.Note);
                    sb.Append(" }");
                    sb.Append(n == total ? "\r\n" : ",\r\n");
                }
                sb.Append("      }\r\n");
            }

            sb.Append("    }");
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < ' ')
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        // ---------------- 设置 ----------------

        public static string WriteSettings(AppSettings s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n  \"dataPath\": ");
            WriteString(sb, s == null ? null : s.DataPath);
            sb.Append(",\r\n  \"tryLaunchExplorer\": ");
            sb.Append(s == null || s.TryLaunchExplorer ? "true" : "false");
            sb.Append("\r\n}\r\n");
            return sb.ToString();
        }

        public static AppSettings ReadSettings(string text)
        {
            object root = new Reader(text).ParseValue();
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
            {
                throw new FormatException("配置文件根节点不是 JSON 对象");
            }

            AppSettings s = new AppSettings();
            s.DataPath = GetString(o, "dataPath", null);
            s.TryLaunchExplorer = GetBool(o, "tryLaunchExplorer", true);
            return s;
        }

        // ---------------- 读取 ----------------

        public static AppData ReadAppData(string text)
        {
            object root = new Reader(text).ParseValue();
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
            {
                throw new FormatException("根节点不是 JSON 对象");
            }

            AppData d = new AppData();
            d.Schema = (int)GetNumber(o, "schema", 1);
            d.HeatmapMode = GetString(o, "heatmapMode", "habit");

            object habitsObj;
            if (o.TryGetValue("habits", out habitsObj))
            {
                List<object> arr = habitsObj as List<object>;
                if (arr != null)
                {
                    for (int i = 0; i < arr.Count; i++)
                    {
                        Dictionary<string, object> ho = arr[i] as Dictionary<string, object>;
                        if (ho == null)
                        {
                            continue;
                        }
                        d.Habits.Add(ReadHabit(ho));
                    }
                }
            }

            d.NormalizeOrder();
            if (!string.Equals(d.HeatmapMode, "all", StringComparison.Ordinal))
            {
                d.HeatmapMode = "habit";
            }
            return d;
        }

        private static Habit ReadHabit(Dictionary<string, object> ho)
        {
            Habit h = new Habit();
            h.Id = GetString(ho, "id", h.Id);
            h.Name = GetString(ho, "name", "");
            h.Color = GetString(ho, "color", "#2F6FED");
            h.CreatedAt = GetString(ho, "createdAt", Dates.TodayKey());
            h.Order = (int)GetNumber(ho, "order", 0);

            object entriesObj;
            if (ho.TryGetValue("entries", out entriesObj))
            {
                Dictionary<string, object> eo = entriesObj as Dictionary<string, object>;
                if (eo != null)
                {
                    foreach (KeyValuePair<string, object> kv in eo)
                    {
                        DateTime dt;
                        if (!Dates.TryParse(kv.Key, out dt))
                        {
                            // 非法日期键直接跳过，不让一条坏数据毁掉整份文件
                            continue;
                        }

                        DayEntry entry = new DayEntry(kv.Key);
                        Dictionary<string, object> de = kv.Value as Dictionary<string, object>;
                        if (de != null)
                        {
                            entry.Note = GetString(de, "note", "");
                            object moodObj;
                            if (de.TryGetValue("mood", out moodObj) && moodObj is double)
                            {
                                int m = (int)(double)moodObj;
                                if (m >= 1 && m <= 5)
                                {
                                    entry.Mood = m;
                                }
                            }
                        }
                        h.Entries[kv.Key] = entry;
                    }
                }
            }
            return h;
        }

        private static string GetString(Dictionary<string, object> o, string key, string fallback)
        {
            object v;
            if (o.TryGetValue(key, out v))
            {
                string s = v as string;
                if (s != null)
                {
                    return s;
                }
            }
            return fallback;
        }

        private static double GetNumber(Dictionary<string, object> o, string key, double fallback)
        {
            object v;
            if (o.TryGetValue(key, out v) && v is double)
            {
                return (double)v;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> o, string key, bool fallback)
        {
            object v;
            if (o.TryGetValue(key, out v) && v is bool)
            {
                return (bool)v;
            }
            return fallback;
        }

        /// <summary>递归下降解析器。对象 -> Dictionary，数组 -> List，数字 -> double。</summary>
        private sealed class Reader
        {
            private readonly string _s;
            private int _i;

            public Reader(string s)
            {
                _s = s;
                _i = 0;
            }

            public object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length)
                {
                    throw new FormatException("JSON 内容意外结束");
                }

                char c = _s[_i];
                if (c == '{')
                {
                    return ParseObject();
                }
                if (c == '[')
                {
                    return ParseArray();
                }
                if (c == '"')
                {
                    return ParseString();
                }
                if (c == 't')
                {
                    Expect("true");
                    return true;
                }
                if (c == 'f')
                {
                    Expect("false");
                    return false;
                }
                if (c == 'n')
                {
                    Expect("null");
                    return null;
                }
                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++;
                SkipWs();
                if (_i < _s.Length && _s[_i] == '}')
                {
                    _i++;
                    return dict;
                }

                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != '"')
                    {
                        throw new FormatException("JSON 对象的键必须是字符串");
                    }
                    string key = ParseString();
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != ':')
                    {
                        throw new FormatException("JSON 对象缺少冒号");
                    }
                    _i++;
                    dict[key] = ParseValue();
                    SkipWs();
                    if (_i >= _s.Length)
                    {
                        throw new FormatException("JSON 对象未闭合");
                    }
                    if (_s[_i] == ',')
                    {
                        _i++;
                        continue;
                    }
                    if (_s[_i] == '}')
                    {
                        _i++;
                        return dict;
                    }
                    throw new FormatException("JSON 对象中出现意外字符");
                }
            }

            private List<object> ParseArray()
            {
                List<object> list = new List<object>();
                _i++;
                SkipWs();
                if (_i < _s.Length && _s[_i] == ']')
                {
                    _i++;
                    return list;
                }

                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (_i >= _s.Length)
                    {
                        throw new FormatException("JSON 数组未闭合");
                    }
                    if (_s[_i] == ',')
                    {
                        _i++;
                        continue;
                    }
                    if (_s[_i] == ']')
                    {
                        _i++;
                        return list;
                    }
                    throw new FormatException("JSON 数组中出现意外字符");
                }
            }

            private string ParseString()
            {
                _i++;
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length)
                    {
                        throw new FormatException("JSON 字符串未闭合");
                    }
                    char c = _s[_i++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_i >= _s.Length)
                    {
                        throw new FormatException("JSON 转义未完成");
                    }
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length)
                            {
                                throw new FormatException("\\u 转义不完整");
                            }
                            sb.Append((char)Convert.ToInt32(_s.Substring(_i, 4), 16));
                            _i += 4;
                            break;
                        default:
                            throw new FormatException("无法识别的转义字符：\\" + e);
                    }
                }
            }

            private double ParseNumber()
            {
                int start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+'))
                {
                    _i++;
                }
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (char.IsDigit(c) || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    {
                        _i++;
                        continue;
                    }
                    break;
                }
                if (_i == start)
                {
                    throw new FormatException("JSON 中出现无法识别的值");
                }
                return double.Parse(_s.Substring(start, _i - start), CultureInfo.InvariantCulture);
            }

            private void Expect(string literal)
            {
                if (_i + literal.Length > _s.Length
                    || string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
                {
                    throw new FormatException("JSON 中出现无法识别的值");
                }
                _i += literal.Length;
            }

            private void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        _i++;
                        continue;
                    }
                    break;
                }
            }
        }
    }
}
