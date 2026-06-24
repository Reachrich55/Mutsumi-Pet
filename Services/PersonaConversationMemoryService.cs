namespace MutsumiPet.Services;

/// <summary>
/// 按人格 ID 管理多个独立的 ConversationMemoryService 实例。
/// 切换人格后只读取该人格自己的对话记忆；切回后恢复原记忆。
/// </summary>
public sealed class PersonaConversationMemoryService
{
    private readonly Dictionary<string, ConversationMemoryService> _memories = new();
    private readonly object _lock = new();

    /// <summary>
    /// 记录一轮对话到指定人格的记忆中。
    /// </summary>
    /// <param name="personaId">人格 ID</param>
    /// <param name="userText">用户输入文本</param>
    /// <param name="assistantText">桌宠回复文本</param>
    public void RecordExchange(string personaId, string userText, string assistantText)
    {
        var memory = GetOrCreate(personaId);
        memory.RecordExchange(userText, assistantText);
    }

    /// <summary>
    /// 构造指定人格的 LLM prompt 记忆文本。
    /// </summary>
    /// <param name="personaId">人格 ID</param>
    /// <param name="assistantPrefix">该人格的记忆前缀（如"小睦""墨提斯"）</param>
    public string BuildPromptMemory(string personaId, string assistantPrefix)
    {
        var memory = GetOrCreate(personaId);
        return memory.BuildPromptMemory(assistantPrefix);
    }

    /// <summary>
    /// 获取指定人格当前记忆中的对话轮数。
    /// </summary>
    public int ExchangeCount(string personaId)
    {
        lock (_lock)
        {
            return _memories.TryGetValue(personaId, out var m) ? m.ExchangeCount : 0;
        }
    }

    /// <summary>
    /// 清空指定人格的运行时记忆。
    /// </summary>
    public void Clear(string personaId)
    {
        lock (_lock)
        {
            if (_memories.TryGetValue(personaId, out var m))
            {
                m.Clear();
            }
        }
    }

    /// <summary>
    /// 获取或创建指定人格的 ConversationMemoryService。
    /// </summary>
    private ConversationMemoryService GetOrCreate(string personaId)
    {
        lock (_lock)
        {
            if (!_memories.TryGetValue(personaId, out var memory))
            {
                memory = new ConversationMemoryService();
                _memories[personaId] = memory;
            }

            return memory;
        }
    }
}
