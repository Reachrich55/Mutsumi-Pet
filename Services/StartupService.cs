using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace MutsumiPet.Services;

/// <summary>
/// 管理当前应用的 Windows 开机自启注册表项。
/// 使用 HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run，
/// 不需要管理员权限。
/// </summary>
public static class StartupService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MutsumiPet";

    /// <summary>
    /// 判断当前是否以独立 exe 方式运行（而非通过 dotnet run/dotnet.dll），
    /// 以及 exe 所在目录是否包含非开发模式的特征文件。
    /// </summary>
    public static bool IsPublishedExecutable
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            var exePath = process.MainModule?.FileName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(exePath))
            {
                return false;
            }

            var exeName = Path.GetFileName(exePath);
            // 如果是通过 dotnet.exe 启动，说明是开发模式
            if (exeName.StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 检查是否以 .exe 结尾（独立发布的 exe）
            return exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 获取当前运行的可执行文件路径（用于写入启动项）。
    /// 开发模式下返回 null。
    /// </summary>
    public static string? GetExecutablePathForStartup()
    {
        if (!IsPublishedExecutable)
        {
            return null;
        }

        using var process = Process.GetCurrentProcess();
        return process.MainModule?.FileName;
    }

    /// <summary>
    /// 读取注册表，判断当前应用是否已设置为开机自启。
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var expectedPath = GetExecutablePathForStartup();
            return expectedPath is not null &&
                   string.Equals(value.Trim('"'), expectedPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 启用开机自启。将当前 exe 的完整路径写入注册表 Run 键。
    /// 开发模式下不执行任何操作。
    /// </summary>
    public static void Enable()
    {
        var exePath = GetExecutablePathForStartup();
        if (exePath is null)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RegistryPath);

            // 路径含空格时用引号包裹
            var quotedPath = exePath.Contains(' ') ? $"\"{exePath}\"" : exePath;
            key?.SetValue(ValueName, quotedPath, RegistryValueKind.String);
        }
        catch (Exception)
        {
            // 权限不足时静默失败，由调用方通过 IsEnabled 反馈
        }
    }

    /// <summary>
    /// 禁用开机自启。从注册表 Run 键中删除当前应用的条目。
    /// </summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
            if (key is null)
            {
                return;
            }

            // 检查值是否存在再删除，避免不必要的异常
            if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // 权限不足时静默失败
        }
    }
}
