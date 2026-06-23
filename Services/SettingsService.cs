using System.Text.Json;
using System.IO;

namespace MutsumiPet.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 初始化设置服务并加载本地设置文件。
    /// </summary>
    public SettingsService()
    {
        Current = LoadFromDisk();
        Save(Current);
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
    /// </summary>
    public void Save(UserSettings settings)
    {
        Current = settings.CloneNormalized();
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
