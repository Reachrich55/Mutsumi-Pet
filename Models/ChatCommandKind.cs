namespace MutsumiPet.Models;

/// <summary>
/// 描述对话窗口支持的斜杠命令类型。
/// </summary>
public enum ChatCommandKind
{
    None,
    Focus,
    EndFocus,
    Break,
    EndBreak,
    Summary,
    Settings,
    Help,
    Unknown
}
