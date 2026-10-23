using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.Services.Minecraft;

/// <summary>Reads local metadata without loading JAR code or contacting a remote mod service.</summary>
public sealed class LocalVersionService(Action<string>? recycleDirectory = null) : ILocalVersionService
{
    private const long MaxMetadataBytes = 1024 * 1024;
    private const int MaxFiles = 100000;

    public Task<LocalVersionDetails> GetDetailsAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token) => Task.Run(() =>
    {
        var root = GameRootLocator.Resolve(settings.GameDirectory);
        var directory = VersionDirectory(root, version.Id);
        var metadata = ReadMetadataChain(root, version.Id, token);
        var minecraft = metadata[^1].Id;
        string loader = "", loaderVersion = "";
        int? java = null;
        DateTimeOffset? released = null;
        foreach (var (_, json) in metadata)
        {
            if (json.ValueKind != JsonValueKind.Object) continue;
            if (java is null && json.TryGetProperty("javaVersion", out var runtime) && runtime.ValueKind == JsonValueKind.Object
                && runtime.TryGetProperty("majorVersion", out var major) && major.ValueKind == JsonValueKind.Number
                && major.TryGetInt32(out var number)) java = number;
            if (released is null && DateTimeOffset.TryParse(Text(json, "releaseTime"), out var time)) released = time;
            if (loader.Length > 0 || !json.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array) continue;
            foreach (var library in libraries.EnumerateArray())
            {
                var parts = Text(library, "name").Split(':');
                if (parts.Length < 3) continue;
                var name = parts[0] + ":" + parts[1];
                loader = name switch
                {
                    "net.fabricmc:fabric-loader" => "Fabric",
                    "org.quiltmc:quilt-loader" => "Quilt",
                    "net.neoforged:neoforge" or "net.neoforged:forge" => "NeoForge",
                    "net.minecraftforge:forge" or "net.minecraftforge:minecraftforge" => "Forge",
                    _ => ""
                };
                if (loader.Length == 0) continue;
                loaderVersion = parts[2];
                break;
            }
        }
        var working = GameEnvironment.GetWorkingDirectory(settings, version.Id);
        // Imported layout records and paths are untrusted. Mods must stay inside the selected game root.
        var relative = Path.GetRelativePath(root, working);
        if (relative != ".") working = SafeGamePath.Resolve(root, relative);
        return new LocalVersionDetails(version.Id, minecraft, loader, loaderVersion, java,
            Text(metadata[0].Json, "type") is { Length: > 0 } type ? type : version.Type,
            root, directory, working, DirectorySize(directory, token), released, loader.Length > 0);
    }, token);

    public Task<IReadOnlyList<LocalModInfo>> GetModsAsync(LocalVersionDetails details, CancellationToken token) => Task.Run(() =>
    {
        if (!details.SupportsMods) return (IReadOnlyList<LocalModInfo>)[];
        var directory = SafeGamePath.Resolve(details.GameDirectory,
            Path.GetRelativePath(details.GameDirectory, Path.Combine(details.WorkingDirectory, "mods")));
        if (!Directory.Exists(directory)) return [];
        RejectLink(directory);
        var result = new List<LocalModInfo>();
        var fileCount = 0;
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (!Path.GetExtension(file).Equals(".jar", StringComparison.OrdinalIgnoreCase)) continue;
            if (++fileCount > 2000) throw new InvalidDataException("模组文件超过 2000 个，请在文件夹中查看。");
            RejectLink(file);
            result.AddRange(ReadMod(file));
        }
        return result;
    }, token);

    public Task DeleteAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token) => Task.Run(() =>
    {
        var root = GameRootLocator.Resolve(settings.GameDirectory);
        var directory = VersionDirectory(root, version.Id);
        if (!version.IsInstalled || !Directory.Exists(directory)) throw new InvalidOperationException("所选本地版本已不存在，请刷新列表。");
        for (var ancestor = directory; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor)) RejectLink(ancestor);
        foreach (var other in Directory.EnumerateDirectories(Path.Combine(root, "versions")))
        {
            token.ThrowIfCancellationRequested();
            if (other.Equals(directory, StringComparison.OrdinalIgnoreCase)) continue;
            RejectLink(other);
            var file = SafeGamePath.Resolve(root, "versions/" + Path.GetFileName(other) + "/" + Path.GetFileName(other) + ".json");
            if (!File.Exists(file)) continue;
            var json = ReadJson(file);
            if (Text(json, "inheritsFrom").Equals(version.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"版本 {Path.GetFileName(other)} 依赖此版本，请先删除依赖版本。");
        }
        // Inspect descendants too: never traverse a junction or delete a linked external directory.
        _ = DirectorySize(directory, token);
        token.ThrowIfCancellationRequested();
        if (recycleDirectory is { } recycle) recycle(directory);
        else FileSystem.DeleteDirectory(directory, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }, token);

    private static string VersionDirectory(string root, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || id.EndsWith('.') || id.EndsWith(' ')) throw new InvalidDataException("本地版本名称无效。");
        var directory = SafeGamePath.Resolve(root, "versions/" + id);
        if (Directory.Exists(directory)) RejectLink(directory);
        return directory;
    }

    private static List<(string Id, JsonElement Json)> ReadMetadataChain(string root, string id, CancellationToken token)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string Id, JsonElement Json)>();
        while (id.Length > 0)
        {
            token.ThrowIfCancellationRequested();
            if (!visited.Add(id) || visited.Count > 32) throw new InvalidDataException("版本继承关系存在循环或层级过多。");
            var directory = VersionDirectory(root, id);
            var file = SafeGamePath.Resolve(root, "versions/" + id + "/" + id + ".json");
            if (!File.Exists(file) && result.Count > 0) { result.Add((id, default)); break; }
            var json = ReadJson(file);
            result.Add((id, json));
            id = Text(json, "inheritsFrom");
        }
        return result;
    }

    private static JsonElement ReadJson(string file)
    {
        if (new FileInfo(file).Length > 8 * MaxMetadataBytes) throw new InvalidDataException("版本资料过大，无法读取。");
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("版本资料格式无效。");
        return document.RootElement.Clone();
    }

    private static long DirectorySize(string directory, CancellationToken token)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        long size = 0;
        var count = 0;
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            RejectLink(current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                token.ThrowIfCancellationRequested();
                if (++count > MaxFiles) throw new InvalidDataException("版本目录文件过多，请在文件夹中管理。");
                RejectLink(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else size = checked(size + new FileInfo(entry).Length);
            }
        }
        return size;
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("目录或文件包含链接，已停止操作。");
    }

    private static IReadOnlyList<LocalModInfo> ReadMod(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var fileName = Path.GetFileName(file);
        try
        {
            using var archive = ZipFile.OpenRead(file);
            foreach (var metadataName in new[] { "fabric.mod.json", "quilt.mod.json", "META-INF/neoforge.mods.toml", "META-INF/mods.toml", "mcmod.info" })
            {
                var entry = archive.GetEntry(metadataName);
                if (entry is null) continue;
                if (entry.Length > MaxMetadataBytes) return [new(name, "", "", fileName, "模组资料过大，仅显示文件名")];
                using var reader = new StreamReader(entry.Open());
                var text = reader.ReadToEnd();
                if (metadataName.EndsWith(".toml", StringComparison.Ordinal))
                {
                    var blocks = Regex.Split(text, @"(?m)^\s*\[\[mods\]\]\s*(?:#.*)?$").Skip(1).ToArray();
                    if (blocks.Length > 0) return blocks.Take(64).Select(block => new LocalModInfo(
                        Toml(block, "displayName") is { Length: > 0 } title ? title : name,
                        Toml(block, "modId"), ResolveModVersion(Toml(block, "version"), archive), fileName, "已读取本地资料")).ToArray();
                    continue;
                }
                using var document = JsonDocument.Parse(text);
                var json = document.RootElement;
                if (metadataName == "quilt.mod.json")
                {
                    var quilt = json.GetProperty("quilt_loader");
                    return [new(quilt.TryGetProperty("metadata", out var info) && Text(info, "name").Length > 0 ? Text(info, "name") : name,
                        Text(quilt, "id"), Text(quilt, "version"), fileName, "已读取本地资料")];
                }
                if (metadataName == "mcmod.info")
                {
                    var list = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("modList");
                    return list.EnumerateArray().Take(64).Select(mod => new LocalModInfo(
                        Text(mod, "name") is { Length: > 0 } title ? title : name, Text(mod, "modid"),
                        ResolveModVersion(Text(mod, "version"), archive), fileName, "已读取本地资料")).ToArray();
                }
                return [new(Text(json, "name") is { Length: > 0 } label ? label : name, Text(json, "id"), Text(json, "version"), fileName, "已读取本地资料")];
            }
            return [new(name, "", "", fileName, "未找到可识别的模组资料")];
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or KeyNotFoundException or RegexMatchTimeoutException)
        {
            return [new(name, "", "", fileName, "模组资料无法读取，仅显示文件名")];
        }
    }

    private static string ResolveModVersion(string value, ZipArchive archive)
    {
        if (!value.Contains("${", StringComparison.Ordinal)) return value;
        if (archive.GetEntry("META-INF/MANIFEST.MF") is { Length: <= MaxMetadataBytes } manifest)
        {
            using var reader = new StreamReader(manifest.Open());
            var match = Regex.Match(reader.ReadToEnd(), @"(?m)^Implementation-Version:\s*([^\r\n]+)");
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        return "未声明";
    }

    private static string Toml(string text, string key)
    {
        var match = Regex.Match(text, "(?m)^\\s*" + Regex.Escape(key) + "\\s*=\\s*[\"']([^\"'\\r\\n]*)[\"']", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string Text(JsonElement json, string property) => json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
