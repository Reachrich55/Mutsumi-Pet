using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class ChatConversationService
{
    private readonly ChatCommandService _commandService;
    private readonly PetInteractionService _interactionService;
    private readonly ConversationMemoryService _memoryService;

    /// <summary>
    /// 初始化对话编排服务。
    /// </summary>
    public ChatConversationService(
        ChatCommandService commandService,
        PetInteractionService interactionService,
        ConversationMemoryService memoryService)
    {
        _commandService = commandService;
        _interactionService = interactionService;
        _memoryService = memoryService;
    }

    /// <summary>
    /// 处理对话窗口中的一次用户输入。
    /// </summary>
    public async Task<ChatConversationResult> SubmitAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ChatConversationResult
            {
                Text = "输入一点内容，或者用 /帮助 查看可用命令。"
            };
        }

        var command = _commandService.Parse(input);
        var result = command.Kind switch
        {
            ChatCommandKind.None => new ChatConversationResult
            {
                Text = await _interactionService.GetChatReplyAsync(
                    input,
                    _memoryService.BuildPromptMemory(),
                    cancellationToken)
            },
            ChatCommandKind.Focus => new ChatConversationResult
            {
                Text = await _interactionService.StartFocusSessionAsync(cancellationToken)
                    ?? "已进入专注状态。"
            },
            ChatCommandKind.EndFocus => new ChatConversationResult
            {
                Text = await _interactionService.EndFocusSessionAsync(cancellationToken)
                    ?? "专注已结束。"
            },
            ChatCommandKind.Break => new ChatConversationResult
            {
                Text = await _interactionService.StartBreakSessionAsync(cancellationToken)
                    ?? "已进入休息状态。"
            },
            ChatCommandKind.EndBreak => new ChatConversationResult
            {
                Text = await _interactionService.EndBreakSessionAsync(cancellationToken)
                    ?? "休息已结束。"
            },
            ChatCommandKind.Summary => new ChatConversationResult
            {
                Text = await _interactionService.GetTodaySummaryAsync(cancellationToken)
                    ?? "今天还没有足够的记录。"
            },
            ChatCommandKind.Settings => new ChatConversationResult
            {
                Text = "已打开隐私与设置。",
                OpenSettings = true
            },
            ChatCommandKind.Help => new ChatConversationResult
            {
                Text = BuildHelpText()
            },
            _ => new ChatConversationResult
            {
                Text = "没有这个命令。输入 /帮助 查看可用命令。"
            }
        };

        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            _memoryService.RecordExchange(input, result.Text);
        }

        return result;
    }

    /// <summary>
    /// 获取当前状态下可补全的命令。
    /// </summary>
    public IReadOnlyList<ChatCommandSuggestion> GetAvailableCommands()
    {
        return _commandService.GetAvailableCommands(_interactionService.CurrentFocusState);
    }

    /// <summary>
    /// 构造命令帮助文本。
    /// </summary>
    private static string BuildHelpText()
    {
        return "可用命令：/专注、/结束专注、/休息、/结束休息、/摘要、/设置。直接输入文字也可以和我聊天。";
    }
}
