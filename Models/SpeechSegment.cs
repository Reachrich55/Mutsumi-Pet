namespace MutsumiPet.Models;

/// <summary>
/// 表示对话气泡自动翻页时显示的一段文本。
/// </summary>
public sealed class SpeechSegment
{
    /// <summary>
    /// 初始化对话分段文本和展示时长。
    /// </summary>
    public SpeechSegment(string text, TimeSpan displayDuration)
    {
        Text = text;
        DisplayDuration = displayDuration;
    }

    /// <summary>
    /// 获取分段文本。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取该段文本建议展示的时长。
    /// </summary>
    public TimeSpan DisplayDuration { get; }
}
