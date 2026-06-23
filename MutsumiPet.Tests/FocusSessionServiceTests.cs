using MutsumiPet.Models;
using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class FocusSessionServiceTests
{
    /// <summary>
    /// 验证专注状态下不能直接进入休息状态。
    /// </summary>
    [Fact]
    public void TryStartBreak_WhenFocusing_ReturnsFalse()
    {
        var service = new FocusSessionService();
        var now = DateTimeOffset.Now;

        var focusStarted = service.TryStartFocus(now);
        var breakStarted = service.TryStartBreak(now.AddMinutes(1));

        Assert.True(focusStarted);
        Assert.False(breakStarted);
        Assert.Equal(FocusSessionState.Focusing, service.State);
    }

    /// <summary>
    /// 验证休息状态下不能直接进入专注状态。
    /// </summary>
    [Fact]
    public void TryStartFocus_WhenBreaking_ReturnsFalse()
    {
        var service = new FocusSessionService();
        var now = DateTimeOffset.Now;

        var breakStarted = service.TryStartBreak(now);
        var focusStarted = service.TryStartFocus(now.AddMinutes(1));

        Assert.True(breakStarted);
        Assert.False(focusStarted);
        Assert.Equal(FocusSessionState.Break, service.State);
    }
}
