using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installers;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.Services.Minecraft;

public sealed class ModLoaderService : ILoaderService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private readonly HttpClient http;
    public ModLoaderService(HttpClient? httpClient = null) => http = httpClient ?? Http;

    public Task<IReadOnlyList<LoaderBuild>> GetBuildsAsync(DownloadComponent component, string minecraftVersion,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        ValidateIdentifier(minecraftVersion);
        if (component == DownloadComponent.Forge)
        {
            var versions = await new ForgeVersionLoader(http).GetForgeVersions(minecraftVersion).WaitAsync(cancellationToken);
            return (IReadOnlyList<LoaderBuild>)versions.OrderByDescending(v => v.IsRecommendedVersion)
                .Select(v => new LoaderBuild(v.ForgeVersionName, v.ForgeVersionName
                    + (v.IsRecommendedVersion ? " · 推荐" : v.IsLatestVersion ? " · 最新" : ""))).ToArray();
        }
        if (component == DownloadComponent.NeoForge)
        {
            var versions = await new NeoForgeVersionLoader(http).GetNeoForgeVersions(minecraftVersion).WaitAsync(cancellationToken);
            return versions.Select(v => new LoaderBuild(v.NeoForgeVersionName, v.NeoForgeVersionName)).ToArray();
        }
        if (component is DownloadComponent.Fabric or DownloadComponent.Quilt)
        {
            using var response = await http.GetAsync($"{GetMetaBase(component)}/versions/loader/{Uri.EscapeDataString(minecraftVersion)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.EnumerateArray().Select(entry => entry.GetProperty("loader"))
                .Select(loader => new { Id = loader.GetProperty("version").GetString()!,
                    Stable = loader.TryGetProperty("stable", out var stable) ? stable.GetBoolean()
                        : !loader.GetProperty("version").GetString()!.Contains('-') })
                .DistinctBy(b => b.Id).OrderByDescending(b => b.Stable)
                .ThenByDescending(b => Version.TryParse(b.Id.Split('-')[0], out var parsed) ? parsed : new Version(0, 0))
                .Select(b => new LoaderBuild(b.Id, b.Id + (b.Stable ? " · 稳定" : " · 预览"))).ToArray();
        }
        return [];
    }, cancellationToken);

    public Task<string> InstallAsync(LauncherSettings settings, DownloadComponent component, string minecraftVersion,
        LoaderBuild build, IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        settings.Validate();
        ValidateIdentifier(minecraftVersion);
        ValidateIdentifier(build.Id);
        var launcher = MinecraftGameService.CreateLauncher(settings.GameDirectory, online: true);
        var fileProgress = new InlineProgress<InstallerProgressChangedEventArgs>(p =>
            progress.Report(new GameProgress($"{component} · {p.Name} · {p.ProgressedTasks}/{p.TotalTasks}",
                p.TotalTasks > 0 ? Math.Clamp(100d * p.ProgressedTasks / p.TotalTasks, 0, 100) : null)));
        var options = new ForgeInstallOptions
        {
            JavaPath = string.IsNullOrWhiteSpace(settings.JavaPath) ? null : settings.JavaPath,
            CancellationToken = cancellationToken, FileProgress = fileProgress,
            SkipIfAlreadyInstalled = false
        };
        progress.Report(new GameProgress($"正在安装 {component} {build.Id}…"));
        if (component == DownloadComponent.Forge)
            return await new ForgeInstaller(launcher, http).Install(minecraftVersion, build.Id, options);
        if (component == DownloadComponent.NeoForge)
            return await new CmlLib.Core.Installer.NeoForge.NeoForgeInstaller(launcher, http).Install(minecraftVersion, build.Id, options);
        if (component is not (DownloadComponent.Fabric or DownloadComponent.Quilt))
            throw new InvalidOperationException("此组件不支持自动安装。");

        using var response = await http.GetAsync($"{GetMetaBase(component)}/versions/loader/{Uri.EscapeDataString(minecraftVersion)}/{Uri.EscapeDataString(build.Id)}/profile/json", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var profile = JsonDocument.Parse(json);
        var id = profile.RootElement.GetProperty("id").GetString() ?? throw new InvalidDataException("加载器资料缺少版本 ID。");
        ValidateIdentifier(id);
        if (profile.RootElement.GetProperty("inheritsFrom").GetString() != minecraftVersion)
            throw new InvalidDataException("加载器资料与选定 Minecraft 版本不匹配。");
        var directory = Path.Combine(settings.GameDirectory, "versions", id);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, id + ".json");
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        // A fresh launcher reads the profile and installs inherited Minecraft / loader dependencies.
        launcher = MinecraftGameService.CreateLauncher(settings.GameDirectory, online: true);
        await launcher.InstallAsync(id, fileProgress, null, cancellationToken);
        return id;
    }, cancellationToken);

    public Task RunOptiFineInstallerAsync(LauncherSettings settings, string jarPath,
        IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        settings.Validate();
        if (!Path.IsPathFullyQualified(jarPath) || !File.Exists(jarPath)
            || !Path.GetFileName(jarPath).StartsWith("OptiFine_", StringComparison.OrdinalIgnoreCase)
            || !jarPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请选择从 OptiFine 官网取得的 OptiFine_*.jar 安装器。");
        if (string.IsNullOrWhiteSpace(settings.JavaPath))
            throw new InvalidOperationException("运行外部安装器前，请在 Settings 选择 Java 可执行文件。");
        await JavaRuntimeProbe.CheckAsync(settings.JavaPath, 8, cancellationToken);
        var start = new ProcessStartInfo(settings.JavaPath) { UseShellExecute = false, WorkingDirectory = settings.GameDirectory };
        start.ArgumentList.Add("-jar"); start.ArgumentList.Add(jarPath);
        progress.Report(new GameProgress("已打开 OptiFine 官方安装器。请在其窗口中核对安装目录并完成安装，然后关闭该窗口；这不是自动安装。"));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法打开 OptiFine 安装器。");
        // Once opened, keep the operation lock until the user's external installer exits.
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"OptiFine 安装器退出码 {process.ExitCode}。");
    }, cancellationToken);

    private static string GetMetaBase(DownloadComponent component) => component == DownloadComponent.Fabric
        ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3";

    public static void ValidateIdentifier(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.Length > 180
            || id.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '-' or '_')))
            throw new InvalidDataException("远程版本名称不合法，已停止文件写入。");
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
