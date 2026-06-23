namespace MutsumiPet.Models;

/// <summary>
/// 表示用户在对话窗口输入的一条斜杠命令。
/// </summary>
public sealed class ChatCommand
{
    /// <summary>
    /// 获取命令类型。
    /// </summary>
    public ChatCommandKind Kind { get; init; } = ChatCommandKind.None;

    /// <summary>
    /// 获取原始输入文本。
    /// </summary>
    public string RawText { get; init; } = string.Empty;
}
