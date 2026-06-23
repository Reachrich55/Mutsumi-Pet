using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class UsageSummaryService
{
    private readonly UsageSessionStore _store;

    /// <summary>
    /// 初始化使用摘要服务。
    /// </summary>
    public UsageSummaryService(UsageSessionStore store)
    {
        _store = store;
    }

    /// <summary>
    /// 获取今天零点到当前时间的使用摘要。
    /// </summary>
    public UsageSummary GetTodaySummary(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Date, now.Offset);
        return _store.GetSummary("今日摘要", start, now);
    }

    /// <summary>
    /// 获取一段专注会话的使用摘要。
    /// </summary>
    public UsageSummary GetFocusSummary(DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        return _store.GetSummary("专注会话摘要", startedAt, endedAt);
    }

    /// <summary>
    /// 在 LLM 不可用时生成本地摘要文案。
    /// </summary>
    public string BuildFallbackSummary(UsageSummary summary)
    {
        if (!summary.HasData)
        {
            return $"{summary.Title}还没有足够记录。我会先从现在开始帮你留意活跃时间和休息节奏。";
        }

        var topApp = summary.TopApps.FirstOrDefault();
        var topText = topApp is null
            ? "暂时没有明显的主要应用"
            : $"主要在 {topApp.ProcessName} 上投入了 {FormatDuration(topApp.ActiveTime)}";

        return $"{summary.Title}：活跃 {FormatDuration(summary.ActiveTime)}，空闲 {FormatDuration(summary.IdleTime)}，记录到 {summary.SwitchCount} 次应用切换。{topText}。";
    }

    /// <summary>
    /// 将时长格式化为摘要中使用的中文短文本。
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return $"{Math.Max(0, (int)duration.TotalSeconds)}秒";
        }

        if (duration.TotalHours < 1)
        {
            return $"{Math.Round(duration.TotalMinutes)}分钟";
        }

        return $"{(int)duration.TotalHours}小时{duration.Minutes}分钟";
    }
}
