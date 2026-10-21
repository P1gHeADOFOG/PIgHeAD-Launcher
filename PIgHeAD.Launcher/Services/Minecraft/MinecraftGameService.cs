using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Version;
using CmlLib.Core.VersionLoader;
using CmlLib.Core.VersionMetadata;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.Services.Minecraft;

public sealed class MinecraftGameService(string? gameLogDirectory = null) : IGameService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    public Task<IReadOnlyList<GameVersionInfo>> GetVersionsAsync(
        string directory, bool includeOnline, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        directory = GameRootLocator.Resolve(directory);
        var path = new MinecraftPath(directory);
        var local = await new LocalJsonVersionLoader(path).GetVersionMetadatasAsync(cancellationToken);
        var versions = includeOnline
            ? await CreateLauncher(directory, online: true).GetAllVersionsAsync(cancellationToken)
            : local;
        return (IReadOnlyList<GameVersionInfo>)versions
            .Where(v => local.Contains(v.Name) || v.Type == "release")
            .OrderByDescending(v => v.ReleaseTime)
            .Select(v => new GameVersionInfo(v.Name, v.Type ?? "custom", local.Contains(v.Name))).ToArray();
    }, cancellationToken);

    public Task RepairAsync(LauncherSettings settings, GameVersionInfo version,
        IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        settings.Validate();
        var launcher = CreateLauncher(settings.GameDirectory, online: true);
        if (launcher.GameInstaller is GameInstallerBase installer)
        {
            installer.CheckFileSize = true;
            installer.CheckFileChecksum = true;
        }
        progress.Report(new GameProgress("正在校验并修复游戏、依赖、资源与 Java；保留存档和自定义配置…"));
        await InstallCoreAsync(launcher, version.Id, progress, cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

    public Task InstallAsync(LauncherSettings settings, GameVersionInfo version,
        IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        settings.Validate();
        var launcher = CreateLauncher(settings.GameDirectory, online: true);
        await InstallCoreAsync(launcher, version.Id, progress, cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

    public Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request,
        IProgress<GameProgress> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        request = request with { Settings = request.Settings with { GameDirectory = GameRootLocator.Resolve(request.Settings.GameDirectory) } };
        request.Settings.Validate();
        if (!request.AllowDownloads && !request.Version.IsInstalled)
        {
            throw new InvalidOperationException("该版本尚未安装。请先下载，或勾选允许下载缺失文件。");
        }
        var launcher = CreateLauncher(request.Settings.GameDirectory, request.AllowDownloads);
        if (request.AllowDownloads)
        {
            await InstallCoreAsync(launcher, request.Version.Id, progress, cancellationToken).ConfigureAwait(false);
        }
        progress.Report(new GameProgress("检查 Java 与启动环境…"));
        var version = await launcher.GetVersionAsync(request.Version.Id, cancellationToken).ConfigureAwait(false);
        var java = ResolveJava(request.Settings.JavaPath, launcher, version);
        var required = version.GetInheritedProperty(v => v.JavaVersion)?.MajorVersion;
        var minimumMajor = int.TryParse(required, out var parsed) ? parsed : 8;
        var major = await JavaRuntimeProbe.CheckAsync(java, minimumMajor, cancellationToken).ConfigureAwait(false);
        progress.Report(new GameProgress($"Java {major} 已就绪，正在准备启动…"));
        var session = new MSession(request.Session.UserName, request.Session.AccessToken, request.Session.Uuid)
        {
            UserType = request.Session.IsMicrosoft ? "msa" : "legacy",
            Xuid = request.Session.Xuid
        };
        var workingDirectory = GameEnvironment.GetWorkingDirectory(request.Settings, request.Version.Id);
        Directory.CreateDirectory(workingDirectory);
        await GameEnvironment.ApplyLanguageAsync(workingDirectory,
            GameEnvironment.ResolveLanguage(request.Settings.Language, CultureInfo.CurrentUICulture), cancellationToken).ConfigureAwait(false);
        var memory = GameEnvironment.GetMemory(request.Settings);
        progress.Report(new GameProgress($"分配内存 {memory:N0} MiB · {(request.Settings.IsolateVersions ? "版本隔离" : "共享目录")}"));
        var process = launcher.BuildProcess(version, new MLaunchOption
        {
            Session = session, JavaPath = java, MaximumRamMb = memory,
            GameLauncherName = "PIgHeAD", GameLauncherVersion = LauncherVersion.Value,
            ScreenWidth = request.Settings.WindowWidth, ScreenHeight = request.Settings.WindowHeight,
            FullScreen = request.Settings.WindowMode == GameWindowMode.Fullscreen,
            ArgumentDictionary = new Dictionary<string, string> { ["game_directory"] = workingDirectory },
            ExtraJvmArguments = LaunchArgumentParser.Parse(request.Settings.ExtraJvmArguments).Select(a => new MArgument(a)).ToArray(),
            ExtraGameArguments = LaunchArgumentParser.Parse(request.Settings.ExtraGameArguments).Select(a => new MArgument(a)).ToArray()
        });
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        var logDirectory = gameLogDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PIgHeAD", "Launcher", "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"game-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        var log = new StreamWriter(new FileStream(logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!process.Start())
            {
                throw new InvalidOperationException("Java 进程未能启动。");
            }
            try
            {
                process.PriorityClass = request.Settings.ProcessPriority switch
                {
                    GamePriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                    GamePriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                    GamePriority.High => ProcessPriorityClass.High,
                    _ => ProcessPriorityClass.Normal
                };
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                await log.WriteLineAsync("未能应用进程优先级，保留系统默认值。").ConfigureAwait(false);
            }
            return new GameLaunchResult(process.Id, logPath,
                ObserveProcessAsync(process, log, request.Session, request.Settings.WindowMode));
        }
        catch
        {
            log.Dispose();
            process.Dispose();
            throw;
        }
    }, cancellationToken);

    internal static MinecraftLauncher CreateLauncher(string directory, bool online)
    {
        var path = new MinecraftPath(GameRootLocator.Resolve(directory));
        var parameters = MinecraftLauncherParameters.CreateDefault(path, Http);
        if (!online)
        {
            parameters.VersionLoader = new LocalJsonVersionLoader(path);
        }
        return new MinecraftLauncher(parameters);
    }

    private static async Task InstallCoreAsync(MinecraftLauncher launcher, string version,
        IProgress<GameProgress> progress, CancellationToken cancellationToken)
    {
        progress.Report(new GameProgress("检查并下载游戏、依赖和 Java 文件…", 0));
        var fileProgress = new CallbackProgress<InstallerProgressChangedEventArgs>(e =>
            progress.Report(new GameProgress($"准备文件 {e.ProgressedTasks}/{e.TotalTasks} · {e.Name}",
                e.TotalTasks == 0 ? null : Math.Clamp(e.ProgressedTasks * 100d / e.TotalTasks, 0, 100))));
        await launcher.InstallAsync(version, fileProgress, null, cancellationToken).ConfigureAwait(false);
        progress.Report(new GameProgress("文件准备完成", 100));
    }

    private static string ResolveJava(string configured, MinecraftLauncher launcher, IVersion version)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }
        var candidates = new List<string?> { launcher.GetJavaPath(version), launcher.GetDefaultJavaPath() };
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            candidates.Add(Path.Combine(javaHome, "bin", "java.exe"));
        }
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.Combine(p.Trim('"'), "java.exe")));
        return candidates.FirstOrDefault(p => p is not null && File.Exists(p))
            ?? throw new FileNotFoundException("未找到 Java。请填写 Java 路径，或允许下载对应运行时。");
    }

    private static async Task<int> ObserveProcessAsync(Process process, StreamWriter log, GameSession session, GameWindowMode windowMode)
    {
        using var gate = new SemaphoreSlim(1);
        async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (session.IsMicrosoft && session.AccessToken.Length > 0)
                {
                    line = line.Replace(session.AccessToken, "[REDACTED]", StringComparison.Ordinal);
                }
                await gate.WaitAsync().ConfigureAwait(false);
                try { await log.WriteLineAsync(line).ConfigureAwait(false); }
                finally { gate.Release(); }
            }
        }
        try
        {
            await Task.WhenAll(PumpAsync(process.StandardOutput), PumpAsync(process.StandardError),
                process.WaitForExitAsync(), ApplyWindowModeAsync(process, windowMode)).ConfigureAwait(false);
            return process.ExitCode;
        }
        finally
        {
            await log.DisposeAsync().ConfigureAwait(false);
            process.Dispose();
        }
    }

    private static async Task ApplyWindowModeAsync(Process process, GameWindowMode mode)
    {
        if (mode != GameWindowMode.Maximized) return;
        for (var attempt = 0; attempt < 100 && !process.HasExited; attempt++)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindowAsync(process.MainWindowHandle, 3);
                return;
            }
            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
