using System.Net.Http;
using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class PetInteractionService
{
    private const int TextLengthLimit = 900;
    private static readonly TimeSpan GlobalInteractionCooldown = TimeSpan.FromMinutes(3);
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly LlmClient _llmClient;
    private readonly SettingsService _settingsService;
    private readonly UsageSessionTracker _usageSessionTracker;
    private readonly FocusSessionService _focusSessionService;
    private readonly UsageSummaryService _usageSummaryService;
    private readonly Dictionary<InteractionTrigger, DateTimeOffset> _lastTriggerTimes = new();
    private DateTimeOffset _lastAnyTriggerAt = DateTimeOffset.MinValue;
    private string? _lastLlmLine;

    /// <summary>
    /// 初始化桌宠互动编排服务。
    /// </summary>
    public PetInteractionService(
        WindowsUsageMonitor usageMonitor,
        LlmClient llmClient,
        SettingsService settingsService,
        UsageSessionTracker usageSessionTracker,
        FocusSessionService focusSessionService,
        UsageSummaryService usageSummaryService)
    {
        _usageMonitor = usageMonitor;
        _llmClient = llmClient;
        _settingsService = settingsService;
        _usageSessionTracker = usageSessionTracker;
        _focusSessionService = focusSessionService;
        _usageSummaryService = usageSummaryService;
    }

    /// <summary>
    /// 获取当前专注或休息状态。
    /// </summary>
    public FocusSessionState CurrentFocusState => _focusSessionService.State;

    /// <summary>
    /// 根据当前用户状态决定是否生成下一句互动文本。
    /// </summary>
    public async Task<string?> GetNextLineAsync(bool force, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        var trackingUpdate = _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);
        var trigger = SelectTrigger(snapshot, trackingUpdate, force, settings);
        if (trigger is null)
        {
            return null;
        }

        if (trigger.Value == InteractionTrigger.FocusSessionEnded)
        {
            _usageSessionTracker.Flush(snapshot.CapturedAt, settings);
        }

        var summary = CreateSummaryForTrigger(trigger.Value, snapshot.CapturedAt);
        return await GenerateOrFallbackAsync(
            snapshot,
            trigger.Value,
            messageNotification: null,
            summary,
            cancellationToken);
    }

    /// <summary>
    /// 在暂停互动时仅处理本地追踪和专注计时，不生成气泡文本。
    /// </summary>
    public void ProcessTrackingOnly()
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);
    }

    /// <summary>
    /// 根据聊天软件新消息信号生成优先级更高的互动文本。
    /// </summary>
    public async Task<string?> GetMessageLineAsync(
        MessageNotification notification,
        CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        if (!settings.EnableMessageReminders)
        {
            return null;
        }

        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);
        var trigger = SelectMessageTrigger(notification);
        if (trigger is null || !CanTriggerMessage(trigger.Value, snapshot.CapturedAt))
        {
            return null;
        }

        return await GenerateOrFallbackAsync(
            snapshot,
            trigger.Value,
            notification,
            summary: null,
            cancellationToken);
    }

    /// <summary>
    /// 启动一个专注会话并生成开始提示。
    /// </summary>
    public async Task<string?> StartFocusSessionAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        if (_focusSessionService.State == FocusSessionState.Break)
        {
            return "正在休息中。先输入 /结束休息，再开始专注。";
        }

        if (!_focusSessionService.TryStartFocus(snapshot.CapturedAt))
        {
            return "已经在专注中了。需要结束时输入 /结束专注。";
        }

        _usageSessionTracker.ProcessSnapshot(snapshot, settings, focusSessionActive: true);
        return await GenerateOrFallbackAsync(
            snapshot,
            InteractionTrigger.FocusSessionStarted,
            messageNotification: null,
            summary: null,
            cancellationToken);
    }

    /// <summary>
    /// 结束当前专注会话并生成专注摘要。
    /// </summary>
    public async Task<string?> EndFocusSessionAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        if (!_focusSessionService.TryEndFocus(snapshot.CapturedAt))
        {
            return "当前没有正在进行的专注。输入 /专注 可以开始一轮。";
        }

        _usageSessionTracker.ProcessSnapshot(snapshot, settings, focusSessionActive: false);
        _usageSessionTracker.Flush(snapshot.CapturedAt, settings);
        var summary = CreateSummaryForTrigger(InteractionTrigger.FocusSessionEnded, snapshot.CapturedAt);
        return await GenerateOrFallbackAsync(
            snapshot,
            InteractionTrigger.FocusSessionEnded,
            messageNotification: null,
            summary,
            cancellationToken);
    }

    /// <summary>
     /// 启动一个休息会话并生成休息提示。
     /// </summary>
    public async Task<string?> StartBreakSessionAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        if (_focusSessionService.State == FocusSessionState.Focusing)
        {
            return "正在专注中。先输入 /结束专注，再开始休息。";
        }

        if (!_focusSessionService.TryStartBreak(snapshot.CapturedAt))
        {
            return "已经在休息中了。需要结束时输入 /结束休息。";
        }

        _usageSessionTracker.ProcessSnapshot(snapshot, settings, focusSessionActive: false);
        return await GenerateOrFallbackAsync(
            snapshot,
            InteractionTrigger.BreakStarted,
            messageNotification: null,
            summary: null,
            cancellationToken);
    }

    /// <summary>
    /// 结束当前休息状态。
    /// </summary>
    public async Task<string?> EndBreakSessionAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        if (!_focusSessionService.TryEndBreak())
        {
            return "当前没有正在进行的休息。输入 /休息 可以切到休息状态。";
        }

        _usageSessionTracker.ProcessSnapshot(snapshot, settings, focusSessionActive: false);
        return await GenerateOrFallbackAsync(
            snapshot,
            InteractionTrigger.BreakEnded,
            messageNotification: null,
            summary: null,
            cancellationToken);
    }

    /// <summary>
     /// 生成今日使用摘要。
     /// </summary>
    public async Task<string?> GetTodaySummaryAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);
        _usageSessionTracker.Flush(snapshot.CapturedAt, settings);
        var summary = _usageSummaryService.GetTodaySummary(snapshot.CapturedAt);
        return await GenerateOrFallbackAsync(
            snapshot,
            InteractionTrigger.DailySummaryReady,
            messageNotification: null,
            summary,
            cancellationToken);
    }

    /// <summary>
    /// 根据用户输入生成对话窗口回复。
    /// </summary>
    public async Task<string> GetChatReplyAsync(
        string userMessage,
        string memoryContext,
        CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);

        string? line = null;
        if (settings.EnableLlm)
        {
            try
            {
                line = await _llmClient.GenerateChatReplyAsync(
                    snapshot,
                    userMessage,
                    memoryContext,
                    _focusSessionService.CreateSnapshot(snapshot.CapturedAt),
                    _lastLlmLine,
                    settings,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                line = null;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                line = null;
            }
        }

        var normalized = NormalizeLine(line);
        if (normalized is not null)
        {
            _lastLlmLine = normalized;
            return normalized;
        }

        return "我现在没连上 LLM，但我还在。可以检查 .env 配置，或者先用 /摘要 看看本地记录。";
    }

    /// <summary>
     /// 结束当前本地使用会话并写入数据库。
     /// </summary>
    public void FlushTracking()
    {
        _usageSessionTracker.Flush(DateTimeOffset.Now, _settingsService.Current);
    }

    /// 调用 LLM 或回落到本地模板生成文本。
    /// </summary>
    private async Task<string?> GenerateOrFallbackAsync(
        UsageSnapshot snapshot,
        InteractionTrigger trigger,
        MessageNotification? messageNotification,
        UsageSummary? summary,
        CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        string? line = null;
        if (settings.EnableLlm)
        {
            try
            {
                line = await _llmClient.GenerateLineAsync(
                    snapshot,
                    trigger,
                    messageNotification,
                    summary,
                    _focusSessionService.CreateSnapshot(snapshot.CapturedAt),
                    _lastLlmLine,
                    settings,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                line = null;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                line = null;
            }
        }

        MarkTriggered(trigger, snapshot.CapturedAt);
        var normalizedLlmLine = NormalizeLine(line);
        if (normalizedLlmLine is not null)
        {
            _lastLlmLine = normalizedLlmLine;
            return normalizedLlmLine;
        }

        return NormalizeLine(GetFallbackLine(snapshot, trigger, messageNotification, summary, settings));
    }

    /// <summary>
    /// 按触发器创建对应的聚合摘要。
    /// </summary>
    private UsageSummary? CreateSummaryForTrigger(InteractionTrigger trigger, DateTimeOffset now)
    {
        if (!_settingsService.Current.EnableUsageSummary)
        {
            return null;
        }

        if (trigger == InteractionTrigger.FocusSessionEnded &&
            _focusSessionService.LastCompletedFocusStartedAt.HasValue &&
            _focusSessionService.LastCompletedFocusEndedAt.HasValue)
        {
            return _usageSummaryService.GetFocusSummary(
                _focusSessionService.LastCompletedFocusStartedAt.Value,
                _focusSessionService.LastCompletedFocusEndedAt.Value);
        }

        return null;
    }

    /// <summary>
    /// 按事件类型和冷却时间选择本次互动触发器。
    /// </summary>
    private InteractionTrigger? SelectTrigger(
        UsageSnapshot snapshot,
        UsageTrackingUpdate trackingUpdate,
        bool force,
        UserSettings settings)
    {
        if (force)
        {
            return InteractionTrigger.ManualRefresh;
        }

        var trigger = (trackingUpdate.DistractionDetected ? InteractionTrigger.DistractionDetected : (InteractionTrigger?)null) ??
            (trackingUpdate.ContextSwitchingHigh ? InteractionTrigger.ContextSwitchingHigh : (InteractionTrigger?)null) ??
            (snapshot.EventKind switch
            {
                UsageEventKind.Startup => InteractionTrigger.Startup,
                UsageEventKind.SessionUnlocked => InteractionTrigger.SessionUnlock,
                UsageEventKind.IdleReturned => InteractionTrigger.IdleReturn,
                UsageEventKind.ContinuousUse => InteractionTrigger.ContinuousUse,
                UsageEventKind.AppDwell when snapshot.AppCategory == AppCategory.Focus => InteractionTrigger.HighFocusApp,
                _ => (InteractionTrigger?)null
            });

        if (trigger == InteractionTrigger.DailySummaryReady && !settings.EnableUsageSummary)
        {
            return null;
        }

        return trigger.HasValue && CanTrigger(trigger.Value, snapshot.CapturedAt) ? trigger : null;
    }

    /// <summary>
    /// 判断指定触发器是否已经过了全局与分类冷却时间。
    /// </summary>
    private bool CanTrigger(InteractionTrigger trigger, DateTimeOffset now)
    {
        if (BypassesGlobalCooldown(trigger))
        {
            return true;
        }

        if (trigger != InteractionTrigger.HighFocusApp &&
            now - _lastAnyTriggerAt < GlobalInteractionCooldown)
        {
            return false;
        }

        if (!_lastTriggerTimes.TryGetValue(trigger, out var lastTriggerAt))
        {
            return true;
        }

        return now - lastTriggerAt >= GetCooldown(trigger);
    }

    /// <summary>
    /// 判断触发器是否绕过全局冷却时间。
    /// </summary>
    private static bool BypassesGlobalCooldown(InteractionTrigger trigger)
    {
        return trigger is InteractionTrigger.ManualRefresh
            or InteractionTrigger.FocusSessionStarted
            or InteractionTrigger.FocusSessionEnded
            or InteractionTrigger.BreakStarted
            or InteractionTrigger.BreakEnded
            or InteractionTrigger.BreakDue
            or InteractionTrigger.DailySummaryReady;
    }

    /// <summary>
    /// 获取不同互动触发器对应的冷却时间。
    /// </summary>
    private static TimeSpan GetCooldown(InteractionTrigger trigger)
    {
        return trigger switch
        {
            InteractionTrigger.ManualRefresh => TimeSpan.Zero,
            InteractionTrigger.FocusSessionStarted => TimeSpan.Zero,
            InteractionTrigger.FocusSessionEnded => TimeSpan.Zero,
            InteractionTrigger.BreakStarted => TimeSpan.Zero,
            InteractionTrigger.BreakEnded => TimeSpan.Zero,
            InteractionTrigger.BreakDue => TimeSpan.Zero,
            InteractionTrigger.DailySummaryReady => TimeSpan.Zero,
            InteractionTrigger.QqMessageReceived => TimeSpan.FromSeconds(20),
            InteractionTrigger.WechatMessageReceived => TimeSpan.FromSeconds(20),
            InteractionTrigger.ContextSwitchingHigh => TimeSpan.FromMinutes(10),
            InteractionTrigger.DistractionDetected => TimeSpan.FromMinutes(10),
            InteractionTrigger.Startup => TimeSpan.FromMinutes(5),
            InteractionTrigger.SessionUnlock => TimeSpan.FromMinutes(5),
            InteractionTrigger.IdleReturn => TimeSpan.FromMinutes(5),
            InteractionTrigger.HighFocusApp => TimeSpan.FromMinutes(10),
            InteractionTrigger.ContinuousUse => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// 记录触发器最近一次成功产生互动的时间。
    /// </summary>
    private void MarkTriggered(InteractionTrigger trigger, DateTimeOffset now)
    {
        _lastAnyTriggerAt = now;
        _lastTriggerTimes[trigger] = now;
    }

    /// <summary>
    /// 按消息来源选择聊天软件消息触发器。
    /// </summary>
    private static InteractionTrigger? SelectMessageTrigger(MessageNotification notification)
    {
        return notification.SourceKind switch
        {
            NotificationSourceKind.Qq => InteractionTrigger.QqMessageReceived,
            NotificationSourceKind.WeChat => InteractionTrigger.WechatMessageReceived,
            _ => null
        };
    }

    /// <summary>
    /// 判断聊天软件消息触发器是否已经过了较短的防刷屏冷却时间。
    /// </summary>
    private bool CanTriggerMessage(InteractionTrigger trigger, DateTimeOffset now)
    {
        if (now - _lastAnyTriggerAt < TimeSpan.FromSeconds(5))
        {
            return false;
        }

        if (!_lastTriggerTimes.TryGetValue(trigger, out var lastTriggerAt))
        {
            return true;
        }

        return now - lastTriggerAt >= GetCooldown(trigger);
    }

    /// <summary>
    /// 清理 LLM 文本以适配图片气泡展示。
    /// </summary>
    private static string? NormalizeLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var normalized = line
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim()
            .Trim('"', '\'', '“', '”', '‘', '’');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= TextLengthLimit
            ? normalized
            : normalized[..(TextLengthLimit - 1)] + "…";
    }

    /// <summary>
    /// 在 LLM 不可用时根据触发器生成本地备用台词。
    /// </summary>
    private string GetFallbackLine(
        UsageSnapshot snapshot,
        InteractionTrigger trigger,
        MessageNotification? messageNotification,
        UsageSummary? summary,
        UserSettings settings)
    {
        if (messageNotification is not null)
        {
            return GetMessageFallbackLine(messageNotification);
        }

        if (summary is not null)
        {
            return _usageSummaryService.BuildFallbackSummary(summary);
        }

        return trigger switch
        {
            InteractionTrigger.Startup => "我醒啦，今天也慢慢来。",
            InteractionTrigger.SessionUnlock => "欢迎回来，刚才休息得还好吗？",
            InteractionTrigger.IdleReturn => "お帰りなさい",
            InteractionTrigger.ContinuousUse => "已经专注很久啦，眼睛也需要休息。",
            InteractionTrigger.HighFocusApp => $"开始处理 {snapshot.ProcessName} 了吗？我在旁边陪着。",
            InteractionTrigger.FocusSessionStarted => "进入专注状态。我会把提醒放轻一点。",
            InteractionTrigger.FocusSessionEnded => "这轮专注结束了。可以站起来活动一下，再决定要不要继续。",
            InteractionTrigger.BreakStarted => "进入休息状态。先把节奏放慢一点。",
            InteractionTrigger.BreakEnded => "休息结束。可以慢慢回到手头的事情上。",
            InteractionTrigger.BreakDue => "先休息一下吧，等一下再回来也不迟。",
            InteractionTrigger.DailySummaryReady => "今天的记录还在积累中，稍后我会给你整理一份更完整的摘要。",
            InteractionTrigger.ContextSwitchingHigh => "刚才应用切换有点密集。可以先把手头这件事收束一下。",
            InteractionTrigger.DistractionDetected => "专注途中切到了容易分心的应用。我先轻轻提醒一下。",
            InteractionTrigger.ManualRefresh => "LLM暂时不可用，修改配置文件以启用服务哦。",
            _ => "我在这里，陪你一起完成今天的事。"
        };
    }

    /// <summary>
    /// 在 LLM 不可用时根据聊天软件消息信号生成本地备用提醒。
    /// </summary>
    private static string GetMessageFallbackLine(MessageNotification notification)
    {
        return notification.SourceKind switch
        {
            NotificationSourceKind.Qq => "QQ 有新消息。我先轻轻提醒一下，等你方便的时候再看就好。",
            NotificationSourceKind.WeChat => "微信有新消息。我在旁边提醒你一下，别让重要消息等太久。",
            _ => $"{notification.SourceDisplayName} 有新消息。我先帮你记着。"
        };
    }
}
