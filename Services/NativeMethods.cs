using System.Runtime.InteropServices;
using System.Text;

namespace MutsuPet.Services;

internal static class NativeMethods
{
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectStateChange = 0x800A;
    internal const uint EventObjectNameChange = 0x800C;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WinEventSkipOwnProcess = 0x0002;
    internal const int HShellFlash = 0x8006;
    internal const int ObjectIdWindow = 0;
    internal const int ChildIdSelf = 0;

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    internal delegate void WinEventProc(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    /// <summary>
    /// 保存 Windows 最后一次输入事件的系统 tick 信息。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    /// <summary>
    /// 保存 Win32 窗口矩形坐标。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// 注册可接收 Shell hook 消息的窗口。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterShellHookWindow(IntPtr hWnd);

    /// <summary>
    /// 取消窗口的 Shell hook 消息注册。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    /// <summary>
    /// 获取指定系统消息名称对应的动态消息 ID。
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessageW(string lpString);

    /// <summary>
    /// 安装跨进程窗口事件钩子。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    /// <summary>
    /// 卸载窗口事件钩子。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>
    /// 枚举当前桌面上的顶层窗口。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// 判断指定窗口当前是否可见。
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// 获取当前前台窗口句柄。
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 获取指定窗口标题文本。
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// 获取指定窗口的类名。
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// 获取窗口所属线程与进程 ID。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 获取指定窗口的屏幕矩形。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out WindowRect lpRect);

    /// <summary>
    /// 获取最后一次键盘或鼠标输入的 tick 信息。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
