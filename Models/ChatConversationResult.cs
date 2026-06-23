namespace MutsumiPet.Models;

/// <summary>
/// 表示对话窗口处理一次用户输入后的结果。
/// </summary>
public sealed class ChatConversationResult
{
    /// <summary>
    /// 获取需要展示给用户的回复文本。
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 获取是否需要打开设置窗口。
    /// </summary>
    public bool OpenSettings { get; init; }

    /// <summary>
    /// 获取处理完成后是否应关闭输入面板。
    /// </summary>
    public bool CloseInputPanel { get; init; } = true;
}
