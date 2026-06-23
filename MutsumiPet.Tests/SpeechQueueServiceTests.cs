using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class SpeechQueueServiceTests
{
    /// <summary>
    /// 验证长文本会被切成气泡可容纳的短段。
    /// </summary>
    [Fact]
    public void EnqueueText_LongChineseText_SplitsIntoBoundedSegments()
    {
        var service = new SpeechQueueService();
        var text = "今天的使用记录已经整理好了。你在专注应用里停留了比较久，中间也出现了几次切换。可以先收束当前任务，再决定是否进入下一轮专注。";

        service.EnqueueText(text, replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.NotEmpty(segments);
        Assert.All(segments, segment => Assert.True(segment.Length <= 35));
    }
}
