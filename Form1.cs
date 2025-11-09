using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StudioBrightnessControl
{
    public partial class Form1 : Form
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WH_KEYBOARD_LL = 13;

        private const uint VK_LSHIFT = 0xA0;
        private const uint VK_LWIN = 0x5B;
        private const uint VK_LEFT = 0x25;
        private const uint VK_RIGHT = 0x27;

        private static readonly uint[] BRIGHTNESS_STEPS = { 400, 2400, 4400, 7200, 10000, 15000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 55000, 60000 };

        private IntPtr hook = IntPtr.Zero;
        private bool holdKey1Down = false;
        private bool holdKey2Down = false;
        private uint currentBrightnessIndex = 8;
        private uint currentBrightness = 30000;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenuStrip;

        // 键盘钩子委托
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc keyboardProc;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private ToolStripMenuItem settingsItem;
        private void InitializeTrayIcon()
        {
            contextMenuStrip = new ContextMenuStrip();

            // 添加菜单项
            var increaseItem = new ToolStripMenuItem("增加亮度 (LShift+LWin+Right)");
            increaseItem.Click += (s, e) => OnStepUp();

            var decreaseItem = new ToolStripMenuItem("减少亮度 (LShift+LWin+Left)");
            decreaseItem.Click += (s, e) => OnStepDown();

            var settingsItem = new ToolStripMenuItem("亮度设置...");
            settingsItem.Click += (s, e) => ShowSettingsDialog();

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => this.Close();

            contextMenuStrip.Items.Add(increaseItem);
            contextMenuStrip.Items.Add(decreaseItem);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(settingsItem);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(exitItem);

            notifyIcon = new NotifyIcon();

            // 使用指定的图标文件
            try
            {
                // 方法1: 直接从文件路径加载
                string iconPath = Path.Combine(Application.StartupPath, "Resources", "brightness.ico");
                notifyIcon.Icon = new Icon(iconPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载图标文件失败: {ex.Message}");

                try
                {
                    // 方法2: 尝试从嵌入资源加载
                    notifyIcon.Icon = LoadIconFromResource();
                }
                catch
                {
                    // 方法3: 使用备用图标
                    notifyIcon.Icon = SystemIcons.Application;
                    Console.WriteLine("使用默认应用程序图标");
                }
            }

            notifyIcon.Text = "Studio Brightness Control";
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += (s, e) => ShowSettingsDialog();
            notifyIcon.MouseMove += NotifyIcon_MouseMove;
        }

        // 从嵌入资源加载图标的方法
        private Icon LoadIconFromResource()
        {
            // 获取当前程序集
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            // 尝试从嵌入资源加载
            using (var stream = assembly.GetManifestResourceStream("StudioBrightnessControl.Resources.brightness-dark.ico"))
            {
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }

            throw new FileNotFoundException("找不到图标资源");
        }
        private void NotifyIcon_MouseMove(object sender, MouseEventArgs e)
        {
            // 在托盘图标提示中显示当前亮度
            int level = (int)currentBrightnessIndex + 1;
            int percentage = (int)((currentBrightnessIndex * 100.0) / 14); // 14个间隔
            notifyIcon.Text = $"Studio Brightness - 级别 {level}/15 ({percentage}%)";
        }
        private async void ShowSettingsDialog()
        {
            // 保存原始亮度值
            uint originalBrightness = currentBrightness;

            // 确保我们有最新的亮度值
            var (result, brightness) = await HIDHelper.GetBrightnessAsync();
            if (result == 0)
            {
                currentBrightness = brightness;
                originalBrightness = brightness;

                // 更新当前亮度级别
                for (currentBrightnessIndex = 0; currentBrightnessIndex < BRIGHTNESS_STEPS.Length; currentBrightnessIndex++)
                {
                    if (currentBrightnessIndex == BRIGHTNESS_STEPS.Length - 1 || BRIGHTNESS_STEPS[currentBrightnessIndex + 1] > currentBrightness)
                        break;
                }
            }

            using (var settingsForm = new SettingsForm(originalBrightness))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    // 用户点击了应用，保存新的亮度设置
                    currentBrightness = settingsForm.SelectedBrightness;

                    // 更新亮度级别索引
                    for (currentBrightnessIndex = 0; currentBrightnessIndex < BRIGHTNESS_STEPS.Length; currentBrightnessIndex++)
                    {
                        if (BRIGHTNESS_STEPS[currentBrightnessIndex] == currentBrightness)
                            break;
                    }

                    // 更新托盘提示
                    NotifyIcon_MouseMove(null, null);

                    ShowBalloonTip("亮度设置", $"亮度已保存为级别 {currentBrightnessIndex + 1}", ToolTipIcon.Info);
                }
                else
                {
                    // 用户点击取消或关闭窗口，恢复原始亮度
                    currentBrightness = originalBrightness;

                    // 更新亮度级别索引
                    for (currentBrightnessIndex = 0; currentBrightnessIndex < BRIGHTNESS_STEPS.Length; currentBrightnessIndex++)
                    {
                        if (BRIGHTNESS_STEPS[currentBrightnessIndex] == currentBrightness)
                            break;
                    }

                    // 更新托盘提示
                    NotifyIcon_MouseMove(null, null);
                }
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public Form1()
        {
            InitializeComponent1();
        }

        private void InitializeComponent1()
        {
            this.SuspendLayout();
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(300, 200);
            this.Name = "Form1";
            this.Text = "Studio Brightness Control";
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.ResumeLayout(false);
        }
        private async void TestBrightnessControl()
        {
            Console.WriteLine("Testing brightness control...");

            // 测试获取亮度
            var (result, brightness) = await HIDHelper.GetBrightnessAsync();
            if (result == 0)
            {
                Console.WriteLine($"Current brightness: {brightness}");

                // 测试设置亮度
                uint testBrightness = (uint)(brightness > 30000 ? 25000 : 35000);
                Console.WriteLine($"Setting brightness to: {testBrightness}");

                int setResult = await HIDHelper.SetBrightnessAsync(testBrightness);
                Console.WriteLine($"SetBrightness result: {setResult}");

                if (setResult == 0)
                {
                    Console.WriteLine("Brightness control test passed!");
                }
                else
                {
                    Console.WriteLine($"Brightness control test failed: {setResult}");
                }
            }
            else
            {
                Console.WriteLine($"GetBrightness failed: {result}");
            }
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            await InitializeApplication();

            // 取消注释下面这行来测试亮度控制
            TestBrightnessControl();
        }

        private async Task InitializeApplication()
        {
            Console.WriteLine("Initializing Studio Brightness Control...");

            // 初始化系统托盘图标
            InitializeTrayIcon();

            // 尝试初始化 HID
            int maxRetries = 10;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                Console.WriteLine($"HID initialization attempt {retry + 1}/{maxRetries}");

                int initResult = await HIDHelper.InitAsync();
                Console.WriteLine($"HID Init result: {initResult}");

                if (initResult == 0)
                {
                    Console.WriteLine("HID initialized successfully!");
                    break;
                }
                else if (initResult == -11) // 设备未找到
                {
                    if (retry < maxRetries - 1)
                    {
                        Console.WriteLine("Device not found, retrying in 2 seconds...");
                        await Task.Delay(2000);
                    }
                    else
                    {
                        ShowError("Apple Studio Display not found. Please ensure the display is connected and try again.");
                        return;
                    }
                }
                else
                {
                    ShowError($"Failed to initialize HID: {initResult}");
                    return;
                }
            }

            // 获取当前亮度
            Console.WriteLine("Getting current brightness...");
            var (result, brightness) = await HIDHelper.GetBrightnessAsync();
            Console.WriteLine($"GetBrightness result: {result}, brightness: {brightness}");

            if (result == 0)
            {
                currentBrightness = brightness;
                // 找到对应的亮度级别
                for (currentBrightnessIndex = 0; currentBrightnessIndex < BRIGHTNESS_STEPS.Length; currentBrightnessIndex++)
                {
                    if (currentBrightnessIndex == BRIGHTNESS_STEPS.Length - 1 || BRIGHTNESS_STEPS[currentBrightnessIndex + 1] > currentBrightness)
                        break;
                }
                Console.WriteLine($"Current brightness level: {currentBrightnessIndex + 1}");
            }
            else
            {
                currentBrightnessIndex = 8;
                currentBrightness = 30000;
                Console.WriteLine("Using default brightness level");
            }

            // 安装键盘钩子
            Console.WriteLine("Installing keyboard hook...");
            InstallKeyboardHook();

            this.Hide();
            ShowBalloonTip("Studio Brightness Control", "Application started successfully", ToolTipIcon.Info);
            Console.WriteLine("Application initialized and running in system tray");
        }

        //private void InitializeTrayIcon()
        //{
        //    contextMenuStrip = new ContextMenuStrip();

        //    // 添加菜单项
        //    var increaseItem = new ToolStripMenuItem("增加亮度 (LShift+LWin+Right)");
        //    increaseItem.Click += (s, e) => OnStepUp();

        //    var decreaseItem = new ToolStripMenuItem("减少亮度 (LShift+LWin+Left)");
        //    decreaseItem.Click += (s, e) => OnStepDown();

        //    var exitItem = new ToolStripMenuItem("退出");
        //    exitItem.Click += (s, e) => this.Close();

        //    contextMenuStrip.Items.Add(increaseItem);
        //    contextMenuStrip.Items.Add(decreaseItem);
        //    contextMenuStrip.Items.Add(new ToolStripSeparator());
        //    contextMenuStrip.Items.Add(exitItem);

        //    notifyIcon = new NotifyIcon();
        //    notifyIcon.Icon = SystemIcons.Application;
        //    notifyIcon.Text = "Studio Brightness Control";
        //    notifyIcon.ContextMenuStrip = contextMenuStrip;
        //    notifyIcon.Visible = true;
        //    notifyIcon.DoubleClick += (s, e) => ShowBalloonTip("亮度控制", $"当前亮度级别: {currentBrightnessIndex + 1}/15", ToolTipIcon.Info);
        //}

        private async Task WaitForMonitorConnection()
        {
            const int waitMonitorConnection = 1000; // 1秒
            const int maxRetries = 180; // 3分钟

            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                await Task.Delay(waitMonitorConnection);

                int result = await HIDHelper.InitAsync();
                if (result == 0)
                {
                    ShowBalloonTip("显示器已连接", "Apple Studio Display 已成功连接", ToolTipIcon.Info);
                    return;
                }
            }

            ShowError($"在 {maxRetries} 秒内未检测到显示器连接");
            this.Close();
        }

        private void InstallKeyboardHook()
        {
            keyboardProc = HookCallback;
            IntPtr moduleHandle = GetModuleHandle(null);
            hook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, moduleHandle, 0);

            if (hook == IntPtr.Zero)
            {
                ShowError("无法安装键盘钩子");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT kbdStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (kbdStruct.vkCode == VK_LSHIFT)
                    {
                        holdKey1Down = true;
                    }
                    else if (kbdStruct.vkCode == VK_LWIN)
                    {
                        holdKey2Down = true;
                    }
                    else if (kbdStruct.vkCode == VK_LEFT && holdKey1Down && holdKey2Down)
                    {
                        OnStepDown();
                    }
                    else if (kbdStruct.vkCode == VK_RIGHT && holdKey1Down && holdKey2Down)
                    {
                        OnStepUp();
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    if (kbdStruct.vkCode == VK_LSHIFT)
                    {
                        holdKey1Down = false;
                    }
                    else if (kbdStruct.vkCode == VK_LWIN)
                    {
                        holdKey2Down = false;
                    }
                }
            }

            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        private async void OnStepDown()
        {
            if (currentBrightnessIndex > 0)
            {
                currentBrightness = BRIGHTNESS_STEPS[--currentBrightnessIndex];
            }

            int err = await HIDHelper.SetBrightnessAsync(currentBrightness);
            if (err < 0)
            {
                ShowError($"设置亮度失败: {err}");
                currentBrightnessIndex = 8;
                currentBrightness = 30000;
            }
            else
            {
                //ShowBalloonTip("亮度调整", $"亮度降低到级别 {currentBrightnessIndex + 1}", ToolTipIcon.Info);
            }
        }

        private async void OnStepUp()
        {
            if (currentBrightnessIndex < BRIGHTNESS_STEPS.Length - 1)
            {
                currentBrightness = BRIGHTNESS_STEPS[++currentBrightnessIndex];
            }

            int err = await HIDHelper.SetBrightnessAsync(currentBrightness);
            if (err < 0)
            {
                ShowError($"设置亮度失败: {err}");
                currentBrightnessIndex = 8;
                currentBrightness = 30000;
            }
            else
            {
                //ShowBalloonTip("亮度调整", $"亮度增加到级别 {currentBrightnessIndex + 1}", ToolTipIcon.Info);
            }
        }

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon)
        {
            notifyIcon?.ShowBalloonTip(1000, title, message, icon);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 清理资源
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }

            HIDHelper.Deinit();

            notifyIcon.Visible = false;
            notifyIcon?.Dispose();
            contextMenuStrip?.Dispose();
        }
    }
}