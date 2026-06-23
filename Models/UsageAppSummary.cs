namespace MutsumiPet.Models;

/// <summary>
/// 表示某个应用在聚合摘要中的使用统计。
/// </summary>
public sealed class UsageAppSummary
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
    /// 获取活跃使用时长。
    /// </summary>
    public TimeSpan ActiveTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// 获取空闲时长。
    /// </summary>
    public TimeSpan IdleTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// 获取会话数量。
    /// </summary>
    public int SessionCount { get; init; }
}
