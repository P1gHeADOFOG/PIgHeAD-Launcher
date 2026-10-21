using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface IGameService
{
    Task<IReadOnlyList<GameVersionInfo>> GetVersionsAsync(string directory, bool includeOnline, CancellationToken cancellationToken);
    Task InstallAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken cancellationToken);
    Task RepairAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken cancellationToken);
    Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, IProgress<GameProgress> progress, CancellationToken cancellationToken);
}
