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
    private readonly PersonaManager _personaManager;
    private readonly Dictionary<InteractionTrigger, DateTimeOffset> _lastTriggerTimes = new();
    private DateTimeOffset _lastAnyTriggerAt = DateTimeOffset.MinValue;
    private readonly Dictionary<string, string?> _lastLlmLines = new();
    private CancellationTokenSource _proactiveCts = new();
    private CancellationTokenSource _chatCts = new();
    private readonly object _ctsLock = new();

    /// <summary>
    /// 初始化桌宠互动编排服务。
    /// </summary>
    public PetInteractionService(
        WindowsUsageMonitor usageMonitor,
        LlmClient llmClient,
        SettingsService settingsService,
        UsageSessionTracker usageSessionTracker,
        FocusSessionService focusSessionService,
        UsageSummaryService usageSummaryService,
        PersonaManager personaManager)
    {
        _usageMonitor = usageMonitor;
        _llmClient = llmClient;
        _settingsService = settingsService;
        _usageSessionTracker = usageSessionTracker;
        _focusSessionService = focusSessionService;
        _usageSummaryService = usageSummaryService;
        _personaManager = personaManager;
    }

    /// <summary>
    /// 获取当前专注或休息状态。
    /// </summary>
    public FocusSessionState CurrentFocusState => _focusSessionService.State;

    // ────────────── 情绪更新 ──────────────

    /// <summary>
    /// tick 更新所有已加载人格的情绪状态。
    /// 应由外部定时器每 30–60 秒调用一次。
    /// </summary>
    /// <param name="isUserActive">用户当前是否在操作电脑（非空闲）</param>
    public void UpdateAllEmotions(bool isUserActive)
    {
        foreach (var state in _personaManager.AllRuntimeStates)
        {
            var persona = _personaManager.GetById(state.PersonaId);
            if (persona is null) continue;

            UpdateEmotion(persona, state, isUserActive);
            state.Emotion.Clamp();
        }
    }

    /// <summary>
    /// 按人格规则更新单个情绪状态。
    /// </summary>
    private static void UpdateEmotion(PersonaProfile persona, PersonaRuntimeState state, bool isUserActive)
    {
        var idleSeconds = state.SecondsSinceActive;
        var speakSeconds = state.SecondsSinceSpeak;

        if (persona.Id == "mutsumi")
        {
            UpdateMutsumiEmotion(state, isUserActive, idleSeconds, speakSeconds);
        }
        else
        {
            UpdateMortisEmotion(state, isUserActive, idleSeconds, speakSeconds);
        }
    }

    /// <summary>
    /// 睦子米情绪规则：寂寞上升缓慢，关注衰减慢，不主动打扰。
    /// </summary>
    private static void UpdateMutsumiEmotion(PersonaRuntimeState state, bool isUserActive,
        double idleSeconds, double speakSeconds)
    {
        // Loneliness: 非常缓慢上升，用户活跃时几乎不涨
        if (isUserActive)
        {
            state.Emotion.Loneliness += 0.003;
        }
        else
        {
            state.Emotion.Loneliness += 0.008;
        }

        // Attention: 对用户活跃度温和响应，衰减慢
        if (isUserActive)
        {
            state.Emotion.Attention += 0.01;
        }
        else
        {
            state.Emotion.Attention -= 0.003;
        }

        // Trust: 极缓慢积累，用户互动时略增
        state.Emotion.Trust += 0.001;
        if (idleSeconds < 120)
        {
            state.Emotion.Trust += 0.002;
        }

        // Jealousy: 非常低，仅在用户活跃但不互动时微升
        if (isUserActive && speakSeconds > 600)
        {
            state.Emotion.Jealousy += 0.003;
        }
        else
        {
            state.Emotion.Jealousy -= 0.005;
        }
    }

    /// <summary>
    /// 墨提斯情绪规则：关注度对用户活跃度敏感，寂寞上升更快，嫉妒随非互动时间上升。
    /// </summary>
    private static void UpdateMortisEmotion(PersonaRuntimeState state, bool isUserActive,
        double idleSeconds, double speakSeconds)
    {
        // Loneliness: 上升较快，尤其用户不活跃时
        if (isUserActive)
        {
            state.Emotion.Loneliness += 0.012;
        }
        else
        {
            state.Emotion.Loneliness += 0.025;
        }

        // Attention: 对用户活跃度高度敏感
        if (isUserActive)
        {
            state.Emotion.Attention += 0.025;
        }
        else
        {
            state.Emotion.Attention -= 0.015;
        }

        // Trust: 稳定，互动时略增
        if (idleSeconds < 120)
        {
            state.Emotion.Trust += 0.003;
        }

        // Jealousy: 用户活跃但不跟自己说话时上升
        if (isUserActive && speakSeconds > 300)
        {
            state.Emotion.Jealousy += 0.02;
        }
        else if (!isUserActive)
        {
            state.Emotion.Jealousy -= 0.008;
        }
        else
        {
            state.Emotion.Jealousy -= 0.015;
        }
    }

    // ────────────── 行为策略推导 ──────────────

    /// <summary>
    /// 根据当前人格和情绪状态推导行为策略。
    /// 每次 LLM 请求前调用，确保策略反映最新情绪。
    /// </summary>
    public BehaviorPolicy DeriveBehaviorPolicy(string personaId)
    {
        var persona = _personaManager.GetById(personaId);
        var state = _personaManager.GetRuntimeState(personaId);
        var emotion = state.Emotion;

        if (persona is null)
        {
            return new BehaviorPolicy();
        }

        if (persona.Id == "mutsumi")
        {
            return new BehaviorPolicy
            {
                CanInterrupt = emotion.Loneliness > 0.7 || emotion.Attention < 0.15,
                MinSentence = 1,
                MaxSentence = emotion.Loneliness > 0.5 ? 2 : 1,
                SpeakProbability = 0.25 + emotion.Loneliness * 0.3,
                UseShortSentences = true,
                EmotionContext = BuildMutsumiEmotionContext(emotion)
            };
        }
        else // mortis
        {
            return new BehaviorPolicy
            {
                CanInterrupt = emotion.Loneliness > 0.35 || emotion.Jealousy > 0.45,
                MinSentence = 2,
                MaxSentence = emotion.Loneliness > 0.6 ? 4 : 3,
                SpeakProbability = 0.45 + emotion.Loneliness * 0.4,
                UseShortSentences = emotion.Attention < 0.3,
                EmotionContext = BuildMortisEmotionContext(emotion)
            };
        }
    }

    private static string BuildMutsumiEmotionContext(EmotionState e)
    {
        if (e.Loneliness > 0.7)
            return "[你感到有些寂寞，但仍在安静等待。可以稍微流露一点在意，但不要抱怨。]";
        if (e.Attention < 0.2)
            return "[你注意到用户似乎非常忙，保持安静，不要打扰。]";
        if (e.Trust > 0.7)
            return "[你感到与用户之间有一种安静的信任，心里很安稳。]";
        if (e.Loneliness > 0.4)
            return "[你有一点点想和用户说话，但不会主动要求。]";
        return "[你正安静地陪伴着用户，心情平稳。]";
    }

    private static string BuildMortisEmotionContext(EmotionState e)
    {
        if (e.Loneliness > 0.7)
            return "[你已经等了很久，很想和用户说话。可以表达想念，但不要指责或施压。]";
        if (e.Jealousy > 0.5)
            return "[你有点在意用户是否忽略了你的存在。可以轻微撒娇，但不要攻击任何人。]";
        if (e.Attention > 0.8)
            return "[你正全神贯注地关注着用户的一举一动，心情很好。可以活泼主动一点。]";
        if (e.Loneliness > 0.4)
            return "[你有一阵子没和用户互动了，有点想引起注意。]";
        return "[你心情不错，可以活泼一点，但不要过度。]";
    }

    /// <summary>
    /// 取消所有正在进行的 LLM 请求（人格切换时调用）。
    /// 主动互动和聊天使用独立的 CancellationTokenSource，互不干扰。
    /// </summary>
    public void CancelInFlightRequests()
    {
        lock (_ctsLock)
        {
            CancelAndDispose(ref _proactiveCts);
            _proactiveCts = new CancellationTokenSource();
            CancelAndDispose(ref _chatCts);
            _chatCts = new CancellationTokenSource();
        }
    }

    /// <summary>
    /// 创建与主动互动路径绑定的链接取消令牌。
    /// </summary>
    private CancellationToken CreateProactiveToken(CancellationToken shutdownToken)
    {
        CancellationTokenSource proactiveCts;
        lock (_ctsLock)
        {
            proactiveCts = _proactiveCts;
        }
        return CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, proactiveCts.Token).Token;
    }

    /// <summary>
    /// 创建与聊天路径绑定的链接取消令牌。
    /// </summary>
    private CancellationToken CreateChatToken(CancellationToken shutdownToken)
    {
        CancellationTokenSource chatCts;
        lock (_ctsLock)
        {
            chatCts = _chatCts;
        }
        return CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, chatCts.Token).Token;
    }

    private static void CancelAndDispose(ref CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        try
        {
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

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
        string personaId,
        CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var snapshot = _usageMonitor.CaptureSnapshot(settings);
        _usageSessionTracker.ProcessSnapshot(
            snapshot,
            settings,
            _focusSessionService.State == FocusSessionState.Focusing);

        var persona = _personaManager.Current;
        var capturedGeneration = _personaManager.Generation;
        var linkedToken = CreateChatToken(cancellationToken);

        // 记录用户互动
        _personaManager.RecordUserInteraction();

        // 推导当前行为策略
        var policy = DeriveBehaviorPolicy(persona.Id);

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
                    GetLastLlmLine(persona.Id),
                    persona,
                    policy.EmotionContext,
                    settings,
                    linkedToken);
            }
            catch (HttpRequestException)
            {
                line = null;
            }
            catch (TaskCanceledException) when (!linkedToken.IsCancellationRequested)
            {
                line = null;
            }
        }

        // 并发安全：人格已切换或世代号已变更则丢弃结果
        if (_personaManager.Current.Id != personaId || _personaManager.Generation != capturedGeneration)
        {
            line = null;
        }

        var normalized = NormalizeLine(line);
        if (normalized is not null)
        {
            SetLastLlmLine(personaId, normalized);
            _personaManager.RecordSpeak();
            return normalized;
        }

        // fallback 也算一次说话
        _personaManager.RecordSpeak();
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
        var persona = _personaManager.Current;
        var capturedId = persona.Id;
        var capturedGeneration = _personaManager.Generation;
        var linkedToken = CreateProactiveToken(cancellationToken);

        // 推导当前行为策略
        var policy = DeriveBehaviorPolicy(persona.Id);

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
                    GetLastLlmLine(persona.Id),
                    persona,
                    policy.EmotionContext,
                    settings,
                    linkedToken);
            }
            catch (HttpRequestException)
            {
                line = null;
            }
            catch (TaskCanceledException) when (!linkedToken.IsCancellationRequested)
            {
                line = null;
            }
        }

        // 并发安全：人格已切换或世代号已变更则丢弃结果
        if (_personaManager.Current.Id != capturedId || _personaManager.Generation != capturedGeneration)
        {
            line = null;
        }

        MarkTriggered(trigger, snapshot.CapturedAt);
        var normalizedLlmLine = NormalizeLine(line);
        if (normalizedLlmLine is not null)
        {
            SetLastLlmLine(capturedId, normalizedLlmLine);
            _personaManager.RecordSpeak();
            return normalizedLlmLine;
        }

        _personaManager.RecordSpeak();
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

    /// <summary>
    /// 获取指定人格的上一条 LLM 回复，用于请求中的 previousAssistantLine。
    /// </summary>
    private string? GetLastLlmLine(string personaId)
    {
        return _lastLlmLines.GetValueOrDefault(personaId);
    }

    /// <summary>
    /// 保存指定人格的上一条 LLM 回复。
    /// </summary>
    private void SetLastLlmLine(string personaId, string? line)
    {
        _lastLlmLines[personaId] = line;
    }
}
