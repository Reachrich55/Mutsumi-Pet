namespace MutsumiPet.Models;

/// <summary>
/// 一份人格定义，包含系统提示词、生成参数和个性行为参数。
/// 通过 JSON 文件加载，所有属性通过 init 保持不可变。
/// </summary>
public sealed class PersonaProfile
{
    /// <summary>
    /// 人格唯一标识，用于切换和持久化。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// UI 显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 记忆上下文中使用的角色前缀（如"小睦""墨提斯"）。
    /// </summary>
    public string AssistantPrefix { get; init; } = string.Empty;

    /// <summary>
    /// 发送给 LLM 的完整系统提示词。
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// LLM 思考中气泡占位文本。
    /// </summary>
    public string ThinkingText { get; init; } = "正在思考...";

    /// <summary>
    /// 切换到当前人格时显示的台词。
    /// </summary>
    public string SwitchInText { get; init; } = string.Empty;

    /// <summary>
    /// 从当前人格切走时显示的台词。
    /// </summary>
    public string SwitchOutText { get; init; } = string.Empty;

    // ────────────── 生成参数 ──────────────

    /// <summary>
    /// LLM 生成温度（0.0-2.0）。
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// LLM 最大输出 token 数，作为各请求类型上限。
    /// </summary>
    public int MaxTokens { get; init; } = 600;

    // ────────────── 行为参数（本阶段仅保存，不全量接入逻辑）──────────────

    /// <summary>
    /// 主动发起互动的倾向（0.0-1.0）。
    /// </summary>
    public double Initiative { get; init; } = 0.5;

    /// <summary>
    /// 黏人程度（0.0-1.0）。
    /// </summary>
    public double Clinginess { get; init; } = 0.3;

    /// <summary>
    /// 嫉妒/占有欲倾向（0.0-1.0）。
    /// </summary>
    public double Jealousy { get; init; } = 0.2;

    /// <summary>
    /// 打断用户工作的倾向（0.0-1.0）。
    /// </summary>
    public double WorkInterruption { get; init; } = 0.3;

    /// <summary>
    /// 单次回复最大句子数（供未来 prompt 模板使用）。
    /// </summary>
    public int MaxSentences { get; init; } = 3;

    /// <summary>
    /// 是否允许在回复中使用 emoji。
    /// </summary>
    public bool UseEmoji { get; init; } = true;
}
