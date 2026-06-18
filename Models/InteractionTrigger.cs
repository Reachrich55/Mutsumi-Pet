namespace MutsumiPet.Models;

/// <summary>
/// 描述触发桌宠主动互动的原因。
/// </summary>
public enum InteractionTrigger
{
    ManualRefresh,
    Startup,
    HighFocusApp,
    IdleReturn,
    ContinuousUse,
    SessionUnlock,
    QqMessageReceived,
    WechatMessageReceived
}
