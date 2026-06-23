using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class FocusSessionService
{
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _lastCompletedFocusStartedAt;
    private DateTimeOffset? _lastCompletedFocusEndedAt;

    /// <summary>
    /// 获取当前专注或休息状态。
    /// </summary>
    public FocusSessionState State { get; private set; } = FocusSessionState.Idle;

    /// <summary>
    /// 获取当前状态开始时间。
    /// </summary>
    public DateTimeOffset? CurrentStartedAt => _startedAt;

    /// <summary>
    /// 获取最近完成的专注会话开始时间。
    /// </summary>
    public DateTimeOffset? LastCompletedFocusStartedAt => _lastCompletedFocusStartedAt;

    /// <summary>
    /// 获取最近完成的专注会话结束时间。
    /// </summary>
    public DateTimeOffset? LastCompletedFocusEndedAt => _lastCompletedFocusEndedAt;

    /// <summary>
    /// 尝试进入专注状态。
    /// </summary>
    public bool TryStartFocus(DateTimeOffset now)
    {
        if (State != FocusSessionState.Idle)
        {
            return false;
        }

        State = FocusSessionState.Focusing;
        _startedAt = now;
        return true;
    }

    /// <summary>
    /// 尝试结束专注状态并记录完成时间。
    /// </summary>
    public bool TryEndFocus(DateTimeOffset now)
    {
        if (State != FocusSessionState.Focusing || !_startedAt.HasValue)
        {
            return false;
        }

        _lastCompletedFocusStartedAt = _startedAt;
        _lastCompletedFocusEndedAt = now;
        Reset();
        return true;
    }

    /// <summary>
    /// 尝试进入休息状态。
    /// </summary>
    public bool TryStartBreak(DateTimeOffset now)
    {
        if (State != FocusSessionState.Idle)
        {
            return false;
        }

        State = FocusSessionState.Break;
        _startedAt = now;
        return true;
    }

    /// <summary>
    /// 尝试结束休息状态。
    /// </summary>
    public bool TryEndBreak()
    {
        if (State != FocusSessionState.Break)
        {
            return false;
        }

        Reset();
        return true;
    }

    /// <summary>
    /// 获取当前状态快照。
    /// </summary>
    public FocusSessionSnapshot CreateSnapshot(DateTimeOffset now)
    {
        return new FocusSessionSnapshot
        {
            State = State,
            StartedAt = _startedAt,
            EndsAt = null,
            Remaining = TimeSpan.Zero
        };
    }

    /// <summary>
    /// 重置为无专注和无休息状态。
    /// </summary>
    private void Reset()
    {
        State = FocusSessionState.Idle;
        _startedAt = null;
    }
}
