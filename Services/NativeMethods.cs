using System.Runtime.InteropServices;
using System.Text;

namespace MutsuPet.Services;

internal static class NativeMethods
{
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
    /// 获取窗口所属线程与进程 ID。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 获取最后一次键盘或鼠标输入的 tick 信息。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
