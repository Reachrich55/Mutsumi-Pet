namespace MutsumiPet.Models;

/// <summary>
/// 表示专注计时器当前状态的只读快照。
/// </summary>
public sealed class FocusSessionSnapshot
{
    /// <summary>
    /// 获取专注计时器当前状态。
    /// </summary>
    public FocusSessionState State { get; init; } = FocusSessionState.Idle;

    /// <summary>
    /// 获取当前阶段开始时间。
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// 获取当前阶段计划结束时间。
    /// </summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>
    /// 获取当前阶段剩余时间。
    /// </summary>
    public TimeSpan Remaining { get; init; } = TimeSpan.Zero;
}
