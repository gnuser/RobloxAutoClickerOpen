using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RobloxCursorClicker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ClickerForm());
    }
}

internal sealed class ClickerForm : Form
{
    private const int HotkeyStart = 1;
    private const int HotkeyStop = 2;
    private const int HotkeyToggle = 3;
    private const int WmHotkey = 0x0312;
    private const int VkF6 = 0x75;
    private const int VkF7 = 0x76;
    private const int VkF8 = 0x77;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint GaRoot = 2;
    private const int ClickHoldMs = 25;

    private readonly NumericUpDown intervalBox;
    private readonly Label statusLabel;
    private readonly Label countLabel;
    private volatile bool clicking;
    private Thread? clickThread;
    private int intervalMs;
    private int runGeneration;
    private long clickCount;
    private IntPtr mainWindowHandle;

    internal ClickerForm()
    {
        Text = "Roblox 当前鼠标位置连点器";
        ClientSize = new Size(420, 190);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(new Label
        {
            Text = "点击间隔（秒）",
            Location = new Point(22, 24),
            AutoSize = true
        });
        intervalBox = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.01M,
            Maximum = 3600M,
            Increment = 0.1M,
            Value = 3M,
            Location = new Point(155, 20),
            Width = 100
        };
        Controls.Add(intervalBox);

        Controls.Add(new Label
        {
            Text = "把鼠标放在 Roblox 中要点的位置，按 F6 开始。",
            Location = new Point(22, 62),
            AutoSize = true
        });
        Controls.Add(new Label
        {
            Text = "F7 停止，F8 切换。运行时不会切换窗口。",
            Location = new Point(22, 86),
            AutoSize = true
        });
        statusLabel = new Label
        {
            Text = "状态：未运行",
            Location = new Point(22, 123),
            AutoSize = true
        };
        Controls.Add(statusLabel);
        countLabel = new Label
        {
            Text = "已发送：0 次",
            Location = new Point(22, 148),
            AutoSize = true
        };
        Controls.Add(countLabel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        mainWindowHandle = Handle;
        bool start = RegisterHotKey(Handle, HotkeyStart, 0, VkF6);
        bool stop = RegisterHotKey(Handle, HotkeyStop, 0, VkF7);
        bool toggle = RegisterHotKey(Handle, HotkeyToggle, 0, VkF8);
        if (!start || !stop || !toggle)
        {
            statusLabel.Text = "快捷键注册失败：F6/F7/F8 可能被其他程序占用";
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        clicking = false;
        Interlocked.Increment(ref runGeneration);
        UnregisterHotKey(Handle, HotkeyStart);
        UnregisterHotKey(Handle, HotkeyStop);
        UnregisterHotKey(Handle, HotkeyToggle);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            switch (m.WParam.ToInt32())
            {
                case HotkeyStart:
                    StartClicking();
                    break;
                case HotkeyStop:
                    StopClicking();
                    break;
                case HotkeyToggle:
                    if (clicking) StopClicking(); else StartClicking();
                    break;
            }
        }
        base.WndProc(ref m);
    }

    private void StartClicking()
    {
        if (clicking) return;
        intervalMs = Math.Max(1, (int)Math.Round(intervalBox.Value * 1000M));
        Interlocked.Exchange(ref clickCount, 0);
        countLabel.Text = "已发送：0 次";
        clicking = true;
        int generation = Interlocked.Increment(ref runGeneration);
        statusLabel.Text = "状态：运行中";
        clickThread = new Thread(() => ClickLoop(generation))
        {
            IsBackground = true,
            Name = "AutoClickerLoop"
        };
        clickThread.Start();
    }

    private void StopClicking()
    {
        clicking = false;
        Interlocked.Increment(ref runGeneration);
        statusLabel.Text = "状态：已停止";
        countLabel.Text = $"已发送：{Interlocked.Read(ref clickCount)} 次";
    }

    private void ClickLoop(int generation)
    {
        while (clicking && Volatile.Read(ref runGeneration) == generation)
        {
            try
            {
                PerformClick();
            }
            catch (Exception ex)
            {
                clicking = false;
                UpdateStatus("点击失败：" + ex.Message);
                break;
            }
            Thread.Sleep(intervalMs);
        }
    }

    private void PerformClick()
    {
        // Match the referenced project: read the current system cursor, position it there,
        // inject DOWN, hold for 25 ms, and inject UP without activating a window.
        Point point = Cursor.Position;
        IntPtr underCursor = WindowFromPoint(point);
        if (GetAncestor(underCursor, GaRoot) == mainWindowHandle) return;

        SetCursorPos(point.X, point.Y);
        SendMouseFlag(MouseLeftDown);
        try
        {
            Thread.Sleep(ClickHoldMs);
        }
        finally
        {
            SendMouseFlag(MouseLeftUp);
        }

        long count = Interlocked.Increment(ref clickCount);
        if (count == 1 || count % 10 == 0)
            UpdateStatus($"已发送：{count} 次", updateCount: true);
    }

    private void UpdateStatus(string text, bool updateCount = false)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                if (updateCount) countLabel.Text = text;
                else statusLabel.Text = "状态：" + text;
            });
        }
        catch (InvalidOperationException) { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    private static void SendMouseFlag(uint flag)
    {
        Input[] inputs = { new Input { Type = 0, Mouse = new MouseInput { Flags = flag } } };
        if (SendInput(1, inputs, Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, int key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
