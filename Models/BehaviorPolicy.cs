namespace MutsumiPet.Models;

/// <summary>
/// 根据当前情绪状态推导出的行为策略。
/// 控制人格是否主动说话、回复长度和频率。
/// 每次 LLM 请求前根据最新 EmotionState 重新计算。
/// </summary>
public sealed class BehaviorPolicy
{
    /// <summary>
    /// 当前是否允许主动打断用户。
    /// </summary>
    public bool CanInterrupt { get; init; }

    /// <summary>
    /// 本次回复最少句子数。
    /// </summary>
    public int MinSentence { get; init; } = 1;

    /// <summary>
    /// 本次回复最多句子数。
    /// </summary>
    public int MaxSentence { get; init; } = 3;

    /// <summary>
    /// 主动说话的概率（0.0–1.0）。由外部触发选择器参考。
    /// </summary>
    public double SpeakProbability { get; init; } = 0.5;

    /// <summary>
    /// 是否偏好使用短句。
    /// </summary>
    public bool UseShortSentences { get; init; } = true;

    /// <summary>
    /// 注入到 LLM system prompt 中的情绪上下文文本。
    /// </summary>
    public string EmotionContext { get; init; } = string.Empty;
}
