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

    /// <summary>
    /// 验证默认人格 ID 为 mutsumi。
    /// </summary>
    [Fact]
    public void ActivePersonaId_Default_IsMutsumi()
    {
        var settings = new UserSettings();
        Assert.Equal("mutsumi", settings.ActivePersonaId);
    }

    /// <summary>
    /// 验证 CloneNormalized 保留 ActivePersonaId。
    /// </summary>
    [Fact]
    public void CloneNormalized_CarriesActivePersonaId()
    {
        var settings = new UserSettings { ActivePersonaId = "mortis" };
        var normalized = settings.CloneNormalized();
        Assert.Equal("mortis", normalized.ActivePersonaId);
    }

    /// <summary>
    /// 验证空 ActivePersonaId 回退到 mutsumi。
    /// </summary>
    [Fact]
    public void CloneNormalized_EmptyActivePersonaId_FallsBackToMutsumi()
    {
        var settings = new UserSettings { ActivePersonaId = "" };
        var normalized = settings.CloneNormalized();
        Assert.Equal("mutsumi", normalized.ActivePersonaId);
    }

    // ────────────── 显示设置 ──────────────

    /// <summary>
    /// 验证默认 PetScalePercent 为 150。
    /// </summary>
    [Fact]
    public void PetScalePercent_Default_Is150()
    {
        var settings = new UserSettings();
        Assert.Equal(150.0, settings.PetScalePercent);
    }

    /// <summary>
    /// 验证默认 SpeechFontSizePt 为 16。
    /// </summary>
    [Fact]
    public void SpeechFontSizePt_Default_Is16()
    {
        var settings = new UserSettings();
        Assert.Equal(16.0, settings.SpeechFontSizePt);
    }

    /// <summary>
    /// 验证六个合法 PetScalePercent 档位均能保留。
    /// </summary>
    [Theory]
    [InlineData(50.0)]
    [InlineData(75.0)]
    [InlineData(100.0)]
    [InlineData(125.0)]
    [InlineData(150.0)]
    [InlineData(175.0)]
    public void CloneNormalized_ValidPetScalePercent_Retained(double value)
    {
        var settings = new UserSettings { PetScalePercent = value };
        var normalized = settings.CloneNormalized();
        Assert.Equal(value, normalized.PetScalePercent);
    }

    /// <summary>
    /// 验证所有合法 SpeechFontSizePt 档位均能保留。
    /// </summary>
    [Theory]
    [InlineData(9.0)]
    [InlineData(10.5)]
    [InlineData(12.0)]
    [InlineData(14.0)]
    [InlineData(16.0)]
    [InlineData(18.0)]
    [InlineData(20.0)]
    [InlineData(24.0)]
    [InlineData(28.0)]
    [InlineData(32.0)]
    public void CloneNormalized_ValidSpeechFontSizePt_Retained(double value)
    {
        var settings = new UserSettings { SpeechFontSizePt = value };
        var normalized = settings.CloneNormalized();
        Assert.Equal(value, normalized.SpeechFontSizePt);
    }

    /// <summary>
    /// 验证非法 PetScalePercent 回退到 150。
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(60.0)]
    [InlineData(200.0)]
    [InlineData(-1.0)]
    public void CloneNormalized_InvalidPetScalePercent_FallsBackTo150(double value)
    {
        var settings = new UserSettings { PetScalePercent = value };
        var normalized = settings.CloneNormalized();
        Assert.Equal(150.0, normalized.PetScalePercent);
    }

    /// <summary>
    /// 验证非法 SpeechFontSizePt 回退到 16。
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(8.0)]
    [InlineData(15.0)]
    [InlineData(100.0)]
    public void CloneNormalized_InvalidSpeechFontSizePt_FallsBackTo16(double value)
    {
        var settings = new UserSettings { SpeechFontSizePt = value };
        var normalized = settings.CloneNormalized();
        Assert.Equal(16.0, normalized.SpeechFontSizePt);
    }

    /// <summary>
    /// 验证设置保存不会覆盖 ActivePersonaId。
    /// </summary>
    [Fact]
    public void CloneNormalized_PreservesActivePersonaId_WithDisplaySettings()
    {
        var settings = new UserSettings
        {
            ActivePersonaId = "mortis",
            PetScalePercent = 100.0,
            SpeechFontSizePt = 20.0
        };
        var normalized = settings.CloneNormalized();

        Assert.Equal("mortis", normalized.ActivePersonaId);
        Assert.Equal(100.0, normalized.PetScalePercent);
        Assert.Equal(20.0, normalized.SpeechFontSizePt);
    }
}
