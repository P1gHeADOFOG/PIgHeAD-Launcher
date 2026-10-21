using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Accounts;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class ProcessAndTokenTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.Launcher.ProcessTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TokenCacheUsesCurrentUserEncryptionAndRejectsCorruption()
    {
        var path = Path.Combine(directory, "auth", "cache.bin");
        var cache = new ProtectedTokenCache(path);
        var secret = Encoding.UTF8.GetBytes("synthetic-test-refresh-token");

        cache.Write(secret);

        Assert.Equal(secret, cache.Read());
        Assert.DoesNotContain("synthetic-test-refresh-token", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
        Assert.NotEqual(secret, File.ReadAllBytes(path));
        File.WriteAllBytes(path, [1, 2, 3]);
        Assert.Throws<CryptographicException>(() => cache.Read());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalLaunchProbesJavaPreservesArgumentsAndRedactsLogTokens(bool isolate)
    {
        var gameDirectory = Path.Combine(directory, "我的 游戏目录");
        var versionDirectory = Path.Combine(gameDirectory, "versions", "test-version");
        Directory.CreateDirectory(versionDirectory);
        var metadata = """
            {
              "id": "test-version", "type": "release", "mainClass": "Test.Main",
              "time": "2025-01-01T00:00:00Z", "releaseTime": "2025-01-01T00:00:00Z",
              "javaVersion": { "component": "java-runtime-delta", "majorVersion": 21 },
              "libraries": [],
              "arguments": {
                "jvm": ["-cp", "${classpath}"],
                "game": ["--username", "${auth_player_name}", "--uuid", "${auth_uuid}",
                  "--accessToken", "${auth_access_token}", "--gameDir", "${game_directory}"]
              }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(versionDirectory, "test-version.json"), metadata);
        await File.WriteAllBytesAsync(Path.Combine(versionDirectory, "test-version.jar"), []);
        var service = new MinecraftGameService(Path.Combine(directory, "logs"));
        var versions = await service.GetVersionsAsync(gameDirectory, false, CancellationToken.None);
        var version = Assert.Single(versions);
        Assert.True(version.IsInstalled);
        var settings = new LauncherSettings
        {
            GameDirectory = gameDirectory,
            JavaPath = Path.Combine(AppContext.BaseDirectory, "ProcessProbe", "PIgHeAD.Launcher.ProcessProbe.exe"),
            MaximumMemoryMb = 2048, IsolateVersions = isolate,
            ExtraJvmArguments = "-Dtest.label=\"hello world\"",
            ExtraGameArguments = "--test-label \"你好 世界\"",
            Language = GameLanguage.SimplifiedChinese,
            WindowWidth = 1600, WindowHeight = 900,
            WindowMode = GameWindowMode.Fullscreen
        };
        var session = new GameSession("TestPlayer", "123456781234123412341234567890ab", "synthetic-access-token", true);

        var result = await service.LaunchAsync(new GameLaunchRequest(settings, version, session, false),
            new InlineProgress(), CancellationToken.None);
        var exit = await result.Completion.WaitAsync(TimeSpan.FromSeconds(15));
        var lines = await File.ReadAllLinesAsync(result.LogFilePath);
        var arguments = JsonSerializer.Deserialize<string[]>(lines.Single(line => line.StartsWith('[')))!;

        Assert.Equal(0, exit);
        Assert.Contains("TestPlayer", arguments);
        Assert.Contains(session.Uuid, arguments);
        var expectedDirectory = GameEnvironment.GetWorkingDirectory(settings, "test-version");
        Assert.Contains(expectedDirectory, arguments);
        Assert.Contains("-Dtest.label=hello world", arguments);
        Assert.Contains("--test-label", arguments);
        Assert.Contains("你好 世界", arguments);
        Assert.Contains("--fullscreen", arguments);
        Assert.Contains("1600", arguments);
        Assert.Contains("900", arguments);
        Assert.Contains("lang:zh_cn", await File.ReadAllLinesAsync(Path.Combine(expectedDirectory, "options.txt")));
        Assert.Contains("[REDACTED]", arguments);
        Assert.DoesNotContain(session.AccessToken, string.Join("\n", lines));
        Assert.Contains("test diagnostic output", lines);
    }

    [Fact]
    public async Task RealServicesInitializeWithoutClientIdOrInstalledVersions()
    {
        var store = new JsonSettingsService(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(new LauncherSettings
        {
            GameDirectory = Path.Combine(directory, "empty-game"),
            MicrosoftClientId = string.Empty
        });
        var settings = new SettingsViewModel(store);
        var games = new MinecraftGameService(Path.Combine(directory, "logs"));
        var downloads = new DownloadsViewModel();
        var accounts = new AccountsViewModel(new MicrosoftAccountService(), settings, downloads);
        var versions = new VersionsViewModel(games, settings, downloads);
        var home = new HomeViewModel(settings, versions, accounts, downloads, games);
        var main = new MainViewModel(home, settings, versions, accounts, downloads);

        await main.InitializeAsync(CancellationToken.None);

        Assert.Same(home, main.CurrentPage);
        Assert.NotNull(settings.SavedSettings);
        Assert.Empty(versions.AvailableVersions);
        Assert.False(home.LaunchGameCommand.CanExecute(null));
        Assert.Null(accounts.MicrosoftSession);
        Assert.False(accounts.UseMicrosoft);
    }

    [Fact]
    public async Task MissingLibraryDirectoryHasNoLocalVersions()
    {
        var versions = await new MinecraftGameService(Path.Combine(directory, "logs"))
            .GetVersionsAsync(Path.Combine(directory, "missing"), false, CancellationToken.None);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task MissingVersionWithDownloadsDisabledDoesNotStartProcess()
    {
        var service = new MinecraftGameService(Path.Combine(directory, "logs"));
        var request = new GameLaunchRequest(new LauncherSettings(),
            new GameVersionInfo("not-installed", "release", false), OfflineSessionFactory.Create("Player"), false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(request, new InlineProgress(), CancellationToken.None));
        Assert.False(Directory.Exists(directory));
    }

    public void Dispose()
    {
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PIgHeAD.Launcher.ProcessTests"));
        var fullPath = Path.GetFullPath(directory);
        if (!fullPath.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete outside the test root.");
        }
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private sealed class InlineProgress : IProgress<GameProgress>
    {
        public void Report(GameProgress value) { }
    }
}
