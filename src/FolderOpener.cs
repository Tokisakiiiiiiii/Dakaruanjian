using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>
    /// 用资源管理器打开文件夹。
    ///
    /// 踩过的坑，记在这里免得以后又走回去：
    ///
    ///   1) UseShellExecute = true 不行。
    ///      启动会被委托给外壳（ShellExecuteEx），子进程继承的是外壳的错误模式，
    ///      我们设的 SetErrorMode 不生效，Windows 会自己弹
    ///      「explorer.exe - 应用程序无法正常启动(0xc0000142)」的原始错误框。
    ///
    ///   2) SHOpenFolderAndSelectItems / Shell.Application.Explore 也不行。
    ///      在受限环境里前者会长时间阻塞，后者内部仍走 ShellExecute，
    ///      被拒绝时弹出「Windows 无法访问指定设备、路径或文件」，
    ///      而且整条路实测要花上百秒 —— 会卡死界面。已彻底移除。
    ///
    /// 所以只保留一条路：自己 CreateProcess 起 explorer.exe。
    /// 它是立即返回的（不会阻塞 UI），失败时抛异常我们接得住，
    /// 子进程继承 SEM_FAILCRITICALERRORS 后也不会再弹系统错误框。
    /// 这条路也不通时，就把路径交给用户，弹一个不依赖剪贴板的对话框。
    /// </summary>
    public static class FolderOpener
    {
        private const uint SEM_FAILCRITICALERRORS = 0x0001;
        private const uint SEM_NOGPFAULTERRORBOX = 0x0002;
        private const uint NtStatusBase = 0xC0000000u;

        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode(uint uMode);

        private sealed class LaunchWatch
        {
            public Process Process;
            public Action<string> OnFailure;
            public bool Reported;
        }

        /// <summary>持有被监视的进程，否则可能被 GC 回收导致 Exited 事件不触发。</summary>
        private static readonly List<LaunchWatch> Watched = new List<LaunchWatch>();

        /// <summary>最近一次的结果描述，仅供诊断输出。</summary>
        public static string LastMethod { get; private set; }

        /// <summary>
        /// 打开文件夹并尽量选中其中的文件。
        /// 返回 true 表示已经把启动请求发出去了；
        /// 返回 false 表示立即失败，此时会调用 onFailure 给出原因。
        /// 进程稍后才以 NTSTATUS 失败退出时，也会异步回调 onFailure。
        /// </summary>
        public static bool Open(string dir, string selectFile, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(dir))
            {
                LastMethod = "路径为空";
                if (onFailure != null)
                {
                    onFailure("路径为空。");
                }
                return false;
            }

            string target = string.IsNullOrEmpty(selectFile) ? dir : selectFile;
            string error;
            Process process;

            uint oldMode = SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
            try
            {
                if (!TryStart("/select,\"" + target + "\"", out process, out error)
                    && !TryStart("\"" + dir + "\"", out process, out error))
                {
                    LastMethod = "新起 explorer.exe 失败";
                    if (onFailure != null)
                    {
                        onFailure(error);
                    }
                    return false;
                }
            }
            finally
            {
                // 子进程在 CreateProcess 时已继承错误模式，这里可以立刻还原
                SetErrorMode(oldMode);
            }

            LastMethod = "已请求启动 explorer.exe";
            Watch(process, onFailure);
            return true;
        }

        private static bool TryStart(string arguments, out Process process, out string error)
        {
            process = null;
            error = null;
            try
            {
                string exe = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = File.Exists(exe) ? exe : "explorer.exe";
                info.Arguments = arguments;
                // 必须是 false：只有直接 CreateProcess，子进程才会继承
                // 我们压掉弹窗的错误模式（见类注释第 1 条）
                info.UseShellExecute = false;
                info.CreateNoWindow = true;

                process = Process.Start(info);
                if (process == null)
                {
                    error = "系统没有返回进程对象。";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void Watch(Process process, Action<string> onFailure)
        {
            if (process == null)
            {
                return;
            }

            LaunchWatch watch = new LaunchWatch();
            watch.Process = process;
            watch.OnFailure = onFailure;

            lock (Watched)
            {
                Watched.Add(watch);
            }

            try
            {
                process.EnableRaisingEvents = true;
                process.Exited += delegate(object sender, EventArgs e) { HandleExit(watch); };
            }
            catch (Exception)
            {
            }

            // 失败得极快的进程可能在订阅事件之前就结束了，补一次检查，
            // 否则会变成"点了没反应"的静默失败。
            try
            {
                if (process.HasExited)
                {
                    HandleExit(watch);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void HandleExit(LaunchWatch watch)
        {
            lock (Watched)
            {
                if (watch.Reported)
                {
                    return;
                }
                watch.Reported = true;
                Watched.Remove(watch);
            }

            int code;
            try
            {
                code = watch.Process.ExitCode;
            }
            catch (Exception)
            {
                return;
            }

            // 资源管理器在已有外壳时常以 1 结束，那是正常的；
            // 只有 0xC0000000 及以上的 NTSTATUS 码才算真失败。
            if ((uint)code < NtStatusBase)
            {
                return;
            }

            LastMethod = string.Format("explorer.exe 以 0x{0:X8} 退出", (uint)code);

            if (watch.OnFailure != null)
            {
                watch.OnFailure(string.Format("系统错误 0x{0:X8}", (uint)code));
            }
        }

        /// <summary>把路径放到剪贴板；剪贴板可能被别的程序占住，多试几次。</summary>
        public static bool CopyPath(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                // 第二个参数 true 表示程序退出后剪贴板内容仍然保留
                Clipboard.SetDataObject(text, true, 10, 120);
                return true;
            }
            catch (Exception)
            {
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (Exception)
                {
                    Thread.Sleep(120);
                }
            }
            return false;
        }

        /// <summary>
        /// 打不开时的兜底提示。
        /// 刻意不依赖剪贴板 API —— 受限环境里它也常被挡住，
        /// 所以弹的是把路径预先选中的只读输入框，用户直接 Ctrl+C 即可。
        /// </summary>
        public static void ReportFailure(IWin32Window owner, string dir, string error)
        {
            ReportFailure(owner, dir, error, "无法自动打开资源管理器");
        }

        /// <summary>同上，但可以自定义标题（按设置关闭自动启动时用另一套措辞）。</summary>
        public static void ReportFailure(IWin32Window owner, string dir, string error, string heading)
        {
            using (FolderFallbackDialog dialog = new FolderFallbackDialog(dir, error, heading))
            {
                if (owner == null)
                {
                    dialog.ShowDialog();
                }
                else
                {
                    dialog.ShowDialog(owner);
                }
            }
        }
    }

    /// <summary>兜底对话框：显示路径并预先选中，绕开可能不可用的剪贴板 API。</summary>
    public class FolderFallbackDialog : Form
    {
        private readonly string _dir;

        public FolderFallbackDialog(string dir, string error)
            : this(dir, error, "无法自动打开资源管理器")
        {
        }

        public FolderFallbackDialog(string dir, string error, string heading)
        {
            _dir = dir;

            this.Text = "打开文件夹";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = Theme.Base;
            this.ClientSize = new Size(580, 248);
            this.KeyPreview = true;

            Label title = new Label();
            title.Text = heading;
            title.Font = Theme.H2;
            title.ForeColor = Theme.TextMain;
            title.BackColor = Color.Transparent;
            title.SetBounds(22, 18, 420, 26);

            Label reason = new Label();
            reason.Text = "原因：" + error
                + "\r\n粘贴到资源管理器地址栏即可打开。";
            reason.Font = Theme.Tiny;
            reason.ForeColor = Theme.TextMuted;
            reason.BackColor = Color.Transparent;
            reason.SetBounds(22, 48, 536, 44);

            Label hint = new Label();
            hint.Text = "文件夹路径（已选中，可直接按 Ctrl+C 复制）：";
            hint.Font = Theme.Tiny;
            hint.ForeColor = Theme.TextMuted;
            hint.BackColor = Color.Transparent;
            hint.SetBounds(22, 98, 420, 18);

            TextBox pathBox = new TextBox();
            pathBox.Text = dir;
            pathBox.ReadOnly = true;
            pathBox.Font = Theme.Base;
            pathBox.BorderStyle = BorderStyle.FixedSingle;
            pathBox.BackColor = Theme.Hex("#F7F8FA");
            pathBox.ForeColor = Theme.TextMain;
            pathBox.SetBounds(22, 118, 536, 26);

            Label copied = new Label();
            copied.Text = "";
            copied.Font = Theme.Tiny;
            copied.ForeColor = Theme.Success;
            copied.BackColor = Color.Transparent;
            copied.SetBounds(22, 150, 420, 18);

            ModernButton copy = new ModernButton();
            copy.Text = "复制路径";
            copy.Accent = Theme.Hex("#EDF0F5");
            copy.ForeColor = Theme.TextMain;
            copy.Font = Theme.Base;
            copy.SetBounds(22, 184, 110, 32);
            copy.Click += delegate(object s, EventArgs e)
            {
                if (FolderOpener.CopyPath(_dir))
                {
                    copied.Text = "已复制到剪贴板。";
                }
                else
                {
                    // 剪贴板不可用也没关系，输入框里已选中，Ctrl+C 即可
                    copied.Text = "剪贴板不可用，请在输入框里按 Ctrl+C。";
                }
                pathBox.Focus();
                pathBox.SelectAll();
            };

            ModernButton close = new ModernButton();
            close.Text = "关闭";
            close.Accent = Theme.Primary;
            close.Font = Theme.Base;
            close.SetBounds(458, 184, 100, 32);
            close.Click += delegate(object s, EventArgs e)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.Controls.Add(title);
            this.Controls.Add(reason);
            this.Controls.Add(hint);
            this.Controls.Add(pathBox);
            this.Controls.Add(copied);
            this.Controls.Add(copy);
            this.Controls.Add(close);

            this.Shown += delegate(object s, EventArgs e)
            {
                pathBox.Focus();
                pathBox.SelectAll();
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
