namespace MutsumiPet.Models;

/// <summary>
/// 表示一段已经结束并准备写入本地数据库的应用使用会话。
/// </summary>
public sealed class UsageSessionRecord
{
    /// <summary>
    /// 获取进程名。
    /// </summary>
    public string ProcessName { get; init; } = "Unknown";

    /// <summary>
    /// 获取应用类别。
    /// </summary>
    public AppCategory Category { get; init; } = AppCategory.Other;

    /// <summary>
    /// 获取可选窗口标题；默认隐私配置下为空。
    /// </summary>
    public string? WindowTitle { get; init; }

    /// <summary>
    /// 获取会话开始时间。
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// 获取会话结束时间。
    /// </summary>
    public DateTimeOffset EndedAt { get; init; }

    /// <summary>
    /// 获取活跃秒数。
    /// </summary>
    public int ActiveSeconds { get; init; }

    /// <summary>
    /// 获取空闲秒数。
    /// </summary>
    public int IdleSeconds { get; init; }

    /// <summary>
    /// 获取会话结束时记录的上下文切换次数。
    /// </summary>
    public int SwitchCount { get; init; }

    /// <summary>
    /// 获取总秒数。
    /// </summary>
    public int TotalSeconds => ActiveSeconds + IdleSeconds;
}
