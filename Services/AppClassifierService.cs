using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class AppClassifierService
{
    private static readonly HashSet<string> FocusProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code",
        "devenv",
        "rider64",
        "idea64",
        "pycharm64",
        "clion64",
        "studio64",
        "winword",
        "excel",
        "powerpnt",
        "notion",
        "obsidian",
        "onenote",
        "ida64"
    };

    private static readonly HashSet<string> CommunicationProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "QQ",
        "QQNT",
        "TIM",
        "WeChat",
        "Weixin",
        "WeChatAppEx",
        "OUTLOOK",
        "Teams",
        "Discord",
        "Telegram",
        "DingTalk"
    };

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi"
    };

    private static readonly HashSet<string> MediaProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Spotify",
        "cloudmusic",
        "qqmusic",
        "kugou",
        "kwmusic",
        "potplayer",
        "potplayermini64",
        "vlc",
        "foobar2000",
        "Music"
    };

    private static readonly HashSet<string> GameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam",
        "steamwebhelper",
        "EpicGamesLauncher",
        "Battle.net",
        "GenshinImpact",
        "StarRail",
        "LeagueClient",
        "VALORANT"
    };

    private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "Taskmgr",
        "SystemSettings",
        "SearchHost",
        "ShellExperienceHost",
        "ApplicationFrameHost",
        "TextInputHost"
    };

    /// <summary>
    /// 根据进程名识别应用类别。
    /// </summary>
    public AppCategory ClassifyProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return AppCategory.Other;
        }

        if (FocusProcesses.Contains(processName))
        {
            return AppCategory.Focus;
        }

        if (CommunicationProcesses.Contains(processName))
        {
            return AppCategory.Communication;
        }

        if (BrowserProcesses.Contains(processName))
        {
            return AppCategory.Browser;
        }

        if (MediaProcesses.Contains(processName))
        {
            return AppCategory.Media;
        }

        if (GameProcesses.Contains(processName))
        {
            return AppCategory.Game;
        }

        return SystemProcesses.Contains(processName) ? AppCategory.System : AppCategory.Other;
    }

    /// <summary>
    /// 判断应用类别是否容易打断专注会话。
    /// </summary>
    public bool IsDistractingCategory(AppCategory category)
    {
        return category is AppCategory.Communication or AppCategory.Media or AppCategory.Game;
    }
}
