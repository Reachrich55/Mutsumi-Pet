using MutsumiPet.Models;
using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class ChatCommandServiceTests
{
    /// <summary>
    /// 验证所有支持的斜杠命令都能被正确解析。
    /// </summary>
    [Theory]
    [InlineData("/专注", ChatCommandKind.Focus)]
    [InlineData("/结束专注", ChatCommandKind.EndFocus)]
    [InlineData("/休息", ChatCommandKind.Break)]
    [InlineData("/结束休息", ChatCommandKind.EndBreak)]
    [InlineData("/摘要", ChatCommandKind.Summary)]
    [InlineData("/设置", ChatCommandKind.Settings)]
    [InlineData("/帮助", ChatCommandKind.Help)]
    public void Parse_KnownCommand_ReturnsExpectedKind(string input, ChatCommandKind expected)
    {
        var service = new ChatCommandService();

        var command = service.Parse(input);

        Assert.Equal(expected, command.Kind);
    }

    /// <summary>
    /// 验证普通文本不会被当作命令处理。
    /// </summary>
    [Fact]
    public void Parse_NormalText_ReturnsNone()
    {
        var service = new ChatCommandService();

        var command = service.Parse("今天状态怎么样？");

        Assert.Equal(ChatCommandKind.None, command.Kind);
    }

    /// <summary>
    /// 验证未知斜杠命令会被标记为 Unknown。
    /// </summary>
    [Fact]
    public void Parse_UnknownSlashCommand_ReturnsUnknown()
    {
        var service = new ChatCommandService();

        var command = service.Parse("/不存在");

        Assert.Equal(ChatCommandKind.Unknown, command.Kind);
    }

    /// <summary>
    /// 验证空闲状态只展示可开始的专注和休息命令。
    /// </summary>
    [Fact]
    public void GetAvailableCommands_WhenIdle_ReturnsStartCommands()
    {
        var service = new ChatCommandService();

        var commands = service.GetAvailableCommands(FocusSessionState.Idle)
            .Select(command => command.CommandText)
            .ToArray();

        Assert.Contains("/专注", commands);
        Assert.Contains("/休息", commands);
        Assert.DoesNotContain("/结束专注", commands);
        Assert.DoesNotContain("/结束休息", commands);
    }

    /// <summary>
    /// 验证专注状态只展示结束专注命令而不展示开始专注。
    /// </summary>
    [Fact]
    public void GetAvailableCommands_WhenFocusing_ReturnsEndFocusOnly()
    {
        var service = new ChatCommandService();

        var commands = service.GetAvailableCommands(FocusSessionState.Focusing)
            .Select(command => command.CommandText)
            .ToArray();

        Assert.Contains("/结束专注", commands);
        Assert.DoesNotContain("/专注", commands);
        Assert.DoesNotContain("/休息", commands);
    }

    /// <summary>
    /// 验证休息状态只展示结束休息命令而不展示开始休息。
    /// </summary>
    [Fact]
    public void GetAvailableCommands_WhenBreaking_ReturnsEndBreakOnly()
    {
        var service = new ChatCommandService();

        var commands = service.GetAvailableCommands(FocusSessionState.Break)
            .Select(command => command.CommandText)
            .ToArray();

        Assert.Contains("/结束休息", commands);
        Assert.DoesNotContain("/休息", commands);
        Assert.DoesNotContain("/专注", commands);
    }
}
