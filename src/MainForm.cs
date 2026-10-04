using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DakaHelper
{
    public class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private AppData _data;
        private string _dataPath;
        private readonly bool _persist;
        private AppSettings _settings;
        private string _selectedHabitId;
        private string _startupWarning;
        private bool _suppressEvents;
        private IntPtr _iconHandle = IntPtr.Zero;
        private DateTime _lastSeenDate = DateTime.Today;

        private Panel _headerPanel;
        private HabitListPanel _habitList;
        private ModernButton _btnNew;
        private ModernButton _btnRename;
        private ModernButton _btnDelete;
        private Label _todayName;
        private ModernButton _btnToggle;
        private Label _moodLabel;
        private MoodPicker _moodPicker;
        private Label _noteLabel;
        private TextBox _noteBox;
        private StatsStrip _stats;
        private HeatmapPanel _heatmap;
        private Label _statusPath;
        private Label _statusSaved;
        private ModernButton _btnOpenFolder;
        private ModernButton _btnSettings;
        private Timer _noteTimer;
        private Timer _rolloverTimer;

        public MainForm(string dataPathOverride, bool persist)
            : this(dataPathOverride, null, persist)
        {
        }

        public MainForm(string dataPathOverride, AppData initial, bool persist)
        {
            _persist = persist;

            // 数据位置优先级：命令行 --data > config.json > 程序目录\数据\data.json
            string settingsWarning;
            _settings = Storage.LoadSettings(out settingsWarning);
            _dataPath = Storage.ResolveDataPath(dataPathOverride, _settings);

            if (initial != null)
            {
                _data = initial;
            }
            else
            {
                string warning;
                _data = Storage.Load(_dataPath, out warning);
                _startupWarning = warning;
            }

            if (!string.IsNullOrEmpty(settingsWarning))
            {
                _startupWarning = string.IsNullOrEmpty(_startupWarning)
                    ? settingsWarning
                    : _startupWarning + "\r\n\r\n" + settingsWarning;
            }

            _data.NormalizeOrder();

            if (_data.Habits.Count > 0)
            {
                _selectedHabitId = _data.Habits[0].Id;
            }

            BuildUi();
            ApplyIcon();

            _noteTimer = new Timer();
            _noteTimer.Interval = 1500;
            _noteTimer.Tick += delegate(object s, EventArgs e) { OnNoteTimerTick(); };

            _rolloverTimer = new Timer();
            _rolloverTimer.Interval = 30000;
            _rolloverTimer.Tick += delegate(object s, EventArgs e) { CheckDateRollover(); };
            _rolloverTimer.Start();

            RefreshAll();
            UpdateStatusPath();
            if (File.Exists(_dataPath))
            {
                SetSavedStatus("");
            }
            else
            {
                _statusSaved.Text = "尚未保存";
            }
        }

        // ---------------- 界面搭建 ----------------

        private void BuildUi()
        {
            this.Text = "打卡助手";
            this.ClientSize = new Size(1120, 790);
            this.MinimumSize = new Size(960, 700);
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.TextMain;
            this.Font = Theme.Base;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.BackColor = Theme.Bg;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            root.Controls.Add(BuildStatusBar(), 0, 2);

            this.Controls.Add(root);
        }

        private Control BuildHeader()
        {
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Fill;
            _headerPanel.BackColor = Theme.Bg;
            _headerPanel.Paint += new PaintEventHandler(HeaderPaint);
            return _headerPanel;
        }

        private void HeaderPaint(object sender, PaintEventArgs e)
        {
            Control host = sender as Control;
            if (host == null)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Theme.DrawText(g, "打卡助手", Theme.H1, Theme.TextMain,
                new Rectangle(18, 10, 240, 30), Theme.LeftCenter);
            Theme.DrawText(g, Dates.LongChinese(DateTime.Today), Theme.Small, Theme.TextMuted,
                new Rectangle(18, 36, 430, 20), Theme.LeftCenter);

            int total = _data.Habits.Count;
            int done = _data.CheckedCountToday();

            int barWidth = 280;
            int x = host.Width - barWidth - 20;
            if (x < 470)
            {
                x = 470;
            }

            string text = string.Format("今日 {0} / {1} 已完成", done, total);
            Theme.DrawText(g, text, Theme.Bold, Theme.TextMain,
                new Rectangle(x, 14, barWidth, 24), Theme.RightCenter);

            Rectangle bar = new Rectangle(x, 44, barWidth, 6);
            Theme.FillRounded(g, bar, 3, Theme.HeatEmpty);
            if (total > 0 && done > 0)
            {
                int w = (int)Math.Round(bar.Width * (double)done / total);
                if (w < 8)
                {
                    w = 8;
                }
                if (w > bar.Width)
                {
                    w = bar.Width;
                }
                Theme.FillRounded(g, new Rectangle(bar.X, bar.Y, w, bar.Height), 3,
                    done >= total ? Theme.Success : Theme.Primary);
            }
        }

        private Control BuildContent()
        {
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 2;
            content.RowCount = 1;
            content.BackColor = Theme.Bg;
            content.Padding = new Padding(16, 2, 16, 10);
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 316));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            content.Controls.Add(BuildHabitCard(), 0, 0);
            content.Controls.Add(BuildRightColumn(), 1, 0);
            return content;
        }

        private Control BuildHabitCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "习惯项目";
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 14, 0);

            _habitList = new HabitListPanel();
            _habitList.Dock = DockStyle.Fill;
            _habitList.SelectionChanged += delegate(object s, EventArgs e) { OnHabitSelectionChanged(); };
            _habitList.HabitActivated += delegate(object s, EventArgs e) { RenameHabit(); };

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Color.Transparent;
            listHost.Padding = new Padding(0, card.TitleHeight, 0, 0);
            listHost.Controls.Add(_habitList);

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 44;
            bar.ColumnCount = 3;
            bar.RowCount = 1;
            bar.BackColor = Color.Transparent;
            bar.Padding = new Padding(0, 8, 0, 0);
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

            _btnNew = MakeSmallButton("＋ 新建习惯", Theme.Primary, Color.White);
            _btnNew.Margin = new Padding(0, 0, 6, 0);
            _btnNew.Click += delegate(object s, EventArgs e) { NewHabit(); };

            _btnRename = MakeSmallButton("改名", Theme.Hex("#EDF0F5"), Theme.TextMain);
            _btnRename.Margin = new Padding(0, 0, 6, 0);
            _btnRename.Click += delegate(object s, EventArgs e) { RenameHabit(); };

            _btnDelete = MakeSmallButton("删除", Theme.DangerSoft, Theme.Danger);
            _btnDelete.Margin = new Padding(0, 0, 0, 0);
            _btnDelete.Click += delegate(object s, EventArgs e) { DeleteHabit(); };

            bar.Controls.Add(_btnNew, 0, 0);
            bar.Controls.Add(_btnRename, 1, 0);
            bar.Controls.Add(_btnDelete, 2, 0);

            card.Controls.Add(listHost);
            card.Controls.Add(bar);
            return card;
        }

        private static ModernButton MakeSmallButton(string text, Color accent, Color fore)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Accent = accent;
            b.ForeColor = fore;
            b.Font = Theme.Base;
            b.Dock = DockStyle.Fill;
            b.Radius = 6;
            return b;
        }

        private Control BuildRightColumn()
        {
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 3;
            right.BackColor = Theme.Bg;
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 228));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            right.Controls.Add(BuildTodayCard(), 0, 0);
            right.Controls.Add(BuildStatsCard(), 0, 1);
            right.Controls.Add(BuildHeatmapCard(), 0, 2);
            return right;
        }

        private Control BuildTodayCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "今日打卡";
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 0, 10);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 5;
            t.BackColor = Color.Transparent;
            t.Padding = new Padding(0, card.TitleHeight, 0, 0);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel row0 = new Panel();
            row0.Dock = DockStyle.Fill;
            row0.BackColor = Color.Transparent;

            _btnToggle = new ModernButton();
            _btnToggle.Dock = DockStyle.Right;
            _btnToggle.Width = 164;
            _btnToggle.UseOnState = true;
            _btnToggle.OnAccent = Theme.Success;
            _btnToggle.Font = Theme.Bold;
            _btnToggle.Radius = 8;

            _btnToggle.Click += delegate(object s, EventArgs e) { ToggleToday(); };

            _todayName = new Label();
            _todayName.Dock = DockStyle.Fill;
            _todayName.Font = Theme.H2;
            _todayName.ForeColor = Theme.TextMain;
            _todayName.TextAlign = ContentAlignment.MiddleLeft;
            _todayName.BackColor = Color.Transparent;
            _todayName.AutoEllipsis = true;

            row0.Controls.Add(_todayName);
            row0.Controls.Add(_btnToggle);

            _moodLabel = new Label();
            _moodLabel.Dock = DockStyle.Fill;
            _moodLabel.Font = Theme.Tiny;
            _moodLabel.ForeColor = Theme.TextMuted;
            _moodLabel.TextAlign = ContentAlignment.MiddleLeft;
            _moodLabel.BackColor = Color.Transparent;

            _moodPicker = new MoodPicker();
            _moodPicker.Dock = DockStyle.Fill;
            _moodPicker.SelectionChanged += delegate(object s, EventArgs e) { OnMoodChanged(); };

            _noteLabel = new Label();
            _noteLabel.Dock = DockStyle.Fill;
            _noteLabel.Font = Theme.Tiny;
            _noteLabel.ForeColor = Theme.TextMuted;
            _noteLabel.TextAlign = ContentAlignment.MiddleLeft;
            _noteLabel.BackColor = Color.Transparent;

            _noteBox = new TextBox();
            _noteBox.Dock = DockStyle.Fill;
            _noteBox.Multiline = true;
            _noteBox.ScrollBars = ScrollBars.Vertical;
            _noteBox.BorderStyle = BorderStyle.FixedSingle;
            _noteBox.Font = Theme.Base;
            _noteBox.BackColor = Color.White;
            _noteBox.ForeColor = Theme.TextMain;
            _noteBox.TextChanged += delegate(object s, EventArgs e) { OnNoteTextChanged(); };
            _noteBox.Leave += delegate(object s, EventArgs e) { CommitNote(); };

            t.Controls.Add(row0, 0, 0);
            t.Controls.Add(_moodLabel, 0, 1);
            t.Controls.Add(_moodPicker, 0, 2);
            t.Controls.Add(_noteLabel, 0, 3);
            t.Controls.Add(_noteBox, 0, 4);

            card.Controls.Add(t);
            return card;
        }

        private Control BuildStatsCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "坚持统计";
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 0, 10);

            _stats = new StatsStrip();
            _stats.Dock = DockStyle.Fill;

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Padding = new Padding(0, card.TitleHeight, 0, 0);
            host.Controls.Add(_stats);

            card.Controls.Add(host);
            return card;
        }

        private Control BuildHeatmapCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "打卡日历";
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 0, 0);

            _heatmap = new HeatmapPanel();
            _heatmap.Dock = DockStyle.Fill;
            _heatmap.DayToggled += delegate(object s, DateTime day) { OnHeatmapDayToggled(day); };
            _heatmap.ModeChanged += delegate(object s, EventArgs e) { OnHeatmapModeChanged(); };

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Padding = new Padding(0, card.TitleHeight, 0, 0);
            host.Controls.Add(_heatmap);

            card.Controls.Add(host);
            return card;
        }

        private Control BuildStatusBar()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = Theme.Bg;

            Panel settingsHost = new Panel();
            settingsHost.Dock = DockStyle.Right;
            settingsHost.Width = 76;
            settingsHost.BackColor = Color.Transparent;
            settingsHost.Padding = new Padding(0, 3, 16, 3);

            _btnSettings = new ModernButton();
            _btnSettings.Text = "设置";
            _btnSettings.Accent = Theme.Hex("#EDF0F5");
            _btnSettings.ForeColor = Theme.TextMain;
            _btnSettings.Font = Theme.Tiny;
            _btnSettings.Radius = 5;
            _btnSettings.Dock = DockStyle.Fill;
            _btnSettings.Click += delegate(object s, EventArgs e) { OpenSettings(); };
            settingsHost.Controls.Add(_btnSettings);

            Panel buttonHost = new Panel();
            buttonHost.Dock = DockStyle.Right;
            buttonHost.Width = 132;
            buttonHost.BackColor = Color.Transparent;
            buttonHost.Padding = new Padding(0, 3, 8, 3);

            _btnOpenFolder = new ModernButton();
            _btnOpenFolder.Text = "打开数据文件夹";
            _btnOpenFolder.Font = Theme.Tiny;
            _btnOpenFolder.Radius = 5;
            _btnOpenFolder.Dock = DockStyle.Fill;
            _btnOpenFolder.Click += delegate(object s, EventArgs e) { OpenDataFolder(); };
            buttonHost.Controls.Add(_btnOpenFolder);

            _statusSaved = new Label();
            _statusSaved.Dock = DockStyle.Right;
            _statusSaved.Width = 128;
            _statusSaved.Font = Theme.Tiny;
            _statusSaved.ForeColor = Theme.TextFaint;
            _statusSaved.TextAlign = ContentAlignment.MiddleRight;
            _statusSaved.BackColor = Color.Transparent;

            _statusPath = new Label();
            _statusPath.Dock = DockStyle.Fill;
            _statusPath.Font = Theme.Tiny;
            _statusPath.ForeColor = Theme.TextFaint;
            _statusPath.TextAlign = ContentAlignment.MiddleLeft;
            _statusPath.BackColor = Color.Transparent;
            _statusPath.AutoEllipsis = true;
            _statusPath.Padding = new Padding(18, 0, 0, 0);

            // 添加顺序决定停靠优先级：后加的 Right 控件先占位，Fill 的最后填充剩余空间
            p.Controls.Add(_statusPath);
            p.Controls.Add(_statusSaved);
            p.Controls.Add(buttonHost);
            p.Controls.Add(settingsHost);
            return p;
        }

        private void ApplyIcon()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        Theme.FillRounded(g, new Rectangle(1, 1, 30, 30), 9, Theme.Primary);
                        using (Pen pen = new Pen(Color.White, 3.6f))
                        {
                            pen.StartCap = LineCap.Round;
                            pen.EndCap = LineCap.Round;
                            pen.LineJoin = LineJoin.Round;
                            g.DrawLines(pen, new PointF[]
                            {
                                new PointF(9f, 17f),
                                new PointF(14f, 22.5f),
                                new PointF(23.5f, 10f)
                            });
                        }
                    }
                    _iconHandle = bmp.GetHicon();
                }
                this.Icon = Icon.FromHandle(_iconHandle);
            }
            catch (Exception)
            {
            }
        }

        // ---------------- 刷新 ----------------

        private Habit CurrentHabit()
        {
            if (_habitList != null)
            {
                _selectedHabitId = _habitList.SelectedId;
            }
            return _data.Find(_selectedHabitId);
        }

        private void RefreshAll()
        {
            _suppressEvents = true;
            try
            {
                _data.NormalizeOrder();
                _habitList.SetData(_data.Habits, _selectedHabitId);
                _selectedHabitId = _habitList.SelectedId;

                Habit h = _data.Find(_selectedHabitId);
                string todayKey = Dates.TodayKey();

                if (h == null)
                {
                    _todayName.Text = "还没有习惯项目";
                    _btnToggle.Enabled = false;
                    _btnToggle.IsOn = false;
                    _btnToggle.Text = "请先新建习惯";
                    _moodPicker.Enabled = false;
                    _moodPicker.SelectedValue = null;
                    _noteBox.Enabled = false;
                    _noteBox.Text = "";
                    _moodLabel.Text = "心情（需先新建习惯）";
                    _noteLabel.Text = "备注";
                    _stats.SetHabit(null);
                    _heatmap.SetData(null, _data.Habits);
                }
                else
                {
                    bool on = h.IsChecked(todayKey);
                    _todayName.Text = h.EffectiveName();
                    _btnToggle.Enabled = true;
                    _btnToggle.Accent = Theme.Hex(h.Color);
                    _btnToggle.UseOnState = true;
                    _btnToggle.IsOn = on;
                    _btnToggle.Text = on ? "已打卡 · 点击取消" : "今日打卡";

                    _moodPicker.Enabled = on;
                    _noteBox.Enabled = on;

                    // C# 5 的确定赋值分析不接受在 && 短路表达式里赋值的 out 变量，
                    // 因此这里先无条件取值，再判断是否可用。
                    DayEntry entry = null;
                    h.Entries.TryGetValue(todayKey, out entry);
                    bool hasEntry = on && entry != null;
                    _moodPicker.SelectedValue = hasEntry ? entry.Mood : null;
                    if (!_noteBox.Focused)
                    {
                        _noteBox.Text = hasEntry && entry.Note != null ? entry.Note : "";
                    }

                    _noteLabel.Text = on ? "备注（自动保存，可留空）" : "备注（打卡后即可填写）";
                    _stats.SetHabit(h);
                    _heatmap.SetData(h, _data.Habits);
                }

                _heatmap.SetMode(_data.HeatmapMode);
                UpdateMoodLabel();
                UpdateButtonsEnabled();
                _heatmap.Invalidate();
                _habitList.Invalidate();
                if (_headerPanel != null)
                {
                    _headerPanel.Invalidate();
                }
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void UpdateMoodLabel()
        {
            if (_moodPicker == null)
            {
                return;
            }
            if (_moodPicker.SelectedValue.HasValue)
            {
                _moodLabel.Text = "心情：" + Moods.Label(_moodPicker.SelectedValue.Value)
                    + "（再点一次该表情可取消）";
            }
            else
            {
                _moodLabel.Text = "心情（可选）";
            }
        }

        private void UpdateButtonsEnabled()
        {
            bool has = _data.Habits.Count > 0;
            _btnRename.Enabled = has;
            _btnDelete.Enabled = has;
        }

        private void UpdateStatusPath()
        {
            _statusPath.Text = "数据文件：" + _dataPath;
        }

        private void SetSavedStatus(string suffix)
        {
            _statusSaved.Text = "已保存 " + DateTime.Now.ToString("HH:mm:ss") + suffix;
            _statusSaved.ForeColor = Theme.TextFaint;
        }

        // ---------------- 交互 ----------------

        private void OnHabitSelectionChanged()
        {
            if (_suppressEvents)
            {
                return;
            }
            _selectedHabitId = _habitList.SelectedId;
            RefreshAll();
        }

        private void OnMoodChanged()
        {
            if (_suppressEvents)
            {
                return;
            }

            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }

            string key = Dates.TodayKey();
            DayEntry e;
            if (!h.Entries.TryGetValue(key, out e) || e == null)
            {
                // 未打卡时不允许记录心情
                _suppressEvents = true;
                _moodPicker.SelectedValue = null;
                _suppressEvents = false;
                return;
            }

            e.Mood = _moodPicker.SelectedValue;
            UpdateMoodLabel();
            SaveNow();
        }

        private void OnNoteTextChanged()
        {
            if (_suppressEvents || _noteTimer == null)
            {
                return;
            }
            _noteTimer.Stop();
            _noteTimer.Start();
        }

        private void OnNoteTimerTick()
        {
            if (_noteTimer != null)
            {
                _noteTimer.Stop();
            }
            CommitNote();
        }

        private void CommitNote()
        {
            if (_suppressEvents || _noteBox == null)
            {
                return;
            }

            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }

            string key = Dates.TodayKey();
            DayEntry e;
            if (!h.Entries.TryGetValue(key, out e) || e == null)
            {
                return;
            }

            string text = _noteBox.Text;
            if (e.Note == text)
            {
                return;
            }

            e.Note = text;
            SaveNow();
        }

        private void ToggleToday()
        {
            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }
            ToggleDay(h, DateTime.Today);
        }

        private void OnHeatmapDayToggled(DateTime day)
        {
            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }
            ToggleDay(h, day);
        }

        private void ToggleDay(Habit h, DateTime day)
        {
            string key = Dates.Key(day);
            DayEntry existing;
            bool has = h.Entries.TryGetValue(key, out existing) && existing != null;

            if (has)
            {
                string extra = "";
                bool hasMood = existing.Mood.HasValue;
                bool hasNote = !string.IsNullOrEmpty(existing.Note);
                if (hasMood && hasNote)
                {
                    extra = "心情和备注";
                }
                else if (hasMood)
                {
                    extra = "心情";
                }
                else if (hasNote)
                {
                    extra = "备注";
                }

                if (extra.Length > 0)
                {
                    string when = day.Date == DateTime.Today ? "今天" : Dates.MonthDay(day);
                    string message = string.Format(
                        "{0}的打卡记录里有{1}，取消打卡会一并删除。\r\n\r\n确定要取消吗？", when, extra);
                    DialogResult r = MessageBox.Show(this, message, "取消打卡",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (r != DialogResult.OK)
                    {
                        return;
                    }
                }
                h.Entries.Remove(key);
            }
            else
            {
                h.Entries[key] = new DayEntry(key);
            }

            SaveNow();
            RefreshAll();
        }

        private void OnHeatmapModeChanged()
        {
            _data.HeatmapMode = _heatmap.Mode;
            SaveNow();
            RefreshAll();
        }

        private void CheckDateRollover()
        {
            if (_lastSeenDate == DateTime.Today)
            {
                return;
            }
            _lastSeenDate = DateTime.Today;
            RefreshAll();
        }

        private void NewHabit()
        {
            string initial = HabitDialog.Palette[_data.Habits.Count % HabitDialog.Palette.Length];
            using (HabitDialog dialog = new HabitDialog("新建习惯", "", initial))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                Habit h = new Habit();
                h.Name = dialog.HabitName;
                h.Color = dialog.HabitColor;
                h.CreatedAt = Dates.TodayKey();
                h.Order = _data.Habits.Count;
                _data.Habits.Add(h);
                _data.NormalizeOrder();

                _selectedHabitId = h.Id;
                SaveNow();
                RefreshAll();
            }
        }

        private void RenameHabit()
        {
            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }

            using (HabitDialog dialog = new HabitDialog("编辑习惯", h.Name, h.Color))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                h.Name = dialog.HabitName;
                h.Color = dialog.HabitColor;
                SaveNow();
                RefreshAll();
            }
        }

        private void DeleteHabit()
        {
            Habit h = CurrentHabit();
            if (h == null)
            {
                return;
            }

            string message = string.Format(
                "确定要永久删除《{0}》吗？\r\n\r\n"
                + "它包含 {1} 条打卡记录，删除后无法在软件内恢复。\r\n"
                + "每次保存都会保留上一版 data.json.bak，必要时可手工找回。",
                h.EffectiveName(), h.Entries.Count);

            DialogResult r = MessageBox.Show(this, message, "删除习惯",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (r != DialogResult.OK)
            {
                return;
            }

            _data.Habits.Remove(h);
            _data.NormalizeOrder();
            _selectedHabitId = _data.Habits.Count > 0 ? _data.Habits[0].Id : null;
            SaveNow();
            RefreshAll();
        }

        private void SaveNow()
        {
            if (!_persist)
            {
                return;
            }

            string error;
            if (Storage.Save(_data, _dataPath, out error))
            {
                SetSavedStatus("");
                return;
            }

            _statusSaved.Text = "保存失败";
            _statusSaved.ForeColor = Theme.Danger;

            string message = "保存数据失败：\r\n" + error + "\r\n\r\n"
                + "目标位置：\r\n" + _dataPath + "\r\n\r\n"
                + "如果这个文件夹没有写入权限，可以在「设置」里更换保存位置。\r\n\r\n"
                + "是否现在打开设置？";

            if (MessageBox.Show(this, message, "打卡助手",
                MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                OpenSettings();
            }
        }

        /// <summary>
        /// 打开设置模块。用户改了保存位置就当场切换过去，并把选择记进 config.json。
        /// </summary>
        private void OpenSettings()
        {
            if (_noteTimer != null)
            {
                _noteTimer.Stop();
            }

            using (SettingsDialog dialog = new SettingsDialog(_dataPath, _data, _settings.TryLaunchExplorer))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string newPath = dialog.ResultDataPath;
                bool newTryLaunch = dialog.ResultTryLaunchExplorer;

                bool pathChanged = !string.IsNullOrEmpty(newPath)
                    && !string.Equals(newPath, _dataPath, StringComparison.OrdinalIgnoreCase);
                bool tryLaunchChanged = newTryLaunch != _settings.TryLaunchExplorer;

                if (!pathChanged && !tryLaunchChanged)
                {
                    return;
                }

                _settings.TryLaunchExplorer = newTryLaunch;
                if (pathChanged)
                {
                    _settings.DataPath = newPath;
                }

                string error;
                if (!Storage.SaveSettings(_settings, out error))
                {
                    MessageBox.Show(this,
                        "设置无法写入 config.json：\r\n" + error
                        + "\r\n\r\n本次仍会临时生效，但下次启动会回到原来的设置。",
                        "打卡助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (!pathChanged)
                {
                    return;
                }

                _suppressEvents = true;
                try
                {
                    _dataPath = newPath;

                    string warning;
                    _data = Storage.Load(_dataPath, out warning);
                    _data.NormalizeOrder();
                    _selectedHabitId = _data.Habits.Count > 0 ? _data.Habits[0].Id : null;

                    UpdateStatusPath();
                    RefreshAll();

                    if (File.Exists(_dataPath))
                    {
                        SetSavedStatus("");
                    }
                    else
                    {
                        _statusSaved.Text = "尚未保存";
                        _statusSaved.ForeColor = Theme.TextFaint;
                    }

                    if (!string.IsNullOrEmpty(warning))
                    {
                        MessageBox.Show(this, warning, "数据文件提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                finally
                {
                    _suppressEvents = false;
                }
            }
        }

        private void OpenDataFolder()
        {
            string dir = Path.GetDirectoryName(_dataPath);
            if (string.IsNullOrEmpty(dir))
            {
                dir = Environment.CurrentDirectory;
            }

            // 文件夹先确保存在，"打开"才有意义
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception)
            {
            }

            // 用户在设置里关掉了自动启动，就直接给路径，不做无谓的进程启动
            if (!_settings.TryLaunchExplorer)
            {
                FolderOpener.ReportFailure(this, dir,
                    "已按你的设置关闭自动启动，可在「设置」里重新打开", "数据文件夹路径");
                return;
            }

            FolderOpener.Open(dir, _dataPath, delegate(string error)
            {
                // 回调可能是稍后才触发的，窗口已经关掉就什么都不做
                if (this.IsDisposed)
                {
                    return;
                }
                FolderOpener.ReportFailure(this, dir, error);
            });
        }

        // ---------------- 生命周期 ----------------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!_persist)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_startupWarning))
            {
                MessageBox.Show(this, _startupWarning, "数据文件提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _startupWarning = null;
            }

            // 保存位置不可写时提前提醒，别等用户打完卡才发现存不进去
            string dirError;
            string dir = Path.GetDirectoryName(_dataPath);
            if (!Storage.EnsureDirectoryWritable(dir, out dirError))
            {
                string message = "数据将保存到：\r\n" + _dataPath
                    + "\r\n\r\n但这个位置现在无法写入：\r\n" + dirError
                    + "\r\n\r\n是否现在去「设置」里更换保存位置？";
                if (MessageBox.Show(this, message, "数据位置不可写",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    OpenSettings();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitNote();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_noteTimer != null)
            {
                _noteTimer.Stop();
                _noteTimer.Dispose();
                _noteTimer = null;
            }
            if (_rolloverTimer != null)
            {
                _rolloverTimer.Stop();
                _rolloverTimer.Dispose();
                _rolloverTimer = null;
            }
            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }
            base.OnFormClosed(e);
        }
    }
}
