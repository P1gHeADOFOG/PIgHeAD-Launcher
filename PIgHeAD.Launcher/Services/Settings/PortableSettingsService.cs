using System.IO;
using System.Text.Json;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.Services.Settings;

/// <summary>Workspace history uses relocatable paths; game settings belong to each game's PHL.</summary>
public sealed class PortableSettingsService(LauncherPaths paths, string? legacySettingsPath = null) : IGameLibrarySettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly List<string> directories = [];
    private string LibraryFile => Path.Combine(paths.DataDirectory, "library.json");
    public string SettingsFilePath => Path.Combine(paths.DataDirectory, "launcher.json");
    public IReadOnlyList<string> GameDirectories => directories.AsReadOnly();

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        directories.Clear();
        Directory.CreateDirectory(paths.DataDirectory);
        var current = await new JsonSettingsService(SettingsFilePath).LoadAsync(cancellationToken);
        LibraryState? library = null;
        string? selected = null;
        string? backup = current.BackupFilePath;
        try
        {
            if (File.Exists(LibraryFile))
                library = JsonSerializer.Deserialize<LibraryState>(await File.ReadAllTextAsync(LibraryFile, cancellationToken));
            if (library is not null && (library.Directories is null || library.Directories.Any(string.IsNullOrWhiteSpace)))
                throw new JsonException("目录记录无效。");
            if (library is not null)
                foreach (var item in library.Directories!) AddDirectory(ResolveWorkspacePath(item));
            if (!string.IsNullOrWhiteSpace(library?.ActiveDirectory)) selected = ResolveWorkspacePath(library.ActiveDirectory);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            backup = LibraryFile + ".corrupt-" + Guid.NewGuid().ToString("N") + ".json";
            File.Copy(LibraryFile, backup);
            library = null;
            selected = null;
            directories.Clear();
        }

        if (selected is null || !Directory.Exists(selected))
            selected = GameRootLocator.Discover(paths.LauncherDirectory);
        if (selected is null && File.Exists(SettingsFilePath)) selected = current.Settings.GameDirectory;
        if (selected is null && legacySettingsPath is not null && File.Exists(legacySettingsPath))
        {
            current = await new JsonSettingsService(legacySettingsPath).LoadAsync(cancellationToken);
            // Carry over harmless preferences, but a new EXE location must not silently use an old pack.
            current = current with { Settings = current.Settings with { JavaPath = "", PreferredVersionId = "" } };
        }
        selected ??= Path.Combine(paths.LauncherDirectory, ".minecraft");
        var settings = await ReadGameAsync(selected, current.Settings, cancellationToken);
        await SaveAsync(settings, cancellationToken);
        return new SettingsLoadResult(settings, backup ?? current.BackupFilePath);
    }

    public Task<LauncherSettings> LoadGameAsync(string directory, CancellationToken cancellationToken)
    {
        var root = GameRootLocator.Resolve(directory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("游戏目录不存在，请重新接入移动后的目录。");
        return ReadGameAsync(root, new LauncherSettings(), cancellationToken);
    }

    private async Task<LauncherSettings> ReadGameAsync(string directory, LauncherSettings fallback, CancellationToken token)
    {
        var root = GameRootLocator.Resolve(directory);
        var file = Path.Combine(LauncherPaths.GetGameDataDirectory(root), "settings.json");
        var settings = File.Exists(file) ? (await new JsonSettingsService(file).LoadAsync(token)).Settings : fallback;
        var java = settings.JavaPath;
        var javaRecord = Path.Combine(LauncherPaths.GetGameDataDirectory(root), "java-path.json");
        if (File.Exists(javaRecord))
        {
            try
            {
                var relative = JsonSerializer.Deserialize<string>(await File.ReadAllTextAsync(javaRecord, token));
                if (!string.IsNullOrWhiteSpace(relative))
                    java = Path.GetFullPath(relative, Path.GetDirectoryName(LauncherPaths.GetGameDataDirectory(root))!);
            }
            catch (JsonException) { /* A stale optional Java hint must not hide the game profile. */ }
        }
        if (!string.IsNullOrWhiteSpace(java) && !File.Exists(java)) java = "";
        return settings with { GameDirectory = root, JavaPath = java };
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        var root = GameRootLocator.Resolve(settings.GameDirectory);
        settings = settings with { GameDirectory = root };
        var data = LauncherPaths.GetGameDataDirectory(root);
        await new JsonSettingsService(Path.Combine(data, "settings.json")).SaveAsync(settings, cancellationToken);
        var container = Path.GetDirectoryName(data)!;
        var relativeJava = settings.JavaPath.Length > 0 && IsWithin(settings.JavaPath, container)
            ? Path.GetRelativePath(container, settings.JavaPath) : null;
        await WriteJsonAsync(Path.Combine(data, "java-path.json"), relativeJava, cancellationToken);
        await new JsonSettingsService(SettingsFilePath).SaveAsync(settings, cancellationToken);
        var updated = directories.Append(root).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await WriteJsonAsync(LibraryFile, new LibraryState(StoreWorkspacePath(root), updated.Select(StoreWorkspacePath).ToArray()), cancellationToken);
        AddDirectory(root);
    }

    private void AddDirectory(string directory)
    {
        if (!directories.Contains(directory, StringComparer.OrdinalIgnoreCase)) directories.Add(directory);
    }
    private string ResolveWorkspacePath(string value) => Path.GetFullPath(value, paths.LauncherDirectory);
    private string StoreWorkspacePath(string value) => IsWithin(value, paths.LauncherDirectory)
        ? Path.GetRelativePath(paths.LauncherDirectory, value) : value;
    private static bool IsWithin(string value, string parent) => value.StartsWith(
        Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || value.Equals(parent, StringComparison.OrdinalIgnoreCase);

    private static async Task WriteJsonAsync<T>(string file, T value, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonOptions), token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, file, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private sealed record LibraryState(string? ActiveDirectory, string[]? Directories);
}
