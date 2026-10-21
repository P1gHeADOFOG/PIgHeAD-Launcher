using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Minecraft;

public sealed record PackDependency(string MinecraftVersion, DownloadComponent? Loader, string? LoaderVersion);

public sealed class ModrinthPackInstaller
{
    private static readonly HttpClient Http = new(new HttpClientHandler { MaxAutomaticRedirections = 5 }) { Timeout = TimeSpan.FromMinutes(10) };
    private readonly HttpClient http;
    public ModrinthPackInstaller(HttpClient? http = null) => this.http = http ?? Http;
    public const string SavedIndexName = ".pighead-modrinth.index.json";

    public static PackDependency ReadDependencies(string indexPath)
    {
        using var document = ReadIndex(indexPath);
        var dependencies = document.RootElement.GetProperty("dependencies");
        var minecraft = dependencies.GetProperty("minecraft").GetString() ?? throw new InvalidDataException("整合包未指定 Minecraft 版本。");
        ModLoaderService.ValidateIdentifier(minecraft);
        DownloadComponent? loader = null;
        string? loaderVersion = null;
        foreach (var dependency in dependencies.EnumerateObject())
        {
            if (dependency.Name == "minecraft") continue;
            var component = dependency.Name switch
            {
                "forge" => DownloadComponent.Forge, "neoforge" => DownloadComponent.NeoForge,
                "fabric-loader" => DownloadComponent.Fabric, "quilt-loader" => DownloadComponent.Quilt,
                _ => throw new InvalidDataException($"暂不支持此整合包的依赖：{dependency.Name}。")
            };
            if (loader is not null) throw new InvalidDataException("整合包声明了多个加载器，无法自动安装。");
            loader = component;
            loaderVersion = dependency.Value.GetString() ?? throw new InvalidDataException("加载器版本为空。");
            ModLoaderService.ValidateIdentifier(loaderVersion);
        }
        return new PackDependency(minecraft, loader, loaderVersion);
    }

    public async Task CompleteFilesAsync(string indexPath, string instanceDirectory, IProgress<GameProgress> progress, CancellationToken cancellationToken)
    {
        using var document = ReadIndex(indexPath);
        var files = document.RootElement.GetProperty("files").EnumerateArray().ToArray();
        if (files.Length > 100000) throw new InvalidDataException("整合包清单过大。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalSize = 0;
        // Validate every destination before any network download or file replacement.
        foreach (var file in files)
        {
            var relative = file.GetProperty("path").GetString()!;
            if (relative.Replace('\\', '/') is SavedIndexName or ".pighead-import.json")
                throw new InvalidDataException("整合包清单不能替换启动器的导入记录。");
            var path = SafeGamePath.Resolve(instanceDirectory, relative);
            if (!seen.Add(path)) throw new InvalidDataException("整合包清单含重复文件路径。");
            var size = file.GetProperty("fileSize").GetInt64();
            if (size < 0 || size > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("整合包文件大小不受支持。");
            totalSize = checked(totalSize + size);
            if (totalSize > SafeZipExtractor.MaximumExpandedBytes) throw new InvalidDataException("清单下载超过 16 GiB。");
        }
        var completed = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.TryGetProperty("env", out var env) && env.TryGetProperty("client", out var client)
                && client.GetString() == "unsupported") { completed++; continue; }
            var relative = file.GetProperty("path").GetString()!;
            var target = SafeGamePath.Resolve(instanceDirectory, relative);
            var size = file.GetProperty("fileSize").GetInt64();
            var hashes = file.GetProperty("hashes");
            var sha1 = hashes.GetProperty("sha1").GetString()!;
            var sha512 = hashes.GetProperty("sha512").GetString()!;
            if (!IsHex(sha1, 40) || !IsHex(sha512, 128)) throw new InvalidDataException("整合包缺少有效的 SHA-1 / SHA-512 校验值。");
            progress.Report(new GameProgress($"校验 / 补全模组 {completed + 1}/{files.Length} · {relative}",
                files.Length == 0 ? 100 : 100d * completed / files.Length));
            if (!await MatchesAsync(target, size, sha1, sha512, cancellationToken))
            {
                var urls = file.GetProperty("downloads").EnumerateArray().Select(value => value.GetString()!).ToArray();
                if (urls.Length == 0) throw new InvalidDataException($"模组没有下载地址：{relative}");
                Exception? failure = null;
                foreach (var url in urls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await DownloadAsync(url, target, size, sha1, sha512, cancellationToken);
                        failure = null;
                        break;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
                    {
                        failure = exception;
                    }
                }
                if (failure is not null) throw new IOException($"无法取得或校验模组 {relative}，请检查来源与网络后重试。", failure);
            }
            completed++;
        }
    }

    private async Task DownloadAsync(string address, string target, long size, string sha1, string sha512, CancellationToken cancellationToken)
    {
        ValidateDownloadUri(new Uri(address));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var response = await http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            ValidateDownloadUri(response.RequestMessage?.RequestUri ?? new Uri(address));
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long written = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    written += count;
                    if (written > size) throw new InvalidDataException("下载大小超出整合包清单。");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
            }
            if (!await MatchesAsync(temporary, size, sha1, sha512, cancellationToken)) throw new InvalidDataException("模组文件哈希校验失败。");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task<bool> MatchesAsync(string path, long size, string sha1, string sha512, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != size) return false;
        await using var stream = File.OpenRead(path);
        var actual512 = Convert.ToHexString(await SHA512.HashDataAsync(stream, cancellationToken));
        if (!actual512.Equals(sha512, StringComparison.OrdinalIgnoreCase)) return false;
        stream.Position = 0;
        return Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken)).Equals(sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadIndex(string path)
    {
        if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException("整合包索引超过 5 MiB。");
        using var stream = File.OpenRead(path);
        var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("formatVersion").GetInt32() != 1
            || document.RootElement.GetProperty("game").GetString() != "minecraft")
        {
            document.Dispose();
            throw new InvalidDataException("不支持此 Modrinth 整合包格式版本。");
        }
        return document;
    }

    private static bool IsHex(string? value, int length) => value?.Length == length && value.All(Uri.IsHexDigit);
    private static void ValidateDownloadUri(Uri uri)
    {
        string[] allowed = ["cdn.modrinth.com", "github.com", "raw.githubusercontent.com", "gitlab.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];
        if (!uri.IsAbsoluteUri || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)
            || !allowed.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("整合包使用了当前未支持的下载来源，仅支持 Modrinth CDN、GitHub 与 GitLab 的 HTTPS 文件。");
    }
}
