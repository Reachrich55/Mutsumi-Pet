namespace MutsuPet.Models;

/// <summary>
/// 表示一次从 Windows API 采集到的电脑使用状态。
/// </summary>
public sealed class UsageSnapshot
{
    /// <summary>
    /// 获取采集状态的本地时间。
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; }

    /// <summary>
    /// 获取前台窗口所属进程名。
    /// </summary>
    public string ProcessName { get; init; } = "Unknown";

    /// <summary>
    /// 获取前台窗口标题。
    /// </summary>
    public string WindowTitle { get; init; } = "Unknown";

    /// <summary>
    /// 获取距离最后一次用户输入经过的秒数。
    /// </summary>
    public uint IdleSeconds { get; init; }

    /// <summary>
    /// 获取当前前台窗口已持续停留的时间。
    /// </summary>
    public TimeSpan ActiveWindowDuration { get; init; }

    /// <summary>
    /// 获取最近一次检测到的使用事件类型。
    /// </summary>
    public UsageEventKind EventKind { get; init; } = UsageEventKind.Routine;

    /// <summary>
    /// 获取最近一次检测到的使用事件描述。
    /// </summary>
    public string RecentEvent { get; init; } = "常规观察";

    /// <summary>
    /// 获取 Windows 会话是否处于锁定状态。
    /// </summary>
    public bool IsSessionLocked { get; init; }
}
