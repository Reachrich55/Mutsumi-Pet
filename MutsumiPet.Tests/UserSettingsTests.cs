using MutsumiPet.Services;
using System.Text.Json;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class UserSettingsTests
{
    /// <summary>
    /// 验证非法数值会回落到默认配置。
    /// </summary>
    [Fact]
    public void CloneNormalized_InvalidNumbers_ReturnsDefaults()
    {
        var settings = new UserSettings
        {
            IdleThresholdSeconds = -1,
            ContinuousUseMinutes = 999
        };

        var normalized = settings.CloneNormalized();

        Assert.Equal(180, normalized.IdleThresholdSeconds);
        Assert.Equal(45, normalized.ContinuousUseMinutes);
    }

    /// <summary>
    /// 验证旧设置文件中的专注和休息时长字段会被忽略。
    /// </summary>
    [Fact]
    public void Deserialize_LegacyFocusAndBreakFields_IgnoresExtraFields()
    {
        const string json = """
            {
              "EnableTracking": true,
              "IdleThresholdSeconds": 120,
              "ContinuousUseMinutes": 30,
              "FocusMinutes": 25,
              "BreakMinutes": 5
            }
            """;

        var settings = JsonSerializer.Deserialize<UserSettings>(json);
        var normalized = settings?.CloneNormalized();

        Assert.NotNull(normalized);
        Assert.True(normalized.EnableTracking);
        Assert.Equal(120, normalized.IdleThresholdSeconds);
        Assert.Equal(30, normalized.ContinuousUseMinutes);
    }
}
