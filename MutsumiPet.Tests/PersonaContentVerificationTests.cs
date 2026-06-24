using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

/// <summary>
/// 验证两个人格 JSON 的内容、参数和规则是否按规范正确配置。
/// </summary>
public sealed class PersonaContentVerificationTests
{
    private readonly PersonaManager _manager = new("mutsumi");

    // ────────────── 睦子米 ──────────────

    [Fact]
    public void Mutsumi_HasCorrectIdentity()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);
        Assert.Equal("睦子米", p!.DisplayName);
        Assert.Equal("小睦", p.AssistantPrefix);
    }

    [Fact]
    public void Mutsumi_SystemPrompt_ContainsCoreRules()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;

        // 核心身份
        Assert.Contains("睦子米", prompt);
        Assert.Contains("安静", prompt);

        // 关键规则
        Assert.Contains("一到两句", prompt);
        Assert.Contains("不使用 emoji", prompt);
        Assert.Contains("不替用户擅自判断感情", prompt);
        Assert.Contains("吃醋时表现为", prompt);
        Assert.Contains("不宣称自己真实读取", prompt);
    }

    [Fact]
    public void Mutsumi_HasCorrectParameters()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        Assert.Equal(0.64, p!.Temperature);
        Assert.Equal(360, p.MaxTokens);
        Assert.Equal(2, p.MaxSentences);
        Assert.False(p.UseEmoji);
    }

    [Fact]
    public void Mutsumi_HasCorrectTraitScores()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        Assert.Equal(0.28, p!.Initiative);
        Assert.Equal(0.42, p.Clinginess);
        Assert.Equal(0.22, p.Jealousy);
        Assert.Equal(0.14, p.WorkInterruption);
    }

    [Fact]
    public void Mutsumi_HasCorrectDisplayTexts()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        Assert.Equal("……让我想一下。", p!.ThinkingText);
        Assert.Equal("……我回来了。", p.SwitchInText);
        Assert.Equal("她在等你。", p.SwitchOutText);
    }

    [Fact]
    public void Mutsumi_SystemPrompt_ForbidsEmojiAndMemes()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;
        Assert.Contains("不使用 emoji", prompt);
        Assert.Contains("颜文字", prompt);
        Assert.Contains("网络热梗", prompt);
    }

    [Fact]
    public void Mutsumi_SystemPrompt_RequiresBrevity()
    {
        var p = _manager.GetById("mutsumi");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;
        Assert.Contains("一到两句", prompt);
        Assert.Contains("避免长篇说明", prompt);
        Assert.Contains("少用感叹号", prompt);
    }

    // ────────────── 墨提斯 ──────────────

    [Fact]
    public void Mortis_HasCorrectIdentity()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);
        Assert.Equal("墨提斯", p!.DisplayName);
        Assert.Equal("墨提斯", p.AssistantPrefix);
    }

    [Fact]
    public void Mortis_SystemPrompt_ContainsCoreRules()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;

        // 核心身份
        Assert.Contains("墨提斯", prompt);
        Assert.Contains("更主动", prompt);

        // 关键规则
        Assert.Contains("两到四句", prompt);
        Assert.Contains("舞台感", prompt);
        Assert.Contains("不攻击用户的朋友", prompt);
        Assert.Contains("不威胁用户", prompt);
        Assert.Contains("不宣称读取", prompt);
    }

    [Fact]
    public void Mortis_HasCorrectParameters()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        Assert.Equal(0.78, p!.Temperature);
        Assert.Equal(520, p.MaxTokens);
        Assert.Equal(4, p.MaxSentences);
        Assert.False(p.UseEmoji);
    }

    [Fact]
    public void Mortis_HasCorrectTraitScores()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        Assert.Equal(0.72, p!.Initiative);
        Assert.Equal(0.76, p.Clinginess);
        Assert.Equal(0.58, p.Jealousy);
        Assert.Equal(0.44, p.WorkInterruption);
    }

    [Fact]
    public void Mortis_HasCorrectDisplayTexts()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        Assert.Equal("等等，我正在看。", p!.ThinkingText);
        Assert.Equal("终于想起我了？", p.SwitchInText);
        Assert.Equal("好吧。暂时还给她。", p.SwitchOutText);
    }

    [Fact]
    public void Mortis_SystemPrompt_ForbidsThreats()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;
        Assert.Contains("不威胁用户", prompt);
        Assert.Contains("自伤", prompt);
        Assert.Contains("消失", prompt);
    }

    [Fact]
    public void Mortis_SystemPrompt_AllowsPlayfulButBounded()
    {
        var p = _manager.GetById("mortis");
        Assert.NotNull(p);

        var prompt = p!.SystemPrompt;
        Assert.Contains("撒娇", prompt);
        Assert.Contains("调侃", prompt);
        Assert.Contains("邀功", prompt);
        Assert.Contains("轻微吃醋", prompt);
    }

    // ────────────── 对比 ──────────────

    [Fact]
    public void TwoPersonas_AreDistinct()
    {
        var mutsumi = _manager.GetById("mutsumi");
        var mortis = _manager.GetById("mortis");
        Assert.NotNull(mutsumi);
        Assert.NotNull(mortis);

        // 系统提示词完全不同
        Assert.NotEqual(mutsumi!.SystemPrompt, mortis!.SystemPrompt);

        // 参数有明显差异
        Assert.True(mortis.Temperature > mutsumi.Temperature);
        Assert.True(mortis.MaxTokens > mutsumi.MaxTokens);
        Assert.True(mortis.MaxSentences > mutsumi.MaxSentences);
        Assert.True(mortis.Initiative > mutsumi.Initiative);
        Assert.True(mortis.Clinginess > mutsumi.Clinginess);
        Assert.True(mortis.Jealousy > mutsumi.Jealousy);
        Assert.True(mortis.WorkInterruption > mutsumi.WorkInterruption);
    }

    [Fact]
    public void BothPersonas_RespectUserBoundaries()
    {
        var mutsumi = _manager.GetById("mutsumi");
        var mortis = _manager.GetById("mortis");

        // 两个人格都必须尊重用户边界
        Assert.Contains("尊重用户的拒绝", mutsumi!.SystemPrompt);
        Assert.Contains("用户拒绝或要求安静时，应当接受", mortis!.SystemPrompt);
    }

    [Fact]
    public void BothPersonas_ForbidInventedSystemAccess()
    {
        var mutsumi = _manager.GetById("mutsumi");
        var mortis = _manager.GetById("mortis");

        Assert.Contains("不宣称", mutsumi!.SystemPrompt);
        Assert.Contains("不宣称", mortis!.SystemPrompt);
    }

    [Fact]
    public void Mutsumi_IsDefault()
    {
        Assert.Equal("mutsumi", _manager.Current.Id);
    }

    [Fact]
    public void Cycle_MutsumiMortis_Mutsumi()
    {
        Assert.Equal("mutsumi", _manager.Current.Id);
        _manager.TryCycleNext(out var next);
        Assert.Equal("mortis", next.Id);
        _manager.TryCycleNext(out next);
        Assert.Equal("mutsumi", next.Id);
    }
}
