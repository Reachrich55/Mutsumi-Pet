using System.Text.Json;
using System.IO;

namespace MutsumiPet.Services;

/// <summary>
/// 管理用户设置的持久化读写。
/// 配置文件路径：%AppData%\MutsumiPet\settings.json
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 初始化设置服务并加载本地设置文件。
    /// </summary>
    public SettingsService()
    {
        Current = LoadFromDisk();
    }

    /// <summary>
    /// 当设置被保存后触发。
    /// </summary>
    public event EventHandler<UserSettings>? SettingsChanged;

    /// <summary>
    /// 获取当前规范化后的用户设置。
    /// </summary>
    public UserSettings Current { get; private set; }

    /// <summary>
    /// 获取应用运行时数据目录。
    /// </summary>
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MutsumiPet");

    /// <summary>
    /// 获取用户设置文件路径。
    /// </summary>
    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>
    /// 保存用户设置并通知订阅者。
    /// API Key 以 DPAPI 加密后写入，不会明文存储。
    /// </summary>
    public void Save(UserSettings settings, string? plaintextApiKey = null)
    {
        var normalized = settings.CloneNormalized();

        // 如果传入了新的明文 Key，加密后存储
        if (!string.IsNullOrWhiteSpace(plaintextApiKey))
        {
            normalized.ApiKeyEncrypted = ApiKeyProtection.Protect(plaintextApiKey);
        }
        // 否则保留已有的加密值（如果有的话），不覆盖为 null

        Current = normalized;
        Directory.CreateDirectory(AppDataDirectory);
        var json = JsonSerializer.Serialize(Current, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
        SettingsChanged?.Invoke(this, Current);
    }

    /// <summary>
    /// 从磁盘加载设置，失败时返回默认设置。
    /// </summary>
    private static UserSettings LoadFromDisk()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions);
            return settings?.CloneNormalized() ?? new UserSettings();
        }
        catch (IOException)
        {
            return new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new UserSettings();
        }
    }
}
