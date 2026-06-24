namespace MutsumiPet.Models;

/// <summary>
/// 单个人格的实时情绪状态。所有值域为 0.0–1.0。
/// </summary>
public sealed class EmotionState
{
    /// <summary>
    /// 寂寞感：被忽略或长时间未互动时上升。
    /// </summary>
    public double Loneliness { get; set; }

    /// <summary>
    /// 关注度：对用户当前活动的关注程度。
    /// </summary>
    public double Attention { get; set; }

    /// <summary>
    /// 信任度：对用户关系的稳定感知。
    /// </summary>
    public double Trust { get; set; } = 0.5;

    /// <summary>
    /// 嫉妒感：用户活跃但未与自己互动时上升。
    /// </summary>
    public double Jealousy { get; set; }

    /// <summary>
    /// 将所有情绪值限制在 0.0–1.0 范围内。
    /// </summary>
    public void Clamp()
    {
        Loneliness = Math.Clamp(Loneliness, 0.0, 1.0);
        Attention = Math.Clamp(Attention, 0.0, 1.0);
        Trust = Math.Clamp(Trust, 0.0, 1.0);
        Jealousy = Math.Clamp(Jealousy, 0.0, 1.0);
    }

    /// <summary>
    /// 创建一份独立副本。
    /// </summary>
    public EmotionState Clone()
    {
        return new EmotionState
        {
            Loneliness = Loneliness,
            Attention = Attention,
            Trust = Trust,
            Jealousy = Jealousy
        };
    }
}
