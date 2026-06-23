using MutsumiPet.Models;
using MutsumiPet.Services;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class AppClassifierServiceTests
{
    /// <summary>
    /// 验证常见开发工具会被识别为专注应用。
    /// </summary>
    [Fact]
    public void ClassifyProcess_DevelopmentTool_ReturnsFocus()
    {
        var classifier = new AppClassifierService();

        var category = classifier.ClassifyProcess("Code");

        Assert.Equal(AppCategory.Focus, category);
    }

    /// <summary>
    /// 验证聊天软件会被识别为通信应用。
    /// </summary>
    [Fact]
    public void ClassifyProcess_ChatApp_ReturnsCommunication()
    {
        var classifier = new AppClassifierService();

        var category = classifier.ClassifyProcess("WeChat");

        Assert.Equal(AppCategory.Communication, category);
    }

    /// <summary>
    /// 验证未知进程会回落到 Other。
    /// </summary>
    [Fact]
    public void ClassifyProcess_UnknownProcess_ReturnsOther()
    {
        var classifier = new AppClassifierService();

        var category = classifier.ClassifyProcess("custom_tool");

        Assert.Equal(AppCategory.Other, category);
    }
}
