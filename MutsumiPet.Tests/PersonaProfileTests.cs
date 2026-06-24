using MutsumiPet.Models;
using System.Text.Json;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class PersonaProfileTests
{
    /// <summary>
    /// 验证完整的 mutsumi JSON 反序列化后所有属性正确。
    /// </summary>
    [Fact]
    public void Deserialize_FullMutsumiJson_AllPropertiesSet()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var jsonPath = Path.Combine(baseDir, "Personas", "mutsumi.json");
        // 测试运行时可能在项目目录而非输出目录，尝试查找
        if (!File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(
                Path.GetDirectoryName(baseDir) ?? "",
                "..", "..", "..", "..",
                "Personas", "mutsumi.json");
        }

        var json = File.ReadAllText(jsonPath);
        var profile = JsonSerializer.Deserialize<PersonaProfile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(profile);
        Assert.Equal("mutsumi", profile.Id);
        Assert.Equal("睦子米", profile.DisplayName);
        Assert.Equal("小睦", profile.AssistantPrefix);
        Assert.Contains("睦子米", profile.SystemPrompt);
        Assert.Equal("……让我想一下。", profile.ThinkingText);
        Assert.True(profile.Temperature > 0);
        Assert.True(profile.MaxTokens > 0);
        Assert.True(profile.MaxSentences > 0);
        Assert.False(profile.UseEmoji);
    }

    /// <summary>
    /// 验证部分 JSON 反序列化时缺失字段使用默认值。
    /// </summary>
    [Fact]
    public void Deserialize_PartialJson_UsesDefaults()
    {
        const string json = """{"id":"test","displayName":"Test"}""";
        var profile = JsonSerializer.Deserialize<PersonaProfile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(profile);
        Assert.Equal("test", profile.Id);
        Assert.Equal("Test", profile.DisplayName);
        Assert.Equal(0.7, profile.Temperature);
        Assert.Equal(600, profile.MaxTokens);
        Assert.Equal(3, profile.MaxSentences);
        Assert.True(profile.UseEmoji);
    }

    /// <summary>
    /// 验证 mortis JSON 反序列化正确。
    /// </summary>
    [Fact]
    public void Deserialize_FullMortisJson_AllPropertiesSet()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var jsonPath = Path.Combine(baseDir, "Personas", "mortis.json");
        if (!File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(
                Path.GetDirectoryName(baseDir) ?? "",
                "..", "..", "..", "..",
                "Personas", "mortis.json");
        }

        var json = File.ReadAllText(jsonPath);
        var profile = JsonSerializer.Deserialize<PersonaProfile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(profile);
        Assert.Equal("mortis", profile.Id);
        Assert.Equal("墨提斯", profile.DisplayName);
        Assert.Equal("墨提斯", profile.AssistantPrefix);
        Assert.Contains("墨提斯", profile.SystemPrompt);
        Assert.True(profile.Temperature > 0);
        Assert.True(profile.MaxTokens > 0);
    }
}
