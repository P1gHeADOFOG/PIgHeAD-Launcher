using System.IO;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class PortableWorkspaceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PIgHeAD.PortableTests", Guid.NewGuid().ToString("N"));
    private string Pack(string name)
    {
        var game = Path.Combine(root, name, ".minecraft");
        Directory.CreateDirectory(Path.Combine(game, "versions"));
        return game;
    }

    [Fact]
    public async Task AdjacentPackWinsOverLegacySettingsAndDoesNotDependOnWorkingDirectory()
    {
        var local = Pack("本地整合包");
        var old = Pack("以前的游戏");
        var legacy = Path.Combine(root, "legacy.json");
        await new JsonSettingsService(legacy).SaveAsync(new LauncherSettings { GameDirectory = old });
        var paths = new LauncherPaths(Path.GetDirectoryName(local)!);
        var service = new PortableSettingsService(paths, legacy);

        var result = await service.LoadAsync();

        Assert.Equal(local, result.Settings.GameDirectory);
        Assert.Equal(new[] { local }, service.GameDirectories);
        Assert.True(File.Exists(Path.Combine(paths.DataDirectory, "library.json")));
        Assert.True(File.Exists(Path.Combine(paths.DataDirectory, "settings.json")));
        Assert.Equal(old, (await new JsonSettingsService(legacy).LoadAsync()).Settings.GameDirectory);
    }

    [Fact]
    public async Task NewFolderCreatesPhlAndUsesItsOwnMinecraftInsteadOfLegacyPack()
    {
        var old = Pack("old");
        var legacy = Path.Combine(root, "legacy.json");
        await new JsonSettingsService(legacy).SaveAsync(new LauncherSettings { GameDirectory = old, MaximumMemoryMb = 8192 });
        var paths = new LauncherPaths(Path.Combine(root, "new"));

        var result = await new PortableSettingsService(paths, legacy).LoadAsync();

        Assert.Equal(Path.Combine(paths.LauncherDirectory, ".minecraft"), result.Settings.GameDirectory);
        Assert.Equal(8192, result.Settings.MaximumMemoryMb);
        Assert.True(Directory.Exists(paths.DataDirectory));
        Assert.False(File.Exists(Path.Combine(paths.DataDirectory, "auth", "anything.bin")));
    }

    [Fact]
    public async Task GamesKeepIndependentMemoryArgumentsAndPreferredVersionAcrossRestarts()
    {
        var first = Pack("A");
        var second = Pack("B");
        var paths = new LauncherPaths(Path.GetDirectoryName(first)!);
        var service = new PortableSettingsService(paths);
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = first, MaximumMemoryMb = 8192, PreferredVersionId = "forge", ExtraJvmArguments = "-XX:+UseG1GC" });
        await service.SaveAsync(new LauncherSettings { GameDirectory = second, MaximumMemoryMb = 2048, PreferredVersionId = "fabric" });

        var reopened = new PortableSettingsService(paths);
        Assert.Equal(second, (await reopened.LoadAsync()).Settings.GameDirectory);
        Assert.Equal(2, reopened.GameDirectories.Count);
        var restored = await reopened.LoadGameAsync(first, CancellationToken.None);
        Assert.Equal(8192, restored.MaximumMemoryMb);
        Assert.Equal("forge", restored.PreferredVersionId);
        Assert.Equal("-XX:+UseG1GC", restored.ExtraJvmArguments);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(second)!, "PHL", "settings.json")));
    }

    [Fact]
    public async Task MovingWholePackRebasesMinecraftAndLocalJavaPaths()
    {
        var game = Pack("original");
        var container = Path.GetDirectoryName(game)!;
        var java = Path.Combine(container, "java21", "bin", "javaw.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(java)!);
        await File.WriteAllTextAsync(java, "test fixture, never executed");
        var service = new PortableSettingsService(new LauncherPaths(container));
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = game, JavaPath = java, MaximumMemoryMb = 6144 });
        var moved = Path.Combine(root, "移动后的整合包");
        Directory.Move(container, moved);

        var settings = (await new PortableSettingsService(new LauncherPaths(moved)).LoadAsync()).Settings;

        Assert.Equal(Path.Combine(moved, ".minecraft"), settings.GameDirectory);
        Assert.Equal(Path.Combine(moved, "java21", "bin", "javaw.exe"), settings.JavaPath);
        Assert.Equal(6144, settings.MaximumMemoryMb);
    }

    [Fact]
    public async Task ExternalPackPathRemainsAbsoluteWhenLauncherMoves()
    {
        var local = Pack("launcher");
        var external = Pack("external");
        var service = new PortableSettingsService(new LauncherPaths(Path.GetDirectoryName(local)!));
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = external });
        var moved = Path.Combine(root, "moved-launcher");
        Directory.Move(Path.GetDirectoryName(local)!, moved);

        var reopened = new PortableSettingsService(new LauncherPaths(moved));
        Assert.Equal(external, (await reopened.LoadAsync()).Settings.GameDirectory);
    }

    [Fact]
    public async Task MissingSelectedDirectoryCannotReplaceSavedConfiguration()
    {
        var first = Pack("A");
        var second = Pack("B");
        var service = new PortableSettingsService(new LauncherPaths(Path.GetDirectoryName(first)!));
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = second });
        var viewModel = new SettingsViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var saved = viewModel.SavedSettings;
        viewModel.SelectedGameDirectory = viewModel.GameDirectories.Single(item => item.DirectoryPath == first);
        Directory.Move(Path.GetDirectoryName(first)!, Path.Combine(root, "moved"));

        await viewModel.SwitchGameDirectoryCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Same(saved, viewModel.SavedSettings);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task DirectorySwitchRefreshesVersionsAndIsBlockedWhileGameOperationRuns()
    {
        var first = Pack("A");
        var second = Pack("B");
        var service = new PortableSettingsService(new LauncherPaths(Path.GetDirectoryName(first)!));
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = second });
        var downloads = new DownloadsViewModel();
        var settings = new SettingsViewModel(service, operations: downloads);
        var versions = new VersionsViewModel(new LauncherTestContext.FakeGameService(), settings, downloads);
        await settings.LoadCommand.ExecuteAsync(null);
        settings.SelectedGameDirectory = settings.GameDirectories.Single(item => item.DirectoryPath == first);
        downloads.Begin("test", () => { });
        Assert.False(settings.SwitchGameDirectoryCommand.CanExecute(null));
        Assert.False(versions.SwitchDirectoryCommand.CanExecute(null));
        downloads.End("done");

        await versions.SwitchDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(first, settings.SavedSettings!.GameDirectory);
        Assert.Equal("1.21.4", versions.SelectedGameVersion!.Id);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(first)!, "PHL"), settings.GameDataDirectory);
    }

    [Fact]
    public async Task ReimportKeepsExistingGameProfileAndRegistersItInTheSelector()
    {
        var first = Pack("A");
        var second = Pack("B");
        var service = new PortableSettingsService(new LauncherPaths(Path.GetDirectoryName(first)!));
        await service.LoadAsync();
        await service.SaveAsync(new LauncherSettings { GameDirectory = second, MaximumMemoryMb = 12288 });
        await service.SaveAsync(new LauncherSettings { GameDirectory = first });
        var settings = new SettingsViewModel(service);
        await settings.LoadCommand.ExecuteAsync(null);

        await settings.ActivateImportedGameAsync(new GameImportResult(second, "imported", false, "test"), CancellationToken.None);

        Assert.Equal(12288, settings.SavedSettings!.MaximumMemoryMb);
        Assert.Equal(second, settings.SavedSettings.GameDirectory);
        Assert.Equal("imported", settings.SavedSettings.PreferredVersionId);
        Assert.Equal(2, settings.GameDirectories.Count);
        Assert.Equal(second, settings.SelectedGameDirectory!.DirectoryPath);
    }

    [Fact]
    public async Task DamagedLibraryIsBackedUpAndAdjacentGameRemainsUsable()
    {
        var game = Pack("pack");
        var paths = new LauncherPaths(Path.GetDirectoryName(game)!);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "library.json"), "broken");

        var result = await new PortableSettingsService(paths).LoadAsync();

        Assert.Equal(game, result.Settings.GameDirectory);
        Assert.NotNull(result.BackupFilePath);
        Assert.Equal("broken", await File.ReadAllTextAsync(result.BackupFilePath));
    }

    [Fact]
    public void AdjacentJavaUsesReleaseVersionAndPrefersMatchingRuntime()
    {
        var game = Pack("pack");
        var parent = Path.GetDirectoryName(game)!;
        foreach (var (folder, version) in new[] { ("java8", "1.8.0_472"), ("java21", "21.0.9"), ("java25", "25.0.1") })
        {
            var java = Path.Combine(parent, folder);
            Directory.CreateDirectory(Path.Combine(java, "bin"));
            File.WriteAllText(Path.Combine(java, "bin", "javaw.exe"), "not executed");
            File.WriteAllText(Path.Combine(java, "release"), "JAVA_VERSION=\"" + version + "\"");
        }

        var candidates = LocalJavaLocator.FindCandidates(game, parent, 21);

        Assert.Equal(Path.Combine(parent, "java21", "bin", "javaw.exe"), candidates[0]);
        Assert.Equal(2, candidates.Count);
        Assert.DoesNotContain(candidates, path => path.Contains("java8"));
    }

    [Fact]
    public void AdjacentPclPackUsesExistingVersionModsWithoutImportStep()
    {
        var game = Pack("pack");
        var version = Path.Combine(game, "versions", "ForgePack");
        Directory.CreateDirectory(Path.Combine(version, "mods"));

        Assert.Equal(version, GameEnvironment.GetWorkingDirectory(new LauncherSettings { GameDirectory = game }, "ForgePack"));
    }

    public void Dispose()
    {
        var prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PIgHeAD.PortableTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
