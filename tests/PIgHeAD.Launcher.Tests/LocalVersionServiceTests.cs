using System.IO;
using System.IO.Compression;
using System.Text.Json;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.Tests;

public sealed class LocalVersionServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.LocalVersionTests", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(directory, ".minecraft");
    private LauncherSettings Settings => new() { GameDirectory = Root };
    private static readonly GameVersionInfo Modded = new("custom-name", "release", true);

    [Theory]
    [InlineData("net.fabricmc:fabric-loader:0.16.10", "Fabric", "0.16.10")]
    [InlineData("org.quiltmc:quilt-loader:0.28.0", "Quilt", "0.28.0")]
    [InlineData("net.minecraftforge:forge:1.21.4-54.0.1", "Forge", "1.21.4-54.0.1")]
    [InlineData("net.neoforged:neoforge:21.4.1", "NeoForge", "21.4.1")]
    public async Task DetailsUseInheritedMetadataAndDetectLoaderFromLibraries(string library, string loader, string loaderVersion)
    {
        WriteBase();
        WriteModded(library);
        var details = await new LocalVersionService().GetDetailsAsync(Settings, Modded, CancellationToken.None);
        Assert.Equal("1.21.4", details.MinecraftVersion);
        Assert.Equal(21, details.JavaMajorVersion);
        Assert.Equal(loader, details.LoaderName);
        Assert.Equal(loaderVersion, details.LoaderVersion);
        Assert.True(details.SupportsMods);
        Assert.Equal(Root, details.WorkingDirectory);
        Assert.True(details.VersionSizeBytes > 0);
        Assert.NotNull(details.ReleaseTime);
    }

    [Fact]
    public async Task VanillaDoesNotExposeModsEvenWhenItsNameOrFolderSuggestsMods()
    {
        WriteVersion("fabric-name-only", """{"id":"fabric-name-only","type":"release","libraries":[]}""");
        WriteJar(Path.Combine(Root, "mods", "example.jar"), "fabric.mod.json", """{"id":"example","name":"Example","version":"1"}""");
        var service = new LocalVersionService();
        var details = await service.GetDetailsAsync(Settings, new("fabric-name-only", "release", true), CancellationToken.None);
        Assert.False(details.SupportsMods);
        Assert.Empty(await service.GetModsAsync(details, CancellationToken.None));
    }

    [Theory]
    [InlineData("shared")]
    [InlineData("isolated")]
    [InlineData("imported-versions")]
    [InlineData("imported-instances")]
    public async Task ModsAreReadFromTheActualSharedIsolatedOrImportedWorkingDirectory(string layout)
    {
        WriteBase();
        WriteModded();
        var settings = Settings with { IsolateVersions = layout == "isolated" };
        var working = layout switch
        {
            "shared" => Root,
            "imported-versions" => Path.Combine(Root, "versions", Modded.Id),
            _ => Path.Combine(Root, "instances", Modded.Id)
        };
        if (layout.StartsWith("imported", StringComparison.Ordinal))
            await ImportedGameLayout.WriteAsync(Root, new Dictionary<string, string> { [Modded.Id] = Path.GetRelativePath(Root, working) }, CancellationToken.None);
        WriteJar(Path.Combine(working, "mods", "example.jar"), "fabric.mod.json", """{"id":"example","name":"Example Mod","version":"2.3.4"}""");
        var service = new LocalVersionService();
        var details = await service.GetDetailsAsync(settings, Modded, CancellationToken.None);
        Assert.Equal(working, details.WorkingDirectory);
        var mod = Assert.Single(await service.GetModsAsync(details, CancellationToken.None));
        Assert.Equal("Example Mod", mod.Name);
        Assert.Equal("example", mod.ModId);
        Assert.Equal("2.3.4", mod.Version);
    }

    [Fact]
    public async Task CommonMetadataFormatsAndBrokenJarsAreHandledWithoutExecutingCode()
    {
        WriteBase();
        WriteModded();
        var mods = Path.Combine(Root, "mods");
        WriteJar(Path.Combine(mods, "quilt.jar"), "quilt.mod.json", """{"quilt_loader":{"id":"quilt-example","version":"1.2","metadata":{"name":"Quilt Example"}}}""");
        WriteJar(Path.Combine(mods, "forge.jar"), "META-INF/mods.toml", """
            modLoader="javafml"
            [[mods]]
            modId="forge-example"
            version="3.4"
            displayName="Forge Example"
            [[mods]]
            modId="second-example"
            version="5.6"
            displayName="Second Example"
            """);
        WriteJar(Path.Combine(mods, "neoforge.jar"), "META-INF/neoforge.mods.toml", """
            [[mods]]
            modId="neo-example"
            version="7.8"
            displayName="NeoForge Example"
            """);
        WriteJar(Path.Combine(mods, "legacy.jar"), "mcmod.info", """[{"modid":"legacy","name":"Legacy Example","version":"9"}]""");
        WriteJar(Path.Combine(mods, "unknown.jar"), "readme.txt", "No mod metadata");
        WriteJar(Path.Combine(mods, "invalid-metadata.jar"), "fabric.mod.json", "not json");
        File.WriteAllText(Path.Combine(mods, "broken.jar"), "not a ZIP");
        File.WriteAllText(Path.Combine(mods, "disabled.jar.disabled"), "not active");
        var service = new LocalVersionService();
        var details = await service.GetDetailsAsync(Settings, Modded, CancellationToken.None);
        var entries = await service.GetModsAsync(details, CancellationToken.None);
        Assert.Equal(8, entries.Count);
        Assert.Contains(entries, mod => mod.Name == "Quilt Example" && mod.Version == "1.2");
        Assert.Contains(entries, mod => mod.ModId == "second-example" && mod.Version == "5.6");
        Assert.Contains(entries, mod => mod.Name == "NeoForge Example");
        Assert.Contains(entries, mod => mod.ModId == "legacy");
        Assert.Contains(entries, mod => mod.FileName == "broken.jar" && mod.MetadataStatus.Contains("无法读取"));
        Assert.DoesNotContain(entries, mod => mod.FileName.EndsWith(".disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingParentAndOversizedModMetadataDoNotInventSuccessfulDetection()
    {
        WriteModded();
        WriteJar(Path.Combine(Root, "mods", "large.jar"), "fabric.mod.json", new string(' ', 1024 * 1024 + 1));
        var service = new LocalVersionService();
        var details = await service.GetDetailsAsync(Settings, Modded, CancellationToken.None);
        Assert.Equal("1.21.4", details.MinecraftVersion);
        Assert.Null(details.JavaMajorVersion);
        Assert.Contains("过大", Assert.Single(await service.GetModsAsync(details, CancellationToken.None)).MetadataStatus);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..")]
    [InlineData("C:\\outside")]
    public async Task InvalidVersionIdentifiersNeverReachTheRecycleOperation(string id)
    {
        WriteBase();
        var recycled = false;
        var service = new LocalVersionService(_ => recycled = true);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DeleteAsync(Settings, new(id, "release", true), CancellationToken.None));
        Assert.False(recycled);
    }

    [Fact]
    public async Task InheritedBaseVersionCannotBeDeletedBeforeItsDependent()
    {
        WriteBase();
        WriteModded();
        var recycled = false;
        var service = new LocalVersionService(_ => recycled = true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(Settings, new("1.21.4", "release", true), CancellationToken.None));
        Assert.Contains(Modded.Id, error.Message);
        Assert.False(recycled);
        Assert.True(Directory.Exists(Path.Combine(Root, "versions", "1.21.4")));
    }

    [Fact]
    public async Task RecyclingMovesOnlyTheChosenVersionAndRetainsSharedAndSeparateInstanceData()
    {
        WriteBase();
        WriteModded();
        var version = Path.Combine(Root, "versions", Modded.Id);
        var instance = Path.Combine(Root, "instances", Modded.Id, "saves");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "world.txt"), "keep");
        Directory.CreateDirectory(Path.Combine(version, "saves"));
        File.WriteAllText(Path.Combine(version, "saves", "inside.txt"), "move with version");
        foreach (var folder in new[] { "libraries", "assets", "saves", "mods" }) Directory.CreateDirectory(Path.Combine(Root, folder));
        var trash = Path.Combine(directory, "recycled-version");
        var service = new LocalVersionService(path => Directory.Move(path, trash));
        await service.DeleteAsync(Settings, Modded, CancellationToken.None);
        Assert.False(Directory.Exists(version));
        Assert.True(File.Exists(Path.Combine(trash, "saves", "inside.txt")));
        Assert.True(File.Exists(Path.Combine(instance, "world.txt")));
        foreach (var folder in new[] { "libraries", "assets", "saves", "mods" }) Assert.True(Directory.Exists(Path.Combine(Root, folder)));
        Assert.True(Directory.Exists(Path.Combine(Root, "versions", "1.21.4")));
    }

    [Fact]
    public async Task CancelledDeleteDoesNotMoveAnyFilesAndInheritanceCyclesAreRejected()
    {
        WriteBase();
        var recycled = false;
        var service = new LocalVersionService(_ => recycled = true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteAsync(Settings, new("1.21.4", "release", true), new(true)));
        Assert.False(recycled);
        WriteVersion("cycle", """{"id":"cycle","inheritsFrom":"cycle"}""");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.GetDetailsAsync(Settings, new("cycle", "release", true), CancellationToken.None));
    }

    private void WriteBase() => WriteVersion("1.21.4", """{"id":"1.21.4","type":"release","javaVersion":{"majorVersion":21},"releaseTime":"2025-01-01T00:00:00Z","libraries":[]}""");
    private void WriteModded(string library = "net.fabricmc:fabric-loader:0.16.10") => WriteVersion(Modded.Id,
        JsonSerializer.Serialize(new { id = Modded.Id, inheritsFrom = "1.21.4", type = "release", libraries = new[] { new { name = library } } }));
    private void WriteVersion(string id, string json)
    {
        var path = Path.Combine(Root, "versions", id);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, id + ".json"), json);
    }
    private static void WriteJar(string file, string entry, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
        writer.Write(content);
    }
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
