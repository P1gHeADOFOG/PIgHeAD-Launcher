using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class GameImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.ImportTests", Guid.NewGuid().ToString("N"));
    private string Imports => Path.Combine(directory, "imports");
    private const string Metadata = """{"id":"1.21.4","type":"release","mainClass":"Test.Main","time":"2025-01-01T00:00:00Z","releaseTime":"2025-01-01T00:00:00Z","libraries":[],"arguments":{"game":[],"jvm":[]}}""";

    [Fact]
    public async Task ZipImportDetectsWrappedMinecraftAndPreservesIsolatedMods()
    {
        var zip = CreateZip("pack.zip", new Dictionary<string, string>
        {
            ["MyPack/.minecraft/versions/1.21.4/1.21.4.json"] = Metadata,
            ["MyPack/.minecraft/versions/1.21.4/mods/example.jar"] = "mod-bytes",
            ["MyPack/.minecraft/versions/1.21.4/saves/world/level.dat"] = "save-bytes"
        });
        var service = new GameImportService(new MinecraftGameService(), new FakeLoader(), Imports);

        var imported = await service.ImportAsync(zip, new LauncherSettings(), false, new InlineProgress(), CancellationToken.None);

        Assert.False(imported.FilesCompleted);
        Assert.Equal("1.21.4", imported.SelectedVersionId);
        Assert.True(File.Exists(zip));
        Assert.StartsWith(Imports, imported.GameDirectory);
        var working = GameEnvironment.GetWorkingDirectory(new LauncherSettings { GameDirectory = imported.GameDirectory }, "1.21.4");
        Assert.Equal(Path.Combine(imported.GameDirectory, "versions", "1.21.4"), working);
        Assert.Equal("mod-bytes", await File.ReadAllTextAsync(Path.Combine(working, "mods", "example.jar")));
        Assert.Equal("save-bytes", await File.ReadAllTextAsync(Path.Combine(working, "saves", "world", "level.dat")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(Imports), path => Path.GetFileName(path).StartsWith(".pending-"));
    }

    [Fact]
    public async Task DirectoryImportResolvesMinecraftAndNeverMovesUserFiles()
    {
        var source = Path.Combine(directory, "existing");
        var root = Path.Combine(source, ".minecraft");
        Directory.CreateDirectory(Path.Combine(root, "versions", "1.21.4"));
        await File.WriteAllTextAsync(Path.Combine(root, "versions", "1.21.4", "1.21.4.json"), Metadata);
        var service = new GameImportService(new MinecraftGameService(), new FakeLoader(), Imports);

        var imported = await service.ImportAsync(source, new LauncherSettings(), false, new InlineProgress(), CancellationToken.None);

        Assert.Equal(root, imported.GameDirectory);
        Assert.Equal(root, GameRootLocator.Discover(source));
        Assert.True(File.Exists(Path.Combine(root, "versions", "1.21.4", "1.21.4.json")));
        Assert.False(Directory.Exists(Imports));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("mods/../../escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("mods/test.jar:stream")]
    [InlineData("/absolute.txt")]
    public async Task UnsafeArchivePathsNeverEscapeAndDoNotPublishImport(string entry)
    {
        var zip = CreateZip("unsafe.zip", new Dictionary<string, string> { [entry] = "unsafe" });
        var service = new GameImportService(new MinecraftGameService(), new FakeLoader(), Imports);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(zip, new LauncherSettings(), false, new InlineProgress(), CancellationToken.None));

        Assert.Empty(Directory.EnumerateDirectories(Imports));
        Assert.False(File.Exists(Path.Combine(directory, "escape.txt")));
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public async Task ArchiveRejectsSymlinksAndCaseCollisionsBeforeWritingFiles()
    {
        Directory.CreateDirectory(directory);
        var zip = Path.Combine(directory, "links.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("link");
            entry.ExternalAttributes = unchecked((int)0xA1FF0000);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => SafeZipExtractor.ExtractAsync(zip, Path.Combine(directory, "out"), new InlineProgress(), CancellationToken.None));
        var duplicate = CreateZip("duplicates.zip", new Dictionary<string, string> { ["Mods/Item.jar"] = "a", ["mods/item.jar"] = "b" });
        await Assert.ThrowsAsync<InvalidDataException>(() => SafeZipExtractor.ExtractAsync(duplicate, Path.Combine(directory, "out"), new InlineProgress(), CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(directory, "out")));
    }

    [Fact]
    public async Task RepairFailureOrCancellationDoesNotPublishPartialArchive()
    {
        var zip = CreateZip("repair.zip", new Dictionary<string, string> { [".minecraft/versions/1.21.4/1.21.4.json"] = Metadata });
        var games = new LauncherTestContext.FakeGameService { InstallError = new IOException("repair failed") };
        var service = new GameImportService(games, new FakeLoader(), Imports);
        await Assert.ThrowsAsync<IOException>(() => service.ImportAsync(zip, new LauncherSettings(), true, new InlineProgress(), CancellationToken.None));
        Assert.Equal(1, games.RepairCalls);
        Assert.Empty(Directory.EnumerateDirectories(Imports));

        var files = Enumerable.Range(0, 70).ToDictionary(i => $"files/{i}.txt", _ => "content");
        var cancelledZip = CreateZip("cancel.zip", files);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportAsync(cancelledZip, new LauncherSettings(), false,
            new InlineProgress(_ => cancellation.Cancel()), cancellation.Token));
        Assert.Empty(Directory.EnumerateDirectories(Imports));
    }

    [Fact]
    public async Task MrpackInstallsPinnedLoaderDownloadsAndHashesModsAndLayersClientOverrides()
    {
        var bytes = Encoding.UTF8.GetBytes("verified-mod-bytes");
        using var handler = new DownloadHandler(bytes);
        using var http = new HttpClient(handler);
        var index = CreateIndex(bytes);
        var zip = CreateZip("pack.mrpack", new Dictionary<string, string>
        {
            ["modrinth.index.json"] = index,
            ["overrides/config/test.txt"] = "common",
            ["client-overrides/config/test.txt"] = "client",
            ["server-overrides/config/server.txt"] = "server"
        });
        var games = new LauncherTestContext.FakeGameService();
        var loaders = new FakeLoader();
        var service = new GameImportService(games, loaders, Imports, new ModrinthPackInstaller(http));

        var imported = await service.ImportAsync(zip, new LauncherSettings { IsolateVersions = true }, true, new InlineProgress(), CancellationToken.None);

        Assert.True(imported.FilesCompleted);
        Assert.Equal(DownloadComponent.Fabric, loaders.Component);
        Assert.Equal("0.16.10", loaders.BuildId);
        Assert.Equal("fabric-test", imported.SelectedVersionId);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(imported.GameDirectory, "mods", "example.jar")));
        Assert.Equal("client", await File.ReadAllTextAsync(Path.Combine(imported.GameDirectory, "config", "test.txt")));
        Assert.False(File.Exists(Path.Combine(imported.GameDirectory, "config", "server.txt")));
        Assert.Equal(1, handler.Calls);
        await File.WriteAllTextAsync(Path.Combine(imported.GameDirectory, "mods", "example.jar"), "corrupted");
        await service.RepairAsync(new LauncherSettings { GameDirectory = imported.GameDirectory },
            new GameVersionInfo("fabric-test", "custom", true), new InlineProgress(), CancellationToken.None);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(imported.GameDirectory, "mods", "example.jar")));
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1, games.RepairCalls);
    }

    [Fact]
    public async Task HashMismatchPreservesOriginalAndVerifiedFileSkipsDownload()
    {
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes("correct");
        var index = Path.Combine(directory, "index.json");
        await File.WriteAllTextAsync(index, CreateIndex(bytes));
        var instance = Path.Combine(directory, "instance");
        Directory.CreateDirectory(Path.Combine(instance, "mods"));
        var path = Path.Combine(instance, "mods", "example.jar");
        await File.WriteAllTextAsync(path, "original");
        using var handler = new DownloadHandler(Encoding.UTF8.GetBytes("badbad!"));
        using var http = new HttpClient(handler);
        var installer = new ModrinthPackInstaller(http);
        await Assert.ThrowsAsync<IOException>(() => installer.CompleteFilesAsync(index, instance, new InlineProgress(), CancellationToken.None));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        await File.WriteAllBytesAsync(path, bytes);
        await installer.CompleteFilesAsync(index, instance, new InlineProgress(), CancellationToken.None);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ImportRegistrationPreservesDraftAndFailedImportKeepsActiveSettings()
    {
        var store = new JsonSettingsService(Path.Combine(directory, "settings.json"));
        var downloads = new DownloadsViewModel();
        var settings = new SettingsViewModel(store, operations: downloads);
        await settings.LoadCommand.ExecuteAsync(null);
        settings.ExtraJvmArguments = "-Ddraft=unsaved";
        var original = settings.SavedSettings;
        var games = new LauncherTestContext.FakeGameService();
        var service = new StubImport { Error = new IOException("broken zip") };
        var versions = new VersionsViewModel(games, settings, downloads, service);
        await versions.ImportPathCommand.ExecuteAsync("bad.zip");
        Assert.Same(original, settings.SavedSettings);
        Assert.True(downloads.IsIdle);
        Assert.Contains("导入未完成", versions.Status);
        service.Error = null;
        await versions.ImportPathCommand.ExecuteAsync("good.zip");
        Assert.Equal(@"C:\imported-game", settings.SavedSettings!.GameDirectory);
        Assert.Equal("1.21.4", (await store.LoadAsync()).Settings.PreferredVersionId);
        Assert.Equal("-Ddraft=unsaved", settings.ExtraJvmArguments);
        Assert.Equal("", settings.SavedSettings.ExtraJvmArguments);
        Assert.Equal("1.21.4", versions.SelectedGameVersion!.Id);
        await versions.RepairSelectedCommand.ExecuteAsync(null);
        Assert.Equal(1, service.RepairCalls);
    }

    private string CreateZip(string name, IReadOnlyDictionary<string, string> entries)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Key).Open(), new UTF8Encoding(false));
            writer.Write(entry.Value);
        }
        return path;
    }
    private static string CreateIndex(byte[] bytes) => JsonSerializer.Serialize(new
    {
        formatVersion = 1, game = "minecraft", versionId = "1", name = "test pack",
        dependencies = new Dictionary<string, string> { ["minecraft"] = "1.21.4", ["fabric-loader"] = "0.16.10" },
        files = new[] { new { path = "mods/example.jar", hashes = new { sha1 = Convert.ToHexString(SHA1.HashData(bytes)), sha512 = Convert.ToHexString(SHA512.HashData(bytes)) },
            downloads = new[] { "https://cdn.modrinth.com/data/test/example.jar" }, fileSize = bytes.Length } }
    });
    private sealed class InlineProgress(Action<GameProgress>? action = null) : IProgress<GameProgress>
    {
        public void Report(GameProgress value) => action?.Invoke(value);
    }
    private sealed class DownloadHandler(byte[] bytes) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request });
        }
    }
    private sealed class FakeLoader : ILoaderService
    {
        public DownloadComponent Component { get; private set; }
        public string? BuildId { get; private set; }
        public Task<IReadOnlyList<LoaderBuild>> GetBuildsAsync(DownloadComponent component, string minecraft, CancellationToken token) => throw new NotSupportedException();
        public Task<string> InstallAsync(LauncherSettings settings, DownloadComponent component, string minecraft, LoaderBuild build, IProgress<GameProgress> progress, CancellationToken token)
        { Component = component; BuildId = build.Id; return Task.FromResult("fabric-test"); }
        public Task RunOptiFineInstallerAsync(LauncherSettings settings, string path, IProgress<GameProgress> progress, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class StubImport : IGameImportService
    {
        public Exception? Error { get; set; }
        public int RepairCalls { get; private set; }
        public Task<GameImportResult> ImportAsync(string source, LauncherSettings settings, bool completeFiles, IProgress<GameProgress> progress, CancellationToken token) =>
            Error is null ? Task.FromResult(new GameImportResult(@"C:\imported-game", "1.21.4", true, "imported")) : Task.FromException<GameImportResult>(Error);
        public Task RepairAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken token)
        { RepairCalls++; return Task.CompletedTask; }
    }
    public void Dispose()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PIgHeAD.ImportTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
