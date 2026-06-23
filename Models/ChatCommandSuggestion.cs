namespace MutsumiPet.Models;

/// <summary>
/// 表示对话输入框中可补全的一条命令。
/// </summary>
public sealed class ChatCommandSuggestion
{
    /// <summary>
    /// 获取命令文本。
    /// </summary>
    public string CommandText { get; init; } = string.Empty;

    /// <summary>
    /// 获取命令类型。
    /// </summary>
    public ChatCommandKind Kind { get; init; } = ChatCommandKind.Unknown;

    /// <summary>
    /// 获取命令说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
