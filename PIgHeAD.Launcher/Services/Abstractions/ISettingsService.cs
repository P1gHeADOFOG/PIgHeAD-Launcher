using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface ISettingsService
{
    string SettingsFilePath { get; }

    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}
