using MutsumiPet.Models;

namespace MutsumiPet.Services;

public sealed class SpeechQueueService
{
    /// <summary>
    /// 用于指示"应尽量放入同一气泡"的大致字符上限。
    /// 不是硬截断值；实际分段由标点优先策略决定。
    /// </summary>
    private const int ComfortableCharLimit = 80;

    /// <summary>
    /// 单个气泡的安全最大字符数（兜底值，仅在标点拆分全部失败后使用）。
    /// </summary>
    private const int SafetyCharLimit = 120;

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

    // ────────────── 分段入口 ──────────────

    private static IEnumerable<SpeechSegment> SplitText(string text)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        // 1. 按强标点拆分
        var clauses = SplitByStrongPunctuation(normalized);

        // 2. 合并可以共同放入同一气泡的短句
        var merged = MergeShortClauses(clauses);

        // 3. 超长从句按弱标点或空格继续拆分
        var split = SplitLongClauses(merged);

        // 4. 最终安全兜底
        var safe = SafetySplit(split);

        foreach (var chunk in safe)
        {
            if (string.IsNullOrWhiteSpace(chunk))
            {
                continue;
            }

            yield return new SpeechSegment(chunk.Trim(), CalculateDuration(chunk));
        }
    }

    // ────────────── 文本规范化 ──────────────

    /// <summary>
    /// 规范化连续空白和换行，折叠多余空格。
    /// </summary>
    private static string NormalizeText(string text)
    {
        // 将各种换行统一为空格
        var normalized = text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace('\t', ' ');

        // 折叠连续空白
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts);
    }

    // ────────────── 标点拆分 ──────────────

    /// <summary>
    /// 优先在句号、问号、感叹号、分号后切分。
    /// 标点保留在前一段的末尾。
    /// </summary>
    private static List<string> SplitByStrongPunctuation(string text)
    {
        var result = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (IsStrongPunctuation(text[i]))
            {
                // 标点后的空格可以跳过
                var end = i + 1;
                var clause = text[start..end].Trim();
                if (!string.IsNullOrWhiteSpace(clause))
                {
                    result.Add(clause);
                }
                start = end;
            }
        }

        // 剩余部分
        var remainder = text[start..].Trim();
        if (!string.IsNullOrWhiteSpace(remainder))
        {
            result.Add(remainder);
        }

        return result;
    }

    /// <summary>
    /// 将可以舒适放入同一气泡的短句合并。
    /// </summary>
    private static List<string> MergeShortClauses(List<string> clauses)
    {
        if (clauses.Count <= 1)
        {
            return clauses;
        }

        var result = new List<string>();
        var buffer = "";

        foreach (var clause in clauses)
        {
            var candidate = string.IsNullOrEmpty(buffer) ? clause : buffer + clause;
            if (candidate.Length <= ComfortableCharLimit)
            {
                buffer = candidate;
            }
            else
            {
                if (!string.IsNullOrEmpty(buffer))
                {
                    result.Add(buffer.Trim());
                }

                buffer = clause;
            }
        }

        if (!string.IsNullOrEmpty(buffer))
        {
            result.Add(buffer.Trim());
        }

        return result;
    }

    /// <summary>
    /// 将仍然过长的从句按弱标点或空格拆分。
    /// 弱标点包括逗号、顿号、省略号、冒号。
    /// </summary>
    private static List<string> SplitLongClauses(List<string> clauses)
    {
        var result = new List<string>();

        foreach (var clause in clauses)
        {
            if (clause.Length <= ComfortableCharLimit)
            {
                result.Add(clause);
                continue;
            }

            // 尝试在弱标点处拆分
            var subClauses = SplitAtWeakPunctuation(clause);
            foreach (var sub in subClauses)
            {
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    result.Add(sub.Trim());
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 在逗号、顿号、省略号、冒号处拆分长句。
    /// 尽量将拆分后的片段保持在合理长度。
    /// </summary>
    private static List<string> SplitAtWeakPunctuation(string text)
    {
        var result = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (IsWeakPunctuation(text[i]))
            {
                var segment = text[start..(i + 1)].Trim();
                if (segment.Length >= ComfortableCharLimit / 3 || i - start > ComfortableCharLimit)
                {
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        result.Add(segment);
                    }
                    start = i + 1;
                }
            }
        }

        var remainder = text[start..].Trim();
        if (!string.IsNullOrWhiteSpace(remainder))
        {
            result.Add(remainder);
        }

        return result;
    }

    // ────────────── 安全兜底 ──────────────

    /// <summary>
    /// 最后的安全兜底：将仍然过长的片段切分，中文不切开常见词组，英文不切开单词。
    /// </summary>
    private static List<string> SafetySplit(List<string> clauses)
    {
        var result = new List<string>();

        foreach (var clause in clauses)
        {
            if (clause.Length <= SafetyCharLimit)
            {
                if (!string.IsNullOrWhiteSpace(clause))
                {
                    result.Add(clause.Trim());
                }
                continue;
            }

            // 兜底切分：按字符遍历，寻找安全的切割点
            var start = 0;
            var currentLength = 0;
            var lastSafeBreak = -1;

            for (var i = 0; i < clause.Length; i++)
            {
                currentLength++;
                var c = clause[i];

                // 记录安全切割点
                if (IsSafeBreakPoint(clause, i))
                {
                    lastSafeBreak = i;
                }

                // 达到安全上限时切分
                if (currentLength >= SafetyCharLimit && lastSafeBreak > start)
                {
                    var segment = clause[start..(lastSafeBreak + 1)].Trim();
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        result.Add(segment);
                    }
                    start = lastSafeBreak + 1;
                    currentLength = i - lastSafeBreak;
                    lastSafeBreak = -1;
                }
                else if (currentLength >= SafetyCharLimit + 20)
                {
                    // 实在找不到安全点，强制切分（但不切多字节字符）
                    var breakPoint = i;
                    // 如果是中文标点，标点跟在前一段
                    if (IsChinesePunctuation(c))
                    {
                        breakPoint = i + 1;
                    }
                    // 英文不切开单词：回退到最近空格
                    else if (IsLatinChar(c))
                    {
                        for (var j = i; j > start && j > i - 30; j--)
                        {
                            if (clause[j] == ' ')
                            {
                                breakPoint = j;
                                break;
                            }
                        }
                        // 如果找不到空格，至少确保不在单词中间
                        if (breakPoint == i && i + 1 < clause.Length && IsLatinChar(clause[i + 1]))
                        {
                            // 回退到上一个非拉丁字符或空格
                            for (var j = i - 1; j > start; j--)
                            {
                                if (!IsLatinChar(clause[j]) || clause[j] == ' ')
                                {
                                    breakPoint = j + 1;
                                    break;
                                }
                            }
                        }
                    }

                    var segment2 = clause[start..breakPoint].Trim();
                    if (!string.IsNullOrWhiteSpace(segment2))
                    {
                        result.Add(segment2);
                    }
                    start = breakPoint;
                    currentLength = i - breakPoint + 1;
                    lastSafeBreak = -1;
                }
            }

            var remainder2 = clause[start..].Trim();
            if (!string.IsNullOrWhiteSpace(remainder2))
            {
                result.Add(remainder2);
            }
        }

        return result;
    }

    // ────────────── 字符判断 ──────────────

    private static bool IsStrongPunctuation(char c)
    {
        return c is '。' or '！' or '？' or '；' or '.' or '!' or '?' or ';';
    }

    private static bool IsWeakPunctuation(char c)
    {
        return c is '，' or '、' or '：' or ',' or ':';
    }

    private static bool IsChinesePunctuation(char c)
    {
        return c is '。' or '！' or '？' or '；' or '，' or '、' or '：' or '…' or '（' or '）' or '“' or '”';
    }

    private static bool IsLatinChar(char c)
    {
        return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
    }

    /// <summary>
    /// 判断当前位置是否可以作为安全切割点。
    /// 包括空格、中文标点后、英文标点后。
    /// </summary>
    private static bool IsSafeBreakPoint(string text, int index)
    {
        if (index < 0 || index >= text.Length)
        {
            return false;
        }

        var c = text[index];

        // 空格总是安全的
        if (c == ' ')
        {
            return true;
        }

        // 标点符号后可以切割
        if (IsStrongPunctuation(c) || IsWeakPunctuation(c))
        {
            return true;
        }

        // 省略号后可以切割
        if (c == '…' && index + 1 < text.Length && text[index + 1] != '…')
        {
            return true;
        }

        return false;
    }

    // ────────────── 时长计算 ──────────────

    /// <summary>
    /// 按文本长度计算每段展示时长。
    /// </summary>
    private static TimeSpan CalculateDuration(string text)
    {
        var seconds = Math.Clamp(2.5 + text.Length * 0.12, 2.5, 8);
        return TimeSpan.FromSeconds(seconds);
    }
}
