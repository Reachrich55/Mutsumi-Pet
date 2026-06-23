using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class ChatCommandService
{
    private static readonly ChatCommandSuggestion FocusCommand = new()
    {
        CommandText = "/专注",
        Kind = ChatCommandKind.Focus,
        Description = "进入专注状态"
    };

    private static readonly ChatCommandSuggestion EndFocusCommand = new()
    {
        CommandText = "/结束专注",
        Kind = ChatCommandKind.EndFocus,
        Description = "结束专注并生成摘要"
    };

    private static readonly ChatCommandSuggestion BreakCommand = new()
    {
        CommandText = "/休息",
        Kind = ChatCommandKind.Break,
        Description = "进入休息状态"
    };

    private static readonly ChatCommandSuggestion EndBreakCommand = new()
    {
        CommandText = "/结束休息",
        Kind = ChatCommandKind.EndBreak,
        Description = "结束休息状态"
    };

    private static readonly ChatCommandSuggestion SummaryCommand = new()
    {
        CommandText = "/摘要",
        Kind = ChatCommandKind.Summary,
        Description = "生成今日摘要"
    };

    private static readonly ChatCommandSuggestion SettingsCommand = new()
    {
        CommandText = "/设置",
        Kind = ChatCommandKind.Settings,
        Description = "打开隐私与设置"
    };

    private static readonly ChatCommandSuggestion HelpCommand = new()
    {
        CommandText = "/帮助",
        Kind = ChatCommandKind.Help,
        Description = "查看可用命令"
    };

    /// <summary>
    /// 解析用户输入，非斜杠开头时返回普通消息。
    /// </summary>
    public ChatCommand Parse(string input)
    {
        var normalized = input.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return new ChatCommand
            {
                Kind = ChatCommandKind.None,
                RawText = input
            };
        }

        return new ChatCommand
        {
            Kind = normalized switch
            {
                "/专注" => ChatCommandKind.Focus,
                "/结束专注" => ChatCommandKind.EndFocus,
                "/休息" => ChatCommandKind.Break,
                "/结束休息" => ChatCommandKind.EndBreak,
                "/摘要" => ChatCommandKind.Summary,
                "/设置" => ChatCommandKind.Settings,
                "/帮助" => ChatCommandKind.Help,
                _ => ChatCommandKind.Unknown
            },
            RawText = input
        };
    }

    /// <summary>
    /// 根据当前专注或休息状态返回可执行命令补全项。
    /// </summary>
    public IReadOnlyList<ChatCommandSuggestion> GetAvailableCommands(FocusSessionState state)
    {
        return state switch
        {
            FocusSessionState.Focusing => new[]
            {
                EndFocusCommand,
                SummaryCommand,
                SettingsCommand,
                HelpCommand
            },
            FocusSessionState.Break => new[]
            {
                EndBreakCommand,
                SummaryCommand,
                SettingsCommand,
                HelpCommand
            },
            _ => new[]
            {
                FocusCommand,
                BreakCommand,
                SummaryCommand,
                SettingsCommand,
                HelpCommand
            }
        };
    }
}
