using System.IO;

namespace MutsumiPet.Services;

public static class DotEnv
{
    /// <summary>
    /// 从当前目录、程序目录及其父目录查找并加载第一个 .env 文件。
    /// </summary>
    public static IReadOnlyDictionary<string, string> LoadNearest()
    {
        foreach (var path in EnumerateCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(path))
            {
                if (TryParseLine(line, out var key, out var value))
                {
                    values[key] = value;
                }
            }

            return values;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按优先级枚举可能的 .env 文件路径。
    /// </summary>
    private static IEnumerable<string> EnumerateCandidatePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            foreach (var candidate in WalkUp(root))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>
    /// 从起始目录向父目录逐级查找 .env。
    /// </summary>
    private static IEnumerable<string> WalkUp(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, ".env");
            directory = directory.Parent;
        }
    }

    /// <summary>
    /// 解析单行 .env 配置为键值对。
    /// </summary>
    private static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        key = trimmed[..equalsIndex].Trim();
        value = Unquote(trimmed[(equalsIndex + 1)..].Trim());
        return !string.IsNullOrWhiteSpace(key);
    }

    /// <summary>
    /// 去除 .env 值两侧可选的单引号或双引号。
    /// </summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
