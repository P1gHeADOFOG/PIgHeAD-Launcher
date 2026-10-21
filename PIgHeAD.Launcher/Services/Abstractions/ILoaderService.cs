using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface ILoaderService
{
    Task<IReadOnlyList<LoaderBuild>> GetBuildsAsync(DownloadComponent component, string minecraftVersion, CancellationToken cancellationToken);
    Task<string> InstallAsync(LauncherSettings settings, DownloadComponent component, string minecraftVersion,
        LoaderBuild build, IProgress<GameProgress> progress, CancellationToken cancellationToken);
    Task RunOptiFineInstallerAsync(LauncherSettings settings, string jarPath, IProgress<GameProgress> progress, CancellationToken cancellationToken);
}
