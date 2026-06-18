using System.IO;

namespace MutsumiPet.Services;

public static class AssetPathResolver
{
    /// <summary>
    /// 在程序目录、当前目录及父目录中查找必需资源文件。
    /// </summary>
    public static string FindRequired(string fileName)
    {
        foreach (var candidate in EnumerateCandidateFilePaths(fileName))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"未找到资源文件 {fileName}");
    }

    /// <summary>
    /// 按优先级枚举可能包含指定资源的路径。
    /// </summary>
    private static IEnumerable<string> EnumerateCandidateFilePaths(string fileName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            foreach (var candidate in WalkUp(root, fileName))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>
    /// 从起始目录向父目录逐级拼接目标文件名。
    /// </summary>
    private static IEnumerable<string> WalkUp(string startDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, fileName);
            directory = directory.Parent;
        }
    }
}
