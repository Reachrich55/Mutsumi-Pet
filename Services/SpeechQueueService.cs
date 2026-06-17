using MutsuPet.Models;

namespace MutsuPet.Services;

public sealed class SpeechQueueService
{
    private const int MaxSegmentLength = 44;
    private readonly Queue<SpeechSegment> _segments = new();

    /// <summary>
    /// 获取当前队列中是否还有待展示的文本段。
    /// </summary>
    public bool HasPendingSegments => _segments.Count > 0;

    /// <summary>
    /// 清空当前全部待展示文本段。
    /// </summary>
    public void Clear()
    {
        _segments.Clear();
    }

    /// <summary>
    /// 将完整文本拆分为多个气泡段并加入展示队列。
    /// </summary>
    public void EnqueueText(string text, bool replaceExisting)
    {
        if (replaceExisting)
        {
            Clear();
        }

        foreach (var segment in SplitText(text))
        {
            _segments.Enqueue(segment);
        }
    }

    /// <summary>
    /// 尝试取出下一段需要显示的气泡文本。
    /// </summary>
    public bool TryDequeue(out SpeechSegment segment)
    {
        if (_segments.Count == 0)
        {
            segment = new SpeechSegment(string.Empty, TimeSpan.Zero);
            return false;
        }

        segment = _segments.Dequeue();
        return true;
    }

    /// <summary>
    /// 将长文本按中文标点和最大长度切成气泡可容纳的分段。
    /// </summary>
    private static IEnumerable<SpeechSegment> SplitText(string text)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        foreach (var chunk in SplitByLength(SplitByPunctuation(normalized)))
        {
            yield return new SpeechSegment(chunk, CalculateDuration(chunk));
        }
    }

    /// <summary>
    /// 清理文本中的多余空白和换行。
    /// </summary>
    private static string NormalizeText(string text)
    {
        return string.Join(
            " ",
            text.Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// 优先按自然中文标点切分长回复。
    /// </summary>
    private static IEnumerable<string> SplitByPunctuation(string text)
    {
        var buffer = new List<char>();
        foreach (var character in text)
        {
            buffer.Add(character);
            if (IsStrongPunctuation(character) || buffer.Count >= MaxSegmentLength)
            {
                yield return new string(buffer.ToArray()).Trim();
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            yield return new string(buffer.ToArray()).Trim();
        }
    }

    /// <summary>
    /// 将仍然过长的段落按固定长度继续切分。
    /// </summary>
    private static IEnumerable<string> SplitByLength(IEnumerable<string> chunks)
    {
        foreach (var chunk in chunks)
        {
            var remaining = chunk;
            while (remaining.Length > MaxSegmentLength)
            {
                yield return remaining[..MaxSegmentLength];
                remaining = remaining[MaxSegmentLength..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(remaining))
            {
                yield return remaining;
            }
        }
    }

    /// <summary>
    /// 判断字符是否适合作为自动翻页分隔点。
    /// </summary>
    private static bool IsStrongPunctuation(char character)
    {
        return character is '。' or '！' or '？' or '；' or '.' or '!' or '?' or ';';
    }

    /// <summary>
    /// 按文本长度计算每段展示时长。
    /// </summary>
    private static TimeSpan CalculateDuration(string text)
    {
        var seconds = Math.Clamp(2.5 + text.Length * 0.12, 2.5, 8);
        return TimeSpan.FromSeconds(seconds);
    }
}
