using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface IGameImportService
{
    Task<GameImportResult> ImportAsync(string source, LauncherSettings settings, bool completeFiles,
        IProgress<GameProgress> progress, CancellationToken cancellationToken);
    Task RepairAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken cancellationToken);
}
