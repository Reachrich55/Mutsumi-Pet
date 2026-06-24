namespace MutsumiPet.Models;

/// <summary>
/// 单个人格的完整运行时状态：人格 ID + 情绪 + 最后活跃时间。
/// 每个人格独立维护，切换人格时不共享。
/// </summary>
public sealed class PersonaRuntimeState
{
    /// <summary>
    /// 所属人格 ID。
    /// </summary>
    public string PersonaId { get; init; } = string.Empty;

    /// <summary>
    /// 当前情绪状态。
    /// </summary>
    public EmotionState Emotion { get; init; } = new();

    /// <summary>
    /// 该人格最后一次被用户直接交互的时间。
    /// </summary>
    public DateTime LastActiveTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 该人格最后一次产生互动台词的时间（含 LLM 和 fallback）。
    /// </summary>
    public DateTime LastSpeakTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 距上次互动已过的秒数。
    /// </summary>
    public double SecondsSinceActive => (DateTime.Now - LastActiveTime).TotalSeconds;

    /// <summary>
    /// 距上次说话已过的秒数。
    /// </summary>
    public double SecondsSinceSpeak => (DateTime.Now - LastSpeakTime).TotalSeconds;
}
