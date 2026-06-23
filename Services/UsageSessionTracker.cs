using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class UsageSessionTracker
{
    private static readonly TimeSpan SwitchWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ContextSwitchWarningCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DistractionWarningCooldown = TimeSpan.FromMinutes(10);
    private readonly UsageSessionStore _store;
    private readonly AppClassifierService _classifier;
    private readonly Queue<DateTimeOffset> _recentSwitches = new();
    private CurrentUsageSession? _currentSession;
    private DateTimeOffset _lastContextSwitchWarningAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDistractionWarningAt = DateTimeOffset.MinValue;

    /// <summary>
    /// 初始化本地使用会话追踪器。
    /// </summary>
    public UsageSessionTracker(UsageSessionStore store, AppClassifierService classifier)
    {
        _store = store;
        _classifier = classifier;
    }

    /// <summary>
    /// 处理一次使用状态快照，并在需要时写入结束的会话。
    /// </summary>
    public UsageTrackingUpdate ProcessSnapshot(
        UsageSnapshot snapshot,
        UserSettings settings,
        bool focusSessionActive)
    {
        if (!settings.EnableTracking)
        {
            Flush(snapshot.CapturedAt, settings);
            return UsageTrackingUpdate.None;
        }

        if (_currentSession is null)
        {
            _currentSession = StartSession(snapshot);
            return UsageTrackingUpdate.None;
        }

        AccumulateElapsed(snapshot.CapturedAt);
        var processChanged = !string.Equals(
            _currentSession.ProcessName,
            snapshot.ProcessName,
            StringComparison.OrdinalIgnoreCase);
        var idleChanged = _currentSession.IsIdle != snapshot.IsIdle;

        if (!processChanged && !idleChanged)
        {
            return UsageTrackingUpdate.None;
        }

        if (processChanged)
        {
            _currentSession.SwitchCount++;
            RecordSwitch(snapshot.CapturedAt);
        }

        var flushedRecord = Flush(snapshot.CapturedAt, settings);
        _currentSession = StartSession(snapshot);

        var contextSwitchingHigh = processChanged && CanWarnContextSwitching(snapshot.CapturedAt);
        var distractionDetected = processChanged &&
            focusSessionActive &&
            !snapshot.IsIdle &&
            _classifier.IsDistractingCategory(snapshot.AppCategory) &&
            CanWarnDistraction(snapshot.CapturedAt);

        return new UsageTrackingUpdate
        {
            ContextSwitchingHigh = contextSwitchingHigh,
            DistractionDetected = distractionDetected,
            FlushedRecord = flushedRecord
        };
    }

    /// <summary>
    /// 强制结束当前使用会话并写入数据库。
    /// </summary>
    public UsageSessionRecord? Flush(DateTimeOffset now, UserSettings settings)
    {
        if (_currentSession is null)
        {
            return null;
        }

        AccumulateElapsed(now);
        var record = CreateRecord(_currentSession, now, settings);
        _currentSession = null;
        if (record is not null)
        {
            _store.InsertSession(record);
        }

        return record;
    }

    /// <summary>
    /// 读取当前会话的活跃秒数估计值。
    /// </summary>
    public int GetCurrentActiveSeconds(DateTimeOffset now)
    {
        if (_currentSession is null)
        {
            return 0;
        }

        var elapsed = _currentSession.IsIdle ? TimeSpan.Zero : now - _currentSession.LastObservedAt;
        return _currentSession.ActiveSeconds + Math.Max(0, (int)elapsed.TotalSeconds);
    }

    /// <summary>
    /// 创建新的当前会话。
    /// </summary>
    private static CurrentUsageSession StartSession(UsageSnapshot snapshot)
    {
        return new CurrentUsageSession
        {
            ProcessName = snapshot.ProcessName,
            Category = snapshot.AppCategory,
            WindowTitle = snapshot.WindowTitle,
            StartedAt = snapshot.CapturedAt,
            LastObservedAt = snapshot.CapturedAt,
            IsIdle = snapshot.IsIdle
        };
    }

    /// <summary>
    /// 将上一次观测到本次观测之间的时间归入当前会话。
    /// </summary>
    private void AccumulateElapsed(DateTimeOffset now)
    {
        if (_currentSession is null)
        {
            return;
        }

        var elapsed = now - _currentSession.LastObservedAt;
        if (elapsed <= TimeSpan.Zero)
        {
            return;
        }

        var seconds = Math.Min((int)elapsed.TotalSeconds, 300);
        if (_currentSession.IsIdle)
        {
            _currentSession.IdleSeconds += seconds;
        }
        else
        {
            _currentSession.ActiveSeconds += seconds;
        }

        _currentSession.LastObservedAt = now;
    }

    /// <summary>
    /// 根据当前会话状态创建可落库记录。
    /// </summary>
    private static UsageSessionRecord? CreateRecord(
        CurrentUsageSession session,
        DateTimeOffset endedAt,
        UserSettings settings)
    {
        var totalSeconds = session.ActiveSeconds + session.IdleSeconds;
        if (totalSeconds <= 0 || endedAt <= session.StartedAt)
        {
            return null;
        }

        return new UsageSessionRecord
        {
            ProcessName = session.ProcessName,
            Category = session.Category,
            WindowTitle = settings.StoreWindowTitles ? Truncate(session.WindowTitle, 120) : null,
            StartedAt = session.StartedAt,
            EndedAt = endedAt,
            ActiveSeconds = session.ActiveSeconds,
            IdleSeconds = session.IdleSeconds,
            SwitchCount = session.SwitchCount
        };
    }

    /// <summary>
    /// 记录一次应用切换并清理过期切换时间。
    /// </summary>
    private void RecordSwitch(DateTimeOffset now)
    {
        _recentSwitches.Enqueue(now);
        while (_recentSwitches.Count > 0 && now - _recentSwitches.Peek() > SwitchWindow)
        {
            _recentSwitches.Dequeue();
        }
    }

    /// <summary>
    /// 判断是否应提示高频上下文切换。
    /// </summary>
    private bool CanWarnContextSwitching(DateTimeOffset now)
    {
        if (_recentSwitches.Count < 5 || now - _lastContextSwitchWarningAt < ContextSwitchWarningCooldown)
        {
            return false;
        }

        _lastContextSwitchWarningAt = now;
        return true;
    }

    /// <summary>
    /// 判断是否应提示专注会话中的分心应用。
    /// </summary>
    private bool CanWarnDistraction(DateTimeOffset now)
    {
        if (now - _lastDistractionWarningAt < DistractionWarningCooldown)
        {
            return false;
        }

        _lastDistractionWarningAt = now;
        return true;
    }

    /// <summary>
    /// 截断可能过长的窗口标题。
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private sealed class CurrentUsageSession
    {
        public string ProcessName { get; init; } = "Unknown";
        public AppCategory Category { get; init; } = AppCategory.Other;
        public string WindowTitle { get; init; } = string.Empty;
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset LastObservedAt { get; set; }
        public bool IsIdle { get; init; }
        public int ActiveSeconds { get; set; }
        public int IdleSeconds { get; set; }
        public int SwitchCount { get; set; }
    }
}
