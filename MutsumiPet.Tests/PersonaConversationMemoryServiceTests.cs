using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class PersonaConversationMemoryServiceTests
{
    /// <summary>
    /// 验证两个人格的对话记忆互相隔离。
    /// </summary>
    [Fact]
    public void RecordExchange_IsolatedPerPersona()
    {
        var service = new PersonaConversationMemoryService();
        service.RecordExchange("mutsumi", "你好", "今天状态不错。");
        service.RecordExchange("mortis", "你好", "有事说事。");

        var mutsumiMemory = service.BuildPromptMemory("mutsumi", "小睦");
        var mortisMemory = service.BuildPromptMemory("mortis", "墨提斯");

        Assert.Contains("小睦：今天状态不错。", mutsumiMemory);
        Assert.DoesNotContain("小睦：今天状态不错。", mortisMemory);
        Assert.Contains("墨提斯：有事说事。", mortisMemory);
        Assert.DoesNotContain("墨提斯：有事说事。", mutsumiMemory);
    }

    /// <summary>
    /// 验证每个人格的对话轮数独立计数。
    /// </summary>
    [Fact]
    public void ExchangeCount_TracksPerPersona()
    {
        var service = new PersonaConversationMemoryService();
        service.RecordExchange("mutsumi", "A", "A'");
        service.RecordExchange("mortis", "B", "B'");
        service.RecordExchange("mutsumi", "C", "C'");

        Assert.Equal(2, service.ExchangeCount("mutsumi"));
        Assert.Equal(1, service.ExchangeCount("mortis"));
    }

    /// <summary>
    /// 验证 Clear 清空指定人格记忆。
    /// </summary>
    [Fact]
    public void Clear_RemovesMemoryForPersona()
    {
        var service = new PersonaConversationMemoryService();
        service.RecordExchange("mutsumi", "A", "A'");
        service.Clear("mutsumi");

        Assert.Equal(0, service.ExchangeCount("mutsumi"));
        Assert.Empty(service.BuildPromptMemory("mutsumi", "小睦"));
    }

    /// <summary>
    /// 验证不存在的人格返回空记忆。
    /// </summary>
    [Fact]
    public void BuildPromptMemory_EmptyPersona_ReturnsEmpty()
    {
        var service = new PersonaConversationMemoryService();
        Assert.Empty(service.BuildPromptMemory("nonexistent", "小睦"));
    }

    /// <summary>
    /// 验证切换人格后恢复原记忆（切回后记忆不变）。
    /// </summary>
    [Fact]
    public void SwitchAndReturn_RestoresMemory()
    {
        var service = new PersonaConversationMemoryService();
        service.RecordExchange("mutsumi", "你好", "状态不错。");

        // 切到 mortis 并记录
        service.RecordExchange("mortis", "嗨", "换我来了。");

        // 切回 mutsumi
        var mutsumiMemory = service.BuildPromptMemory("mutsumi", "小睦");

        Assert.Contains("小睦：状态不错。", mutsumiMemory);
        Assert.DoesNotContain("换我来了", mutsumiMemory);
    }

    /// <summary>
    /// 验证每个人格最多保留 8 轮对话。
    /// </summary>
    [Fact]
    public void RecordExchange_TooManyEntries_TrimsPerPersona()
    {
        var service = new PersonaConversationMemoryService();
        for (var i = 0; i < 15; i++)
        {
            service.RecordExchange("mutsumi", $"M{i}", $"R{i}");
            service.RecordExchange("mortis", $"m{i}", $"r{i}");
        }

        Assert.True(service.ExchangeCount("mutsumi") <= 8);
        Assert.True(service.ExchangeCount("mortis") <= 8);

        var mutsumiMemory = service.BuildPromptMemory("mutsumi", "小睦");
        Assert.DoesNotContain("M0", mutsumiMemory);
        Assert.Contains("M14", mutsumiMemory);
    }
}
