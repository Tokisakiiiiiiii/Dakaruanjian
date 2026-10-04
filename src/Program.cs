using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DakaHelper
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwRestore = 9;

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dataPath = null;
            string shotPath = null;
            string dialogName = null;
            string tryOpenPath = null;
            int seed = 0;
            bool selfTest = false;
            bool allowMultiple = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    selfTest = true;
                }
                else if (string.Equals(a, "--data", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dataPath = args[++i];
                }
                else if (string.Equals(a, "--shot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    shotPath = args[++i];
                }
                else if (string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int parsed;
                    if (int.TryParse(args[++i], out parsed))
                    {
                        seed = parsed;
                    }
                }
                else if (string.Equals(a, "--dialog", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dialogName = args[++i];
                }
                else if (string.Equals(a, "--tryopen", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    tryOpenPath = args[++i];
                }
                else if (string.Equals(a, "--multi", StringComparison.OrdinalIgnoreCase))
                {
                    allowMultiple = true;
                }
                else if (string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Usage());
                    return 0;
                }
            }

            // dataPath 为 null 时由 MainForm 按「配置文件 -> 默认位置」的顺序解析。
            // 这里只做一次规范化，方便截图模式与日志显示。
            if (dataPath != null)
            {
                try
                {
                    dataPath = Path.GetFullPath(dataPath);
                }
                catch (Exception)
                {
                }
            }

#if SELFTEST
            if (selfTest)
            {
                return SelfTest.Run();
            }
#else
            if (selfTest)
            {
                MessageBox.Show(
                    "这个可执行文件不包含自测代码。\r\n请运行「打卡助手-测试.exe --selftest」。",
                    "打卡助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
#endif

            if (tryOpenPath != null)
            {
                string reason = null;
                bool opened = FolderOpener.Open(tryOpenPath, null,
                    delegate(string error) { reason = error; });

                // 等一会儿，让异步的失败检测有机会触发
                for (int i = 0; i < 30 && reason == null; i++)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("打开文件夹：" + tryOpenPath);
                Console.WriteLine("请求结果：" + (opened ? "已发出启动请求" : "立即失败"));
                Console.WriteLine("最终方式：" + FolderOpener.LastMethod);
                if (!string.IsNullOrEmpty(reason))
                {
                    Console.WriteLine("失败原因：" + reason);
                }
                return (opened && reason == null) ? 0 : 1;
            }

            if (shotPath != null)
            {
                return RunShotMode(dataPath, shotPath, seed, dialogName);
            }

            Mutex mutex = null;
            if (!allowMultiple)
            {
                bool createdNew;
                mutex = new Mutex(true, MutexName(), out createdNew);
                if (!createdNew)
                {
                    ActivateExistingInstance();
                    return 0;
                }
            }

            try
            {
                using (MainForm form = new MainForm(dataPath, true))
                {
                    Application.Run(form);
                }
            }
            finally
            {
                if (mutex != null)
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch (Exception)
                    {
                    }
                    mutex.Close();
                }
            }
            return 0;
        }

        private static string MutexName()
        {
            string user = Environment.UserName;
            if (string.IsNullOrEmpty(user))
            {
                user = "default";
            }
            return "Local\\DakaHelper.SingleInstance." + user;
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                Process[] all = Process.GetProcesses();
                for (int i = 0; i < all.Length; i++)
                {
                    Process p = all[i];
                    try
                    {
                        if (p.Id == Process.GetCurrentProcess().Id)
                        {
                            continue;
                        }
                        string fileName = Path.GetFileNameWithoutExtension(p.MainModule.FileName);
                        if (fileName != null
                            && fileName.IndexOf("打卡助手", StringComparison.Ordinal) >= 0)
                        {
                            IntPtr h = p.MainWindowHandle;
                            if (h != IntPtr.Zero)
                            {
                                ShowWindow(h, SwRestore);
                                SetForegroundWindow(h);
                                return;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // ---------------- 截图自检模式 ----------------

        private static int RunShotMode(string dataPath, string shotPath, int seed, string dialogName)
        {
            string note = "";
            int exitCode = 0;

            try
            {
                AppData data;
                if (seed > 0)
                {
                    data = Storage.BuildDemo(seed);
                }
                else
                {
                    string warning;
                    data = Storage.Load(dataPath, out warning);
                    if (data.Habits.Count == 0)
                    {
                        data = Storage.BuildDemo(20250612);
                    }
                }

                // 把本次渲染所用的数据一并导出，便于用外部工具独立核对界面上的数字
                try
                {
                    File.WriteAllText(shotPath + ".json", Json.Write(data));
                }
                catch (Exception)
                {
                }

                bool rendered;
                if (string.Equals(dialogName, "settings", StringComparison.OrdinalIgnoreCase))
                {
                    using (SettingsDialog dialog = new SettingsDialog(dataPath, data))
                    {
                        rendered = RenderToPng(dialog, shotPath, out note);
                    }
                }
                else if (string.Equals(dialogName, "folderfallback", StringComparison.OrdinalIgnoreCase))
                {
                    using (FolderFallbackDialog dialog = new FolderFallbackDialog(
                        Storage.DefaultDataDirectory(), "系统错误 0xC0000142"))
                    {
                        rendered = RenderToPng(dialog, shotPath, out note);
                    }
                }
                else
                {
                    using (MainForm form = new MainForm(dataPath, data, false))
                    {
                        rendered = RenderToPng(form, shotPath, out note);
                    }
                }

                exitCode = rendered ? 0 : 1;
            }
            catch (Exception ex)
            {
                note = "失败：" + ex.ToString();
                exitCode = 3;
            }

            WriteShotLog(shotPath, note);
            Console.WriteLine("截图：" + shotPath + "  " + note);
            return exitCode;
        }

        /// <summary>把一个窗体离屏渲染成 PNG，返回是否真的渲染出了内容。</summary>
        private static bool RenderToPng(Form form, string shotPath, out string note)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-6000, -6000);
            form.ShowInTaskbar = false;
            form.Show();
            Pump(14);
            form.PerformLayout();
            Pump(4);

            int w = form.Width;
            int h = form.Height;

            using (Bitmap bmp = new Bitmap(w, h))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                bool rendered = !IsNearlyBlank(bmp);

                if (!rendered)
                {
                    // DrawToBitmap 偶发只能画出背景，退回到屏幕抓取（PrintWindow）
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        IntPtr hdc = g.GetHdc();
                        try
                        {
                            rendered = PrintWindow(form.Handle, hdc, 0);
                        }
                        finally
                        {
                            g.ReleaseHdc(hdc);
                        }
                    }
                    rendered = rendered && !IsNearlyBlank(bmp);
                }

                string dir = Path.GetDirectoryName(Path.GetFullPath(shotPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                bmp.Save(shotPath, ImageFormat.Png);

                note = string.Format("尺寸 {0}x{1}，渲染{2}",
                    w, h, rendered ? "正常" : "疑似空白");
                return rendered;
            }
        }

        private static void WriteShotLog(string shotPath, string note)
        {
            try
            {
                File.WriteAllText(shotPath + ".log",
                    "打卡助手截图自检\r\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + note + "\r\n");
            }
            catch (Exception)
            {
            }
        }

        private static void Pump(int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                Application.DoEvents();
                Thread.Sleep(55);
            }
        }

        /// <summary>采样判断位图是否只是纯色背景（说明根本没渲染出来）。</summary>
        private static bool IsNearlyBlank(Bitmap bmp)
        {
            HashSet<int> colors = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y += 3)
            {
                for (int x = 0; x < bmp.Width; x += 3)
                {
                    colors.Add(bmp.GetPixel(x, y).ToArgb());
                    if (colors.Count > 10)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static string Usage()
        {
            return "打卡助手\r\n"
                + "\r\n"
                + "  直接双击运行即可。\r\n"
                + "\r\n"
                + "可选参数：\r\n"
                + "  --data <路径>      指定数据文件（默认 程序目录\\数据\\data.json）\r\n"
                + "  --multi            允许同时打开多个实例\r\n"
                + "  --shot <图片路径>  自检：把主界面渲染成 PNG 后退出（不写入数据）\r\n"
                + "  --seed <数字>      自检：使用演示数据\r\n"
                + "  --dialog <名字>    自检：渲染对话框而不是主窗口（settings / folderfallback）\r\n"
                + "  --tryopen <文件夹> 自检：测试打开文件夹的三种方式，并返回是否成功\r\n"
                + "  --selftest         自检：运行逻辑单元测试（仅测试版可执行文件）\r\n"
                + "  --help             显示本说明\r\n";
        }
    }
}
