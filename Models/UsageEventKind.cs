namespace MutsumiPet.Models;

/// <summary>
/// 描述 Win API 监控到的用户使用事件类型。
/// </summary>
public enum UsageEventKind
{
    Startup,
    Routine,
    AppSwitch,
    AppDwell,
    IdleStarted,
    IdleReturned,
    ContinuousUse,
    SessionLocked,
    SessionUnlocked
}
