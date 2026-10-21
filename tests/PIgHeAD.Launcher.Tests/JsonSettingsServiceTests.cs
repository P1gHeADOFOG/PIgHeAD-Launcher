using System.ComponentModel.DataAnnotations;
using System.IO;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Settings;

namespace PIgHeAD.Launcher.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.Launcher.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(directory, "profile", "settings.json");

    [Fact]
    public async Task MissingFileReturnsDefaultsWithoutCreatingFiles()
    {
        var result = await new JsonSettingsService(SettingsPath).LoadAsync();

        Assert.Equal(new LauncherSettings(), result.Settings);
        Assert.Null(result.BackupFilePath);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task SaveCreatesParentDirectoryAndRoundTripsUnicodePaths()
    {
        var service = new JsonSettingsService(SettingsPath);
        var settings = new LauncherSettings
        {
            GameDirectory = Path.Combine(directory, "我的世界"),
            JavaPath = Path.Combine(directory, "Java 21", "bin", "javaw.exe"),
            MaximumMemoryMb = 8192
        };

        await service.SaveAsync(settings);
        var result = await service.LoadAsync();

        Assert.Equal(settings, result.Settings);
        Assert.Null(result.BackupFilePath);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"maximumMemoryMb\":1}")]
    [InlineData("{\"gameDirectory\":null}")]
    [InlineData("{\"javaPath\":null}")]
    [InlineData("{\"maximumMemoryMb\":\"oops\"}")]
    public async Task CorruptFileIsBackedUpWithoutChangingOriginal(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await File.WriteAllTextAsync(SettingsPath, contents);

        var result = await new JsonSettingsService(SettingsPath).LoadAsync();

        Assert.Equal(new LauncherSettings(), result.Settings);
        Assert.NotNull(result.BackupFilePath);
        Assert.Equal(contents, await File.ReadAllTextAsync(result.BackupFilePath));
        Assert.Equal(contents, await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task SaveReplacesExistingFileWithoutLeavingTemporaryFiles()
    {
        var service = new JsonSettingsService(SettingsPath);
        await service.SaveAsync(new LauncherSettings());
        var updated = new LauncherSettings { MaximumMemoryMb = 12288 };

        await service.SaveAsync(updated);

        Assert.Equal(updated, (await service.LoadAsync()).Settings);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!));
    }

    [Fact]
    public async Task InvalidSavePreservesExistingFile()
    {
        var service = new JsonSettingsService(SettingsPath);
        await service.SaveAsync(new LauncherSettings());
        var original = await File.ReadAllTextAsync(SettingsPath);

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(
            new LauncherSettings { GameDirectory = "relative/path" }));

        Assert.Equal(original, await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task CanceledSavePreservesExistingFile()
    {
        var service = new JsonSettingsService(SettingsPath);
        await service.SaveAsync(new LauncherSettings());
        var original = await File.ReadAllTextAsync(SettingsPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(
            new LauncherSettings { MaximumMemoryMb = 8192 }, cancellation.Token));

        Assert.Equal(original, await File.ReadAllTextAsync(SettingsPath));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!));
    }

    [Fact]
    public async Task CanceledLoadDoesNotCreateDefaultConfiguration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new JsonSettingsService(SettingsPath).LoadAsync(cancellation.Token));

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task FailedReplacementCleansTemporaryFile()
    {
        Directory.CreateDirectory(SettingsPath);
        var service = new JsonSettingsService(SettingsPath);

        var exception = await Record.ExceptionAsync(() => service.SaveAsync(new LauncherSettings()));

        Assert.True(exception is IOException or UnauthorizedAccessException);

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!));
    }

    [Fact]
    public async Task ReadAccessErrorsAreNotTreatedAsMissingConfiguration()
    {
        Directory.CreateDirectory(SettingsPath);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new JsonSettingsService(SettingsPath).LoadAsync());
    }

    public void Dispose()
    {
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PIgHeAD.Launcher.Tests"));
        var fullPath = Path.GetFullPath(directory);
        if (!fullPath.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a directory outside the test root.");
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
