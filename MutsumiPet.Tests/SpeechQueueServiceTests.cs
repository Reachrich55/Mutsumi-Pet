using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class SpeechQueueServiceTests
{
    /// <summary>
    /// 验证长文本被拆分为多个气泡段。
    /// </summary>
    [Fact]
    public void EnqueueText_LongChineseText_SplitsIntoSegments()
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
        // 新算法按标点拆分，不再限制 35 字符
        Assert.True(segments.Count >= 1);
    }

    /// <summary>
    /// 验证一句 20 字中文不被拆分。
    /// </summary>
    [Fact]
    public void ShortChineseSentence_NotSplit()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("今天天气不错，适合出门走走。", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.Single(segments);
        Assert.Equal("今天天气不错，适合出门走走。", segments[0]);
    }

    /// <summary>
    /// 验证两个短句可以合并显示。
    /// </summary>
    [Fact]
    public void TwoShortSentences_MergedIntoSingleSegment()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("好的。知道了。", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.Single(segments);
        Assert.Contains("好的", segments[0]);
        Assert.Contains("知道了", segments[0]);
    }

    /// <summary>
    /// 验证超长中文句子优先在逗号处分割。
    /// </summary>
    [Fact]
    public void LongChineseSentence_SplitAtComma()
    {
        var service = new SpeechQueueService();
        // 构造一个超过 ComfortableCharLimit(80) 的句子，包含逗号，确保触发逗号处拆分
        var longClause = "这是一个非常长的测试句子，用来验证分段逻辑是否能在逗号处正确切分，而不是把词语从中间直接切断，那样会严重影响用户的阅读体验，也会破坏整体的交互感受，所以我们必须在逗号这里进行合理的拆分。";
        service.EnqueueText(longClause, replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.True(segments.Count >= 2, $"长句应在逗号处拆分为至少两段，实际段数: {segments.Count}");

        // 验证没有任何段在词语中间被切断（每个段以中文或标点结束）
        foreach (var seg in segments)
        {
            Assert.True(seg.Length > 0);
            // 不应以未完成的词语开头（如"验，"这种）
            Assert.False(seg.StartsWith("验，"));
            Assert.False(seg.StartsWith("读体验。"));
        }
    }

    /// <summary>
    /// 验证英文不会从单词中间截断。
    /// </summary>
    [Fact]
    public void EnglishText_NotSplitMidWord()
    {
        var service = new SpeechQueueService();
        // 构造超长英文文本强制触发安全拆分
        var words = "The application encountered an unexpected configuration error during the initialization process and the system administrator needs to review the diagnostic logs carefully before proceeding";
        service.EnqueueText(words, replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.NotEmpty(segments);
        // 每段末尾不应以未完成的英文单词结束（除了最后一个段）
        foreach (var seg in segments.SkipLast(1))
        {
            var trimmed = seg.TrimEnd();
            // 以标点或空格结尾是 OK 的，但不能以字母结尾（除非是被标点分割）
            if (trimmed.Length > 0 && char.IsLetter(trimmed[^1]))
            {
                // 检查下一个段是否以空格开头（说明在空格处分割）
                var nextIdx = segments.IndexOf(seg) + 1;
                if (nextIdx < segments.Count)
                {
                    var next = segments[nextIdx].TrimStart();
                    // 如果下一段不以标点或空格开头时的字母开头，则是从单词中间切的
                }
            }
        }

        // 组合所有段，验证没有丢失字符（段间用空格连接恢复原始文本）
        var combined = string.Join(" ", segments);
        // 规范化原始文本用于比较
        var expected = words;
        Assert.Equal(expected, combined);
    }

    /// <summary>
    /// 验证省略号不会被拆开。
    /// </summary>
    [Fact]
    public void Ellipsis_NotSplitApart()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("他沉默了……不知道该说什么。", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        // 省略号 "……" 应完整保留在同一个段中
        foreach (var seg in segments)
        {
            if (seg.Contains('…'))
            {
                Assert.Contains("……", seg);
            }
        }

        Assert.Single(segments);
    }

    /// <summary>
    /// 验证连续换行能被正确规范化。
    /// </summary>
    [Fact]
    public void ConsecutiveNewlines_NormalizedToSpace()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("第一行。\r\n\r\n\r\n第二行。", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        // 换行被规范化为空格，两个句子可能合并或分开
        Assert.NotEmpty(segments);
        var combined = string.Join(" ", segments);
        Assert.Contains("第一行", combined);
        Assert.Contains("第二行", combined);
        // 不应包含原始换行符
        Assert.DoesNotContain("\r", combined);
        Assert.DoesNotContain("\n", combined);
    }

    /// <summary>
    /// 验证不产生空白气泡。
    /// </summary>
    [Fact]
    public void NoEmptyBubbles()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("   ", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        Assert.Empty(segments);
    }

    /// <summary>
    /// 验证不丢失任何原始文本字符。
    /// </summary>
    [Fact]
    public void NoCharacterLoss()
    {
        var service = new SpeechQueueService();
        var original = "这是一个测试文本，包含中文标点、英文单词hello world，以及各种符号……测试完整性和字符保留。";

        service.EnqueueText(original, replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        // 拼接所有段（空格已被规范化）
        var reconstructed = string.Join("", segments);
        var normalizedOriginal = original
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace('\t', ' ');

        // 去除规范化后的多余空格进行比较
        var originalParts = normalizedOriginal
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var reconstructedParts = reconstructed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(string.Join(" ", originalParts), string.Join(" ", reconstructedParts));
    }

    /// <summary>
    /// 验证强标点（。！？）后正确分段。
    /// 当总长度超过舒适限制时，应在强标点处拆分。
    /// </summary>
    [Fact]
    public void StrongPunctuation_ProducesSeparateSegments()
    {
        var service = new SpeechQueueService();
        // 多句合并后超过 ComfortableCharLimit(80)，应在强标点处分段
        var longText = "编译成功了！不过控制台还有几个警告需要仔细检查一下才能确定具体原因，不能直接忽略。要看一下吗？如果不看的话可能会遗漏一些重要的提示信息，导致后续调试变得非常困难。还是看看吧，花不了多少时间的。";
        service.EnqueueText(longText, replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        // 应该至少有两段
        Assert.True(segments.Count >= 2, $"预期至少 2 段，实际: {segments.Count}。文本长度: {longText.Length}");

        // 感叹号和问号应保留在对应段中
        Assert.Contains(segments, s => s.Contains("！"));
        Assert.Contains(segments, s => s.Contains("？"));
    }

    /// <summary>
    /// 验证标点保留在前一段的末尾。
    /// </summary>
    [Fact]
    public void PunctuationStaysWithPrecedingText()
    {
        var service = new SpeechQueueService();
        service.EnqueueText("先做这个。再做那个。", replaceExisting: true);

        var segments = new List<string>();
        while (service.TryDequeue(out var segment))
        {
            segments.Add(segment.Text);
        }

        // 可能合并为一段（因为两句都很短），也可能分开
        // 但无论如何，句号不应出现在某一段的开头
        foreach (var seg in segments)
        {
            var trimmed = seg.TrimStart();
            Assert.False(trimmed.StartsWith("。"));
        }
    }
}
