using System.Text;

namespace MutsumiPet.Services;

public sealed class ConversationMemoryService
{
    private const int MaxRecentExchanges = 8;
    private const int MaxMemoryCharacters = 1400;
    private readonly Queue<(string User, string Assistant)> _recentExchanges = new();

    /// <summary>
    /// 记录一轮用户输入和桌宠回复，仅保存在当前进程内存中。
    /// </summary>
    public void RecordExchange(string userText, string assistantText)
    {
        var user = Normalize(userText, 280);
        var assistant = Normalize(assistantText, 420);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(assistant))
        {
            return;
        }

        _recentExchanges.Enqueue((user, assistant));
        while (_recentExchanges.Count > MaxRecentExchanges)
        {
            _recentExchanges.Dequeue();
        }

        TrimToBudget();
    }

    /// <summary>
    /// 构造可放入 LLM prompt 的运行期记忆文本。
    /// </summary>
    /// <param name="assistantPrefix">当前人格的记忆前缀（如"小睦""墨提斯"）</param>
    public string BuildPromptMemory(string assistantPrefix)
    {
        if (_recentExchanges.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var (user, assistant) in _recentExchanges)
        {
            builder.Append("用户：").Append(user).AppendLine();
            builder.Append(assistantPrefix).Append("：").Append(assistant).AppendLine();
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// 获取当前记忆中的对话轮数。
    /// </summary>
    public int ExchangeCount => _recentExchanges.Count;

    /// <summary>
    /// 清空所有运行时记忆。
    /// </summary>
    public void Clear()
    {
        _recentExchanges.Clear();
    }

    /// <summary>
    /// 按字符预算裁剪最旧的记忆。
    /// </summary>
    private void TrimToBudget()
    {
        while (_recentExchanges.Count > 0 && BuildPromptMemory("X").Length > MaxMemoryCharacters)
        {
            _recentExchanges.Dequeue();
        }
    }

    /// <summary>
    /// 清理文本中的换行和多余空白，并限制长度。
    /// </summary>
    private static string Normalize(string value, int maxLength)
    {
        var normalized = string.Join(
            " ",
            value.Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("\t", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "…";
    }
}
