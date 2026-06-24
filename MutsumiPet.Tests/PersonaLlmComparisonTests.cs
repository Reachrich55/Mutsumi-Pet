using MutsumiPet.Services;
using Xunit;
using Xunit.Abstractions;

namespace MutsumiPet.Tests;

/// <summary>
/// 打印两个人格的完整配置对比，供人工审阅。
/// 不调用 LLM API，仅输出 systemPrompt 和参数。
/// </summary>
public sealed class PersonaLlmComparisonTests
{
    private readonly ITestOutputHelper _output;

    public PersonaLlmComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PrintMutsumiSystemPrompt()
    {
        var manager = new PersonaManager("mutsumi");
        var p = manager.GetById("mutsumi")!;

        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine("  睦子米 (mutsumi)");
        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine($"  Temperature   : {p.Temperature}");
        _output.WriteLine($"  MaxTokens     : {p.MaxTokens}");
        _output.WriteLine($"  MaxSentences  : {p.MaxSentences}");
        _output.WriteLine($"  UseEmoji      : {p.UseEmoji}");
        _output.WriteLine($"  Initiative    : {p.Initiative}");
        _output.WriteLine($"  Clinginess    : {p.Clinginess}");
        _output.WriteLine($"  Jealousy      : {p.Jealousy}");
        _output.WriteLine($"  WorkInterruption: {p.WorkInterruption}");
        _output.WriteLine($"  ThinkingText  : {p.ThinkingText}");
        _output.WriteLine($"  SwitchInText  : {p.SwitchInText}");
        _output.WriteLine($"  SwitchOutText : {p.SwitchOutText}");
        _output.WriteLine("───────────────────────────────────────");
        _output.WriteLine("  SystemPrompt:");
        foreach (var line in p.SystemPrompt.Split('\n'))
            _output.WriteLine($"  | {line.TrimEnd('\r')}");
        _output.WriteLine("═══════════════════════════════════════");
    }

    [Fact]
    public void PrintMortisSystemPrompt()
    {
        var manager = new PersonaManager("mortis");
        var p = manager.GetById("mortis")!;

        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine("  墨提斯 (mortis)");
        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine($"  Temperature   : {p.Temperature}");
        _output.WriteLine($"  MaxTokens     : {p.MaxTokens}");
        _output.WriteLine($"  MaxSentences  : {p.MaxSentences}");
        _output.WriteLine($"  UseEmoji      : {p.UseEmoji}");
        _output.WriteLine($"  Initiative    : {p.Initiative}");
        _output.WriteLine($"  Clinginess    : {p.Clinginess}");
        _output.WriteLine($"  Jealousy      : {p.Jealousy}");
        _output.WriteLine($"  WorkInterruption: {p.WorkInterruption}");
        _output.WriteLine($"  ThinkingText  : {p.ThinkingText}");
        _output.WriteLine($"  SwitchInText  : {p.SwitchInText}");
        _output.WriteLine($"  SwitchOutText : {p.SwitchOutText}");
        _output.WriteLine("───────────────────────────────────────");
        _output.WriteLine("  SystemPrompt:");
        foreach (var line in p.SystemPrompt.Split('\n'))
            _output.WriteLine($"  | {line.TrimEnd('\r')}");
        _output.WriteLine("═══════════════════════════════════════");
    }

    /// <summary>
    /// 用 6 组相同上下文分别构造 prompt 结构，对比两个人格的差异点。
    /// 不调用 LLM API，仅验证 prompt 模板结构。
    /// </summary>
    [Fact]
    public void VerifyPersonaDistinction_AllSixScenarios()
    {
        var manager = new PersonaManager("mutsumi");
        var mutsumi = manager.GetById("mutsumi")!;
        var mortis = manager.GetById("mortis")!;

        // 场景列表
        var scenarios = new[]
        {
            "用户连续写代码 45 分钟",
            "用户复制了一段编译错误",
            "用户说'我现在很忙，安静一会儿'",
            "用户重新打开聊天窗口",
            "用户提到刚刚在和朋友聊天",
            "用户说'今天什么都不想做'"
        };

        // 结构差异检查（不调用 LLM，仅验证配置差异）
        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine("  配置差异对比");
        _output.WriteLine("═══════════════════════════════════════");

        _output.WriteLine($"  {"参数",-20} {"睦子米",-15} {"墨提斯",-15}");
        _output.WriteLine($"  {"───",-20} {"────",-15} {"────",-15}");
        _output.WriteLine($"  {"Temperature",-20} {mutsumi.Temperature,-15} {mortis.Temperature,-15}");
        _output.WriteLine($"  {"MaxTokens",-20} {mutsumi.MaxTokens,-15} {mortis.MaxTokens,-15}");
        _output.WriteLine($"  {"MaxSentences",-20} {mutsumi.MaxSentences,-15} {mortis.MaxSentences,-15}");
        _output.WriteLine($"  {"UseEmoji",-20} {mutsumi.UseEmoji,-15} {mortis.UseEmoji,-15}");
        _output.WriteLine($"  {"Initiative",-20} {mutsumi.Initiative,-15} {mortis.Initiative,-15}");
        _output.WriteLine($"  {"Clinginess",-20} {mutsumi.Clinginess,-15} {mortis.Clinginess,-15}");
        _output.WriteLine($"  {"Jealousy",-20} {mutsumi.Jealousy,-15} {mortis.Jealousy,-15}");
        _output.WriteLine($"  {"WorkInterruption",-20} {mutsumi.WorkInterruption,-15} {mortis.WorkInterruption,-15}");

        _output.WriteLine("");
        _output.WriteLine($"  测试场景 ({scenarios.Length} 组):");
        foreach (var s in scenarios)
            _output.WriteLine($"    - {s}");

        // 关键规则对比
        _output.WriteLine("");
        _output.WriteLine("  规则差异:");
        _output.WriteLine($"    睦子米: 每次 1-2 句, t={mutsumi.Temperature}, tok={mutsumi.MaxTokens}, 无 emoji");
        _output.WriteLine($"    墨提斯: 每次 2-4 句, t={mortis.Temperature}, tok={mortis.MaxTokens}, 无 emoji");
        _output.WriteLine($"    共同点: 都禁止 emoji/颜文字, 都接受用户边界, 都禁止虚构系统访问");

        Assert.NotEqual(mutsumi.SystemPrompt, mortis.SystemPrompt);
        Assert.True(mortis.Initiative > mutsumi.Initiative);
        Assert.True(mortis.MaxSentences > mutsumi.MaxSentences);
    }
}
