namespace MutsumiPet.Models;

/// <summary>
/// 表示一段时间范围内的本地使用聚合摘要。
/// </summary>
public sealed class UsageSummary
{
    /// <summary>
    /// 获取摘要标题。
    /// </summary>
    public string Title { get; init; } = "使用摘要";

    /// <summary>
    /// 获取摘要范围开始时间。
    /// </summary>
    public DateTimeOffset RangeStart { get; init; }

    /// <summary>
    /// 获取摘要范围结束时间。
    /// </summary>
    public DateTimeOffset RangeEnd { get; init; }

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

    /// <summary>
    /// 获取上下文切换次数。
    /// </summary>
    public int SwitchCount { get; init; }

    /// <summary>
    /// 获取活跃时间最高的应用列表。
    /// </summary>
    public IReadOnlyList<UsageAppSummary> TopApps { get; init; } = Array.Empty<UsageAppSummary>();

    /// <summary>
    /// 获取摘要是否包含有效数据。
    /// </summary>
    public bool HasData => SessionCount > 0 || ActiveTime > TimeSpan.Zero || IdleTime > TimeSpan.Zero;
}
