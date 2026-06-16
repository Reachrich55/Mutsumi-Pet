using System.Net.Http;
using MutsuPet.Models;

namespace MutsuPet.Services;

public sealed class PetInteractionService
{
    private const int LineLengthLimit = 54;
    private readonly WindowsUsageMonitor _usageMonitor;
    private readonly LlmClient _llmClient;
    private readonly Dictionary<InteractionTrigger, DateTimeOffset> _lastTriggerTimes = new();
    private DateTimeOffset _lastAnyTriggerAt = DateTimeOffset.MinValue;

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
            line = await _llmClient.GenerateLineAsync(snapshot, trigger.Value, cancellationToken);
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
        return NormalizeLine(line) ?? NormalizeLine(GetFallbackLine(snapshot, trigger.Value));
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
            UsageEventKind.AppSwitch when IsHighFocusProcess(snapshot.ProcessName) => InteractionTrigger.HighFocusApp,
            _ => (InteractionTrigger?)null
        };

        return trigger.HasValue && CanTrigger(trigger.Value, snapshot.CapturedAt) ? trigger : null;
    }

    /// <summary>
    /// 判断指定触发器是否已经过了全局与分类冷却时间。
    /// </summary>
    private bool CanTrigger(InteractionTrigger trigger, DateTimeOffset now)
    {
        var globalCooldown = TimeSpan.FromSeconds(75);
        if (now - _lastAnyTriggerAt < globalCooldown)
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
            InteractionTrigger.Startup => TimeSpan.FromMinutes(10),
            InteractionTrigger.SessionUnlock => TimeSpan.FromMinutes(2),
            InteractionTrigger.IdleReturn => TimeSpan.FromMinutes(8),
            InteractionTrigger.HighFocusApp => TimeSpan.FromMinutes(12),
            InteractionTrigger.ContinuousUse => TimeSpan.FromMinutes(35),
            _ => TimeSpan.FromMinutes(10)
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

        return normalized.Length <= LineLengthLimit
            ? normalized
            : normalized.Substring(0, LineLengthLimit - 1) + "…";
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
}
