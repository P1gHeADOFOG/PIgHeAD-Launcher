using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface IGameLibrarySettingsService : ISettingsService
{
    IReadOnlyList<string> GameDirectories { get; }
    Task<LauncherSettings> LoadGameAsync(string directory, CancellationToken cancellationToken);
}
