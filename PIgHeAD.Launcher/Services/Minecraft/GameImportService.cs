using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.Services.Minecraft;

public sealed class GameImportService(IGameService games, ILoaderService loaders,
    string? importDirectory = null, ModrinthPackInstaller? modrinth = null) : IGameImportService
{
    private readonly string importDirectory = Path.GetFullPath(importDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIgHeAD", "Launcher", "imports"));
    private readonly ModrinthPackInstaller modrinth = modrinth ?? new();

    public Task<GameImportResult> ImportAsync(string source, LauncherSettings settings, bool completeFiles,
        IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        if (Directory.Exists(source))
        {
            var root = GameRootLocator.FindInExtractedPack(source) ?? throw new InvalidDataException("所选目录及其 .minecraft 中没有找到版本目录。");
            var local = await games.GetVersionsAsync(root, false, cancellationToken);
            if (local.Count == 0) throw new InvalidDataException("没有发现有效的版本 JSON，请检查游戏根目录。");
            var selected = SelectVersion(root, local);
            await ImportedGameLayout.WriteAsync(root, BuildLayouts(root, local), cancellationToken);
            if (completeFiles) await RepairAsync(settings with { GameDirectory = root }, selected, progress, cancellationToken);
            return new GameImportResult(root, selected.Id, completeFiles, completeFiles
                ? "已接入游戏目录并补全所选版本，原文件未移动。" : "已接入现有游戏目录，尚未校验补全，原文件未移动。");
        }
        if (!File.Exists(source) || (Path.GetExtension(source).ToLowerInvariant() is not (".zip" or ".mrpack")))
            throw new InvalidDataException("请选择游戏目录、ZIP 整合包或 Modrinth .mrpack 文件。");
        Directory.CreateDirectory(importDirectory);
        var id = "pack-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var stage = Path.Combine(importDirectory, ".pending-" + id);
        var destination = Path.Combine(importDirectory, id);
        Directory.CreateDirectory(stage);
        try
        {
            await SafeZipExtractor.ExtractAsync(source, stage, progress, cancellationToken);
            var index = Path.Combine(stage, "modrinth.index.json");
            string root;
            string selected;
            IReadOnlyList<GameVersionInfo> local;
            if (File.Exists(index))
            {
                if (!completeFiles) throw new InvalidDataException(".mrpack 需要联网安装游戏与模组，请勾选“导入后校验并补全文件”后重试。");
                var dependency = ModrinthPackInstaller.ReadDependencies(index);
                root = Path.Combine(stage, ".minecraft");
                Directory.CreateDirectory(root);
                var savedIndex = Path.Combine(root, ModrinthPackInstaller.SavedIndexName);
                SaveClientIndex(index, savedIndex, stage);
                var snapshot = settings with { GameDirectory = root };
                var minecraft = new GameVersionInfo(dependency.MinecraftVersion, "release", false);
                await games.InstallAsync(snapshot, minecraft, progress, cancellationToken);
                selected = dependency.Loader is { } component
                    ? await loaders.InstallAsync(snapshot, component, dependency.MinecraftVersion,
                        new LoaderBuild(dependency.LoaderVersion!, dependency.LoaderVersion!), progress, cancellationToken)
                    : dependency.MinecraftVersion;
                local = await games.GetVersionsAsync(root, false, cancellationToken);
                if (completeFiles) await modrinth.CompleteFilesAsync(savedIndex, root, progress, cancellationToken);
                CopyOverrides(Path.Combine(stage, "overrides"), root, cancellationToken);
                CopyOverrides(Path.Combine(stage, "client-overrides"), root, cancellationToken);
                // Overrides cannot replace the launcher's retained repair manifest.
                SaveClientIndex(index, savedIndex, stage);
            }
            else
            {
                if (Path.GetExtension(source).Equals(".mrpack", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(".mrpack 文件缺少根目录的 modrinth.index.json。");
                root = GameRootLocator.FindInExtractedPack(stage) ?? throw new InvalidDataException(
                    "ZIP 中没有找到 versions / .minecraft。仅含 CurseForge manifest.json 的导出包暂不支持，请使用完整 ZIP 或 .mrpack。");
                local = await games.GetVersionsAsync(root, false, cancellationToken);
                if (local.Count == 0) throw new InvalidDataException("整合包没有有效的 Minecraft 版本资料。");
                selected = SelectVersion(root, local).Id;
                if (completeFiles)
                    foreach (var version in local) await games.RepairAsync(settings with { GameDirectory = root }, version, progress, cancellationToken);
            }
            var layouts = BuildLayouts(root, local);
            layouts.TryAdd(selected, ".");
            await ImportedGameLayout.WriteAsync(root, layouts, cancellationToken);
            var relativeRoot = Path.GetRelativePath(stage, root);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(stage, destination);
            root = relativeRoot == "." ? destination : SafeGamePath.Resolve(destination, relativeRoot);
            return new GameImportResult(root, selected, completeFiles,
                completeFiles ? "整合包已导入，已校验补全游戏依赖及可用清单文件。" : "整合包已导入，尚未补全文件；可使用“修复游戏”继续。");
        }
        catch
        {
            // Only remove this service's unique, verified staging directory; never the user's archive or game.
            if (Directory.Exists(stage) && Path.GetFullPath(stage).StartsWith(importDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(stage, recursive: true); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { /* Keep the original import error. */ }
            }
            throw;
        }
    }, cancellationToken);

    public async Task RepairAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken cancellationToken)
    {
        var root = GameRootLocator.Resolve(settings.GameDirectory);
        var index = Path.Combine(root, ModrinthPackInstaller.SavedIndexName);
        if (File.Exists(index))
            await modrinth.CompleteFilesAsync(index, ImportedGameLayout.GetWorkingDirectory(root, version.Id) ?? root, progress, cancellationToken);
        await games.RepairAsync(settings with { GameDirectory = root }, version, progress, cancellationToken);
    }

    private static Dictionary<string, string> BuildLayouts(string root, IReadOnlyList<GameVersionInfo> versions) =>
        versions.ToDictionary(version => version.Id, version =>
        {
            var recorded = ImportedGameLayout.GetWorkingDirectory(root, version.Id);
            if (recorded is not null) return Path.GetRelativePath(root, recorded).Replace('\\', '/');
            foreach (var relative in new[] { "versions/" + version.Id, "instances/" + version.Id })
            {
                var isolated = SafeGamePath.Resolve(root, relative);
                if (new[] { "mods", "config", "saves", "resourcepacks" }.Any(name => Directory.Exists(Path.Combine(isolated, name))))
                    return relative;
            }
            return ".";
        }, StringComparer.OrdinalIgnoreCase);

    private static GameVersionInfo SelectVersion(string root, IReadOnlyList<GameVersionInfo> versions) =>
        versions.FirstOrDefault(version =>
        {
            var path = SafeGamePath.Resolve(root, "versions/" + version.Id + "/" + version.Id + ".json");
            if (!File.Exists(path) || new FileInfo(path).Length > 5 * 1024 * 1024) return false;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                return json.RootElement.TryGetProperty("inheritsFrom", out _);
            }
            catch (JsonException) { return false; }
        }) ?? versions[0];

    private static void SaveClientIndex(string index, string destination, string stage)
    {
        var overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in new[] { "overrides", "client-overrides" })
        {
            var directory = Path.Combine(stage, folder);
            if (Directory.Exists(directory))
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    overridden.Add(Path.GetRelativePath(directory, file).Replace('\\', '/'));
        }
        var json = JsonNode.Parse(File.ReadAllText(index)) ?? throw new InvalidDataException("整合包索引为空。");
        var files = json["files"]?.AsArray() ?? throw new InvalidDataException("整合包缺少文件列表。");
        for (var i = files.Count - 1; i >= 0; i--)
            if (overridden.Contains(files[i]?["path"]?.GetValue<string>()?.Replace('\\', '/') ?? "")) files.RemoveAt(i);
        File.WriteAllText(destination, json.ToJsonString());
    }

    private static void CopyOverrides(string source, string target, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = SafeGamePath.Resolve(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
