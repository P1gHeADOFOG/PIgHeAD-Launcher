using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Abstractions;

public interface IAccountService
{
    Task<GameSession> SignInAsync(string clientId, CancellationToken cancellationToken);
    Task<GameSession?> RestoreAsync(string clientId, CancellationToken cancellationToken);
    Task SignOutAsync(string clientId, CancellationToken cancellationToken);
}
