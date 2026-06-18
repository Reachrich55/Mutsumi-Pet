namespace MutsumiPet.Models;

/// <summary>
/// 表示从聊天软件窗口状态中识别到的一次消息信号。
/// </summary>
public sealed class MessageNotification
{
    /// <summary>
    /// 获取本地生成的消息信号 ID。
    /// </summary>
    public uint NotificationId { get; init; }

    /// <summary>
    /// 获取识别后的聊天软件来源类型。
    /// </summary>
    public NotificationSourceKind SourceKind { get; init; } = NotificationSourceKind.Unknown;

    /// <summary>
    /// 获取聊天软件显示名。
    /// </summary>
    public string AppName { get; init; } = "Unknown";

    /// <summary>
    /// 获取通用消息标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 获取消息摘要正文；非侵入式监听默认不填充。
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// 获取消息信号识别时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 获取用于去重的稳定键。
    /// </summary>
    public string DeduplicationKey => $"{SourceKind}:{NotificationId}:{CreatedAt.UtcTicks}:{Title}:{Body}";

    /// <summary>
    /// 获取适合 UI 展示的来源名称。
    /// </summary>
    public string SourceDisplayName => SourceKind switch
    {
        NotificationSourceKind.Qq => "QQ",
        NotificationSourceKind.WeChat => "微信",
        _ => AppName
    };
}
