namespace MutsumiPet.Models;

/// <summary>
/// 表示本地使用追踪器处理一次快照后的提醒信号。
/// </summary>
public sealed class UsageTrackingUpdate
{
    /// <summary>
    /// 获取本次是否检测到高频上下文切换。
    /// </summary>
    public bool ContextSwitchingHigh { get; init; }

    /// <summary>
    /// 获取本次是否检测到专注会话中的分心应用。
    /// </summary>
    public bool DistractionDetected { get; init; }

    /// <summary>
    /// 获取本次被写入数据库的会话记录。
    /// </summary>
    public UsageSessionRecord? FlushedRecord { get; init; }

    /// <summary>
    /// 获取不包含任何提醒信号的空结果。
    /// </summary>
    public static UsageTrackingUpdate None { get; } = new();
}
