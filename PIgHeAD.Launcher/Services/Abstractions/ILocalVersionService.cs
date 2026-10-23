using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface ILocalVersionService
{
    Task<LocalVersionDetails> GetDetailsAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token);
    Task<IReadOnlyList<LocalModInfo>> GetModsAsync(LocalVersionDetails details, CancellationToken token);
    Task DeleteAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token);
}
