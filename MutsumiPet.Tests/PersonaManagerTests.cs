using MutsumiPet.Models;
using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class PersonaManagerTests
{
    /// <summary>
    /// 验证有效初始 ID 正确设置当前人格。
    /// </summary>
    [Fact]
    public void Constructor_ValidInitialId_SetsCurrent()
    {
        var manager = new PersonaManager("mutsumi");
        Assert.Equal("mutsumi", manager.Current.Id);
        Assert.Equal("睦子米", manager.Current.DisplayName);
    }

    /// <summary>
    /// 验证无效初始 ID 回退到 mutsumi。
    /// </summary>
    [Fact]
    public void Constructor_InvalidInitialId_FallsBackToMutsumi()
    {
        var manager = new PersonaManager("nonexistent");
        Assert.NotNull(manager.Current);
        Assert.Equal("mutsumi", manager.Current.Id);
    }

    /// <summary>
    /// 验证 SetCurrent 有效 ID 返回 true 且切换成功。
    /// </summary>
    [Fact]
    public void SetCurrent_ValidId_ReturnsTrueAndChangesCurrent()
    {
        var manager = new PersonaManager("mutsumi");
        var result = manager.SetCurrent("mortis");
        Assert.True(result);
        Assert.Equal("mortis", manager.Current.Id);
    }

    /// <summary>
    /// 验证 SetCurrent 无效 ID 返回 false 且保持不变。
    /// </summary>
    [Fact]
    public void SetCurrent_InvalidId_ReturnsFalseAndKeepsCurrent()
    {
        var manager = new PersonaManager("mutsumi");
        var result = manager.SetCurrent("nonexistent");
        Assert.False(result);
        Assert.Equal("mutsumi", manager.Current.Id);
    }

    /// <summary>
    /// 验证 SetCurrent 切换到同一人格不触发事件且返回 false。
    /// </summary>
    [Fact]
    public void SetCurrent_SamePersona_ReturnsFalse()
    {
        var manager = new PersonaManager("mutsumi");
        var result = manager.SetCurrent("mutsumi");
        Assert.False(result);
    }

    /// <summary>
    /// 验证 SetCurrent 触发 CurrentPersonaChanged 事件。
    /// </summary>
    [Fact]
    public void SetCurrent_FiresEvent()
    {
        var manager = new PersonaManager("mutsumi");
        PersonaProfile? eventArg = null;
        manager.CurrentPersonaChanged += (_, p) => eventArg = p;

        manager.SetCurrent("mortis");
        Assert.NotNull(eventArg);
        Assert.Equal("mortis", eventArg!.Id);
    }

    /// <summary>
    /// 验证 TryCycleNext 在两个人格之间循环：mutsumi → mortis → mutsumi。
    /// </summary>
    [Fact]
    public void TryCycleNext_CyclesDeterministically()
    {
        var manager = new PersonaManager("mutsumi");

        // mutsumi → mortis
        Assert.True(manager.TryCycleNext(out var next));
        Assert.Equal("mortis", next.Id);

        // mortis → mutsumi
        Assert.True(manager.TryCycleNext(out next));
        Assert.Equal("mutsumi", next.Id);

        // mutsumi → mortis
        Assert.True(manager.TryCycleNext(out next));
        Assert.Equal("mortis", next.Id);
    }

    /// <summary>
    /// 验证 SetCurrent 递增 Generation。
    /// </summary>
    [Fact]
    public void SetCurrent_IncrementsGeneration()
    {
        var manager = new PersonaManager("mutsumi");
        var gen0 = manager.Generation;

        manager.SetCurrent("mortis");
        Assert.True(manager.Generation > gen0);

        var gen1 = manager.Generation;

        // 切换到同一人格不应递增
        manager.SetCurrent("mortis");
        Assert.Equal(gen1, manager.Generation);

        manager.SetCurrent("mutsumi");
        Assert.True(manager.Generation > gen1);
    }

    /// <summary>
    /// 验证 Exists 对有效和无效 ID 返回正确结果。
    /// </summary>
    [Fact]
    public void Exists_ReturnsCorrectly()
    {
        var manager = new PersonaManager("mutsumi");
        Assert.True(manager.Exists("mutsumi"));
        Assert.True(manager.Exists("mortis"));
        Assert.False(manager.Exists("nonexistent"));
    }

    /// <summary>
    /// 验证 AllPersonas 返回所有已加载人格。
    /// </summary>
    [Fact]
    public void AllPersonas_ReturnsAllLoaded()
    {
        var manager = new PersonaManager("mutsumi");
        Assert.True(manager.AllPersonas.Count >= 2);
    }

    /// <summary>
    /// 验证 GetById 正确获取人格。
    /// </summary>
    [Fact]
    public void GetById_ReturnsCorrectly()
    {
        var manager = new PersonaManager("mutsumi");
        var mortis = manager.GetById("mortis");
        Assert.NotNull(mortis);
        Assert.Equal("墨提斯", mortis!.DisplayName);

        var nonexistent = manager.GetById("nonexistent");
        Assert.Null(nonexistent);
    }
}
