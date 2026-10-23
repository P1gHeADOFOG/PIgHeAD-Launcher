using System.IO;
using System.Text.Json;
using PIgHeAD.Launcher.Services.Presentation;

namespace PIgHeAD.Launcher.Tests;

public sealed class PresentationSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.PresentationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AppearanceAndLanguagePersistTogetherAndTranslateInterpolatedStatus()
    {
        var path = Path.Combine(directory, "appearance.json");
        var preferences = new PresentationSettings();
        await preferences.InitializeAsync(path);
        await Task.WhenAll(preferences.ToggleLanguageCommand.ExecuteAsync(null), preferences.ToggleThemeCommand.ExecuteAsync(null));
        Assert.Equal("Light", preferences.Theme);
        Assert.Equal("en-US", preferences.Language);
        var restored = new PresentationSettings();
        await restored.InitializeAsync(path);
        Assert.Equal(preferences.Theme, restored.Theme);
        Assert.Equal(preferences.Language, restored.Language);
        Assert.Equal("Player · Offline", restored.Localize("Player · 离线"));
        Assert.Equal("12 release versions ready to install.", restored.Localize("已读取 12 个正式版本，可直接安装。"));
        Assert.Equal("Launch failed: Sign-in cancelled.", restored.Localize("启动失败：登录已取消。"));
        Assert.Equal(@"C:\玩家\我的游戏", restored.Localize(@"C:\玩家\我的游戏"));
        Assert.Equal("数据目录", restored.Localize("数据目录")); // Unknown user data stays intact.
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("{\"Theme\":\"unsupported\",\"Language\":\"unsupported\"}")]
    public async Task InvalidPreferencesFallBackWithoutPreventingStartup(string content)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appearance.json");
        await File.WriteAllTextAsync(path, content);
        var preferences = new PresentationSettings();
        await preferences.InitializeAsync(path);
        Assert.Equal("Dark", preferences.Theme);
        Assert.Equal("zh-CN", preferences.Language);
    }

    [Fact]
    public async Task SaveFailureKeepsTheAppliedChoiceAndReportsFailure()
    {
        var path = Path.Combine(directory, "appearance.json");
        Directory.CreateDirectory(path);
        var preferences = new PresentationSettings();
        await preferences.InitializeAsync(path);
        await preferences.ToggleLanguageCommand.ExecuteAsync(null);
        Assert.Equal("en-US", preferences.Language);
        Assert.Contains("保存失败", preferences.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
