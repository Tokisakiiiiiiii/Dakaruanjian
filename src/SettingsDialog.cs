using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 设置模块：让用户自己选择打卡数据的保存位置。
    ///
    /// 切换位置时不会悄悄丢掉数据 —— 目标位置已有数据、目标位置为空、
    /// 这两种情况都会分别询问用户，明确告知会发生什么。
    /// </summary>
    public class SettingsDialog : Form
    {
        private readonly string _currentPath;
        private readonly AppData _data;
        private readonly bool _tryLaunchInitial;
        private string _targetDir;
        private string _resultPath;

        private TextBox _pathBox;
        private Label _infoLabel;
        private CheckBox _tryLaunch;

        /// <summary>用户确认后的数据文件路径（对话框返回 OK 时有效）。</summary>
        public string ResultDataPath
        {
            get { return _resultPath; }
        }

        /// <summary>用户是否勾选「打开数据文件夹时先尝试启动资源管理器」。</summary>
        public bool ResultTryLaunchExplorer
        {
            get { return _tryLaunch == null || _tryLaunch.Checked; }
        }

        public SettingsDialog(string currentPath, AppData data)
            : this(currentPath, data, true)
        {
        }

        public SettingsDialog(string currentPath, AppData data, bool tryLaunchExplorer)
        {
            _currentPath = string.IsNullOrEmpty(currentPath)
                ? Storage.DefaultDataPath()
                : currentPath;
            _data = data;
            _targetDir = DirectoryOf(_currentPath);
            _tryLaunchInitial = tryLaunchExplorer;

            this.Text = "设置 · 数据保存位置";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = Theme.Base;
            this.ClientSize = new Size(640, 356);
            this.KeyPreview = true;

            BuildUi();
        }

        private static string DirectoryOf(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    return dir;
                }
            }
            catch (Exception)
            {
            }
            return Storage.DefaultDataDirectory();
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.TextAlign = ContentAlignment.TopLeft;
            return l;
        }

        private void BuildUi()
        {
            Label title = MakeLabel("数据保存位置", Theme.H2, Theme.TextMain);
            title.SetBounds(22, 18, 300, 26);

            Label desc = MakeLabel("打卡记录保存在下面这个文件夹里的 data.json 中。",
                Theme.Tiny, Theme.TextMuted);
            desc.SetBounds(22, 46, 580, 18);

            _pathBox = new TextBox();
            _pathBox.SetBounds(22, 72, 470, 26);
            _pathBox.ReadOnly = true;
            _pathBox.Font = Theme.Base;
            _pathBox.BorderStyle = BorderStyle.FixedSingle;
            _pathBox.BackColor = Theme.Hex("#F7F8FA");
            _pathBox.ForeColor = Theme.TextMain;

            ModernButton browse = new ModernButton();
            browse.Text = "选择文件夹…";
            browse.Accent = Theme.Primary;
            browse.Font = Theme.Base;
            browse.SetBounds(500, 71, 118, 28);
            browse.Click += delegate(object s, EventArgs e) { Browse(); };

            ModernButton open = new ModernButton();
            open.Text = "打开所在文件夹";
            open.Accent = Theme.Hex("#EDF0F5");
            open.ForeColor = Theme.TextMain;
            open.Font = Theme.Base;
            open.SetBounds(22, 110, 138, 30);
            open.Click += delegate(object s, EventArgs e) { OpenFolder(); };

            ModernButton reset = new ModernButton();
            reset.Text = "恢复默认位置";
            reset.Accent = Theme.Hex("#EDF0F5");
            reset.ForeColor = Theme.TextMain;
            reset.Font = Theme.Base;
            reset.SetBounds(170, 110, 130, 30);
            reset.Click += delegate(object s, EventArgs e) { ResetDefault(); };

            ModernButton copy = new ModernButton();
            copy.Text = "复制数据路径";
            copy.Accent = Theme.Hex("#EDF0F5");
            copy.ForeColor = Theme.TextMain;
            copy.Font = Theme.Base;
            copy.SetBounds(308, 110, 120, 30);
            copy.Click += delegate(object s, EventArgs e) { CopyPath(); };

            _infoLabel = MakeLabel("", Theme.Small, Theme.TextMuted);
            _infoLabel.SetBounds(22, 150, 598, 98);

            _tryLaunch = new CheckBox();
            _tryLaunch.Text = "打开数据文件夹时，先尝试启动资源管理器";
            _tryLaunch.Font = Theme.Base;
            _tryLaunch.ForeColor = Theme.TextMain;
            _tryLaunch.BackColor = Color.Transparent;
            _tryLaunch.SetBounds(20, 252, 440, 24);
            _tryLaunch.Checked = _tryLaunchInitial;

            Label launchHint = MakeLabel(
                "关掉它就只显示路径，不再尝试启动 explorer.exe —— 某些受限环境里该进程会以 0xC0000142 失败。",
                Theme.Tiny, Theme.TextFaint);
            launchHint.SetBounds(40, 278, 580, 18);

            ModernButton cancel = new ModernButton();
            cancel.Text = "取消";
            cancel.Accent = Theme.Hex("#E8EAEF");
            cancel.ForeColor = Theme.TextMain;
            cancel.Font = Theme.Base;
            cancel.SetBounds(410, 306, 96, 34);
            cancel.Click += delegate(object s, EventArgs e)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            ModernButton ok = new ModernButton();
            ok.Text = "保存";
            ok.Accent = Theme.Primary;
            ok.Font = Theme.Base;
            ok.SetBounds(516, 306, 96, 34);
            ok.Click += delegate(object s, EventArgs e) { Confirm(); };

            this.Controls.Add(title);
            this.Controls.Add(desc);
            this.Controls.Add(_pathBox);
            this.Controls.Add(browse);
            this.Controls.Add(open);
            this.Controls.Add(reset);
            this.Controls.Add(copy);
            this.Controls.Add(_infoLabel);
            this.Controls.Add(_tryLaunch);
            this.Controls.Add(launchHint);
            this.Controls.Add(cancel);
            this.Controls.Add(ok);

            UpdatePathBox();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // 本对话框没有文本输入框，回车直接确认即可
                Confirm();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        // ---------------- 交互 ----------------

        private void UpdatePathBox()
        {
            _pathBox.Text = Path.Combine(_targetDir, Storage.DataFileName);
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if (_infoLabel == null)
            {
                return;
            }

            int habits = 0;
            int entries = 0;
            if (_data != null)
            {
                habits = _data.Habits.Count;
                foreach (Habit h in _data.Habits)
                {
                    entries += h.Entries.Count;
                }
            }

            string sizeText = "尚未创建";
            try
            {
                if (File.Exists(_currentPath))
                {
                    sizeText = FormatSize(new FileInfo(_currentPath).Length);
                }
            }
            catch (Exception)
            {
            }

            bool bakExists = false;
            try
            {
                bakExists = File.Exists(_currentPath + ".bak");
            }
            catch (Exception)
            {
            }

            string target = Path.Combine(_targetDir, Storage.DataFileName);
            string targetState;
            if (string.Equals(target, _currentPath, StringComparison.OrdinalIgnoreCase))
            {
                targetState = "就是现在使用的位置。";
            }
            else if (File.Exists(target))
            {
                targetState = "该文件夹里已经有 data.json，保存时会问你是使用它、还是用当前数据覆盖它。";
            }
            else
            {
                targetState = "该文件夹里还没有 data.json，保存时会问你是否把当前数据复制过去。";
            }

            _infoLabel.Text =
                string.Format("当前数据：{0} 个习惯 · {1} 条打卡记录 · 文件 {2}\r\n", habits, entries, sizeText)
                + string.Format("上一版备份：{0}\r\n", bakExists
                    ? "data.json.bak 存在（可手工恢复）"
                    : "暂无")
                + "\r\n"
                + "新位置：" + targetState + "\r\n"
                + "提示：每次保存都会自动把上一版留成 data.json.bak。";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }
            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0.0") + " KB";
            }
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }

        private void Browse()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择打卡数据的保存文件夹";
                dlg.ShowNewFolderButton = true;
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                if (Directory.Exists(_targetDir))
                {
                    dlg.SelectedPath = _targetDir;
                }

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _targetDir = dlg.SelectedPath;
                UpdatePathBox();
            }
        }

        private void OpenFolder()
        {
            try
            {
                if (!Directory.Exists(_targetDir))
                {
                    Directory.CreateDirectory(_targetDir);
                }
            }
            catch (Exception)
            {
            }

            FolderOpener.Open(_targetDir, Path.Combine(_targetDir, Storage.DataFileName),
                delegate(string error)
                {
                    if (this.IsDisposed)
                    {
                        return;
                    }
                    FolderOpener.ReportFailure(this, _targetDir, error);
                });
        }

        private void CopyPath()
        {
            string path = Path.Combine(_targetDir, Storage.DataFileName);
            if (FolderOpener.CopyPath(path))
            {
                MessageBox.Show(this, "路径已复制到剪贴板：\r\n\r\n" + path, "设置",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    "复制到剪贴板失败，请手动复制下面的路径：\r\n\r\n" + path,
                    "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetDefault()
        {
            _targetDir = Storage.DefaultDataDirectory();
            UpdatePathBox();
        }

        private void Confirm()
        {
            string newPath = Path.Combine(_targetDir, Storage.DataFileName);

            if (string.Equals(newPath, _currentPath, StringComparison.OrdinalIgnoreCase))
            {
                _resultPath = newPath;
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            string error;
            if (!Storage.EnsureDirectoryWritable(_targetDir, out error))
            {
                MessageBox.Show(this,
                    "这个文件夹无法写入：\r\n" + error + "\r\n\r\n请换一个位置。",
                    "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool targetExists = File.Exists(newPath);
            bool copyCurrent = false;

            if (targetExists)
            {
                DialogResult r = MessageBox.Show(this,
                    "该文件夹里已经有一个 data.json。\r\n\r\n"
                    + "「是」：使用该文件夹现有的数据（当前位置的数据保持不动）\r\n"
                    + "「否」：用当前的数据覆盖它\r\n"
                    + "「取消」：什么都不做",
                    "目标位置已有数据", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    return;
                }
                copyCurrent = r == DialogResult.No;
            }
            else
            {
                DialogResult r = MessageBox.Show(this,
                    "是否把当前的数据复制到新位置？\r\n\r\n"
                    + "「是」：复制过去（推荐）\r\n"
                    + "「否」：不复制，在新位置从空数据开始",
                    "复制现有数据", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    return;
                }
                copyCurrent = r == DialogResult.Yes;
            }

            if (copyCurrent)
            {
                try
                {
                    if (File.Exists(_currentPath))
                    {
                        File.Copy(_currentPath, newPath, true);
                    }
                    else if (_data != null)
                    {
                        // 原位置还没有落盘过，直接把内存里的数据写到新位置
                        if (!Storage.Save(_data, newPath, out error))
                        {
                            MessageBox.Show(this, "写入新位置失败：\r\n" + error, "设置",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "复制数据失败：\r\n" + ex.Message, "设置",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _resultPath = newPath;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
