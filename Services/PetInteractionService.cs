using System.Net.Http;
using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class PetInteractionService
{
    private const int TextLengthLimit = 280;
    private static readonly TimeSpan GlobalInteractionCooldown = TimeSpan.FromMinutes(3);
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly LlmClient _llmClient;
    private readonly Dictionary<InteractionTrigger, DateTimeOffset> _lastTriggerTimes = new();
    private DateTimeOffset _lastAnyTriggerAt = DateTimeOffset.MinValue;
    private string? _lastLlmLine;

    /// <summary>
    /// 初始化桌宠互动编排服务。
    /// </summary>
    public PetInteractionService(WindowsUsageMonitor usageMonitor, LlmClient llmClient)
    {
        _usageMonitor = usageMonitor;
        _llmClient = llmClient;
    }

    /// <summary>
    /// 根据当前用户状态决定是否生成下一句互动文本。
    /// </summary>
    public async Task<string?> GetNextLineAsync(bool force, CancellationToken cancellationToken)
    {
        var snapshot = _usageMonitor.CaptureSnapshot();
        var trigger = SelectTrigger(snapshot, force);
        if (trigger is null)
        {
            return null;
        }

        string? line = null;
        try
        {
            line = await _llmClient.GenerateLineAsync(snapshot, trigger.Value, _lastLlmLine, cancellationToken);
        }
        catch (HttpRequestException)
        {
            line = null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            line = null;
        }

        MarkTriggered(trigger.Value, snapshot.CapturedAt);
        var normalizedLlmLine = NormalizeLine(line);
        if (normalizedLlmLine is not null)
        {
            _lastLlmLine = normalizedLlmLine;
            return normalizedLlmLine;
        }

        return NormalizeLine(GetFallbackLine(snapshot, trigger.Value));
    }

    /// <summary>
    /// 根据聊天软件新消息信号生成优先级更高的互动文本。
    /// </summary>
    public async Task<string?> GetMessageLineAsync(
        MessageNotification notification,
        CancellationToken cancellationToken)
    {
        var snapshot = _usageMonitor.CaptureSnapshot();
        var trigger = SelectMessageTrigger(notification);
        if (trigger is null || !CanTriggerMessage(trigger.Value, snapshot.CapturedAt))
        {
            return null;
        }

        string? line = null;
        try
        {
            line = await _llmClient.GenerateLineAsync(
                snapshot,
                trigger.Value,
                notification,
                _lastLlmLine,
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

        MarkTriggered(trigger.Value, snapshot.CapturedAt);
        var normalizedLlmLine = NormalizeLine(line);
        if (normalizedLlmLine is not null)
        {
            _lastLlmLine = normalizedLlmLine;
            return normalizedLlmLine;
        }

        return NormalizeLine(GetMessageFallbackLine(notification));
    }

    /// <summary>
    /// 按事件类型和冷却时间选择本次互动触发器。
    /// </summary>
    private InteractionTrigger? SelectTrigger(UsageSnapshot snapshot, bool force)
    {
        if (force)
        {
            return InteractionTrigger.ManualRefresh;
        }

        var trigger = snapshot.EventKind switch
        {
            UsageEventKind.Startup => InteractionTrigger.Startup,
            UsageEventKind.SessionUnlocked => InteractionTrigger.SessionUnlock,
            UsageEventKind.IdleReturned => InteractionTrigger.IdleReturn,
            UsageEventKind.ContinuousUse => InteractionTrigger.ContinuousUse,
            UsageEventKind.AppDwell when IsHighFocusProcess(snapshot.ProcessName) => InteractionTrigger.HighFocusApp,
            _ => (InteractionTrigger?)null
        };

        return trigger.HasValue && CanTrigger(trigger.Value, snapshot.CapturedAt) ? trigger : null;
    }

    /// <summary>
    /// 判断指定触发器是否已经过了全局与分类冷却时间。
    /// </summary>
    private bool CanTrigger(InteractionTrigger trigger, DateTimeOffset now)
    {
        if (trigger == InteractionTrigger.ManualRefresh)
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
    /// 获取不同互动触发器对应的冷却时间。
    /// </summary>
    private static TimeSpan GetCooldown(InteractionTrigger trigger)
    {
        return trigger switch
        {
            InteractionTrigger.ManualRefresh => TimeSpan.Zero,
            InteractionTrigger.QqMessageReceived => TimeSpan.FromSeconds(20),
            InteractionTrigger.WechatMessageReceived => TimeSpan.FromSeconds(20),
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
    /// 判断前台进程是否属于适合提醒的高专注应用。
    /// </summary>
    private static bool IsHighFocusProcess(string processName)
    {
        var highFocusProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Code",
            "devenv",
            "rider64",
            "idea64",
            "pycharm64",
            "clion64",
            "studio64",
            "chrome",
            "msedge",
            "firefox",
            "winword",
            "excel",
            "powerpnt",
            "notion",
            "obsidian"
        };

        return highFocusProcesses.Contains(processName);
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
            : normalized.Substring(0, TextLengthLimit - 1) + "…";
    }

    /// <summary>
    /// 在 LLM 不可用时根据触发器生成本地备用台词。
    /// </summary>
    private static string GetFallbackLine(UsageSnapshot snapshot, InteractionTrigger trigger)
    {
        return trigger switch
        {
            InteractionTrigger.Startup => "我醒啦，今天也慢慢来。",
            InteractionTrigger.SessionUnlock => "欢迎回来，刚才休息得还好吗？",
            InteractionTrigger.IdleReturn => "你回来了，要不要先伸个懒腰？",
            InteractionTrigger.ContinuousUse => "已经专注很久啦，眼睛也需要休息。",
            InteractionTrigger.HighFocusApp => $"开始处理 {snapshot.ProcessName} 了吗？我在旁边陪着。",
            InteractionTrigger.ManualRefresh => "我看了一下，现在状态还不错。",
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
