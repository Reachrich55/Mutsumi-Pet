using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class ConversationMemoryServiceTests
{
    /// <summary>
    /// 验证运行期记忆会记录最近对话。
    /// </summary>
    [Fact]
    public void BuildPromptMemory_AfterRecordExchange_ReturnsMemoryText()
    {
        var service = new ConversationMemoryService();

        service.RecordExchange("今天应该先做什么？", "可以先收束当前任务。");

        var memory = service.BuildPromptMemory();

        Assert.Contains("今天应该先做什么", memory);
        Assert.Contains("可以先收束当前任务", memory);
    }

    /// <summary>
    /// 验证运行期记忆会裁剪旧对话。
    /// </summary>
    [Fact]
    public void RecordExchange_TooManyEntries_TrimsOldEntries()
    {
        var service = new ConversationMemoryService();

        for (var i = 0; i < 20; i++)
        {
            service.RecordExchange($"用户消息 {i}", $"回复 {i}");
        }

        var memory = service.BuildPromptMemory();

        Assert.True(service.ExchangeCount <= 8);
        Assert.DoesNotContain("用户消息 0", memory);
        Assert.Contains("用户消息 19", memory);
    }
}
