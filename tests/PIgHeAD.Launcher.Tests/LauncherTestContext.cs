using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

internal sealed class LauncherTestContext
{
    public LauncherTestContext(SettingsViewModel? settings = null)
    {
        Settings = settings ?? new SettingsViewModel(new MemorySettingsService());
        Downloads = new DownloadsViewModel();
        Accounts = new AccountsViewModel(AccountService, Settings, Downloads);
        Versions = new VersionsViewModel(GameService, Settings, Downloads);
        Home = new HomeViewModel(Settings, Versions, Accounts, Downloads, GameService);
        Main = new MainViewModel(Home, Settings, Versions, Accounts, Downloads);
    }

    public SettingsViewModel Settings { get; }
    public DownloadsViewModel Downloads { get; }
    public AccountsViewModel Accounts { get; }
    public VersionsViewModel Versions { get; }
    public HomeViewModel Home { get; }
    public MainViewModel Main { get; }
    public FakeGameService GameService { get; } = new();
    public FakeAccountService AccountService { get; } = new();

    internal sealed class MemorySettingsService : ISettingsService
    {
        public string SettingsFilePath => @"C:\tests\settings.json";
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(new LauncherSettings()));
        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeGameService : IGameService
    {
        public int LaunchCalls { get; private set; }
        public GameLaunchRequest? LastRequest { get; private set; }
        public bool WaitForCancellation { get; set; }
        public Task<int> Exit { get; set; } = Task.FromResult(0);
        public Exception? InstallError { get; set; }
        public Exception? CatalogError { get; set; }
        public TaskCompletionSource<IReadOnlyList<GameVersionInfo>>? PendingCatalog { get; set; }
        public int OnlineCatalogCalls { get; private set; }
        public GameVersionInfo? LastInstalledVersion { get; private set; }
        public int RepairCalls { get; private set; }
        public Task RepairAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken token)
        {
            RepairCalls++;
            return InstallAsync(settings, version, progress, token);
        }
        public Task<IReadOnlyList<GameVersionInfo>> GetVersionsAsync(string directory, bool includeOnline, CancellationToken token)
        {
            if (includeOnline)
            {
                OnlineCatalogCalls++;
                if (CatalogError is not null) return Task.FromException<IReadOnlyList<GameVersionInfo>>(CatalogError);
                if (PendingCatalog is not null) return PendingCatalog.Task.WaitAsync(token);
            }
            return Task.FromResult<IReadOnlyList<GameVersionInfo>>([new("1.21.4", "release", true)]);
        }
        public Task InstallAsync(LauncherSettings settings, GameVersionInfo version, IProgress<GameProgress> progress, CancellationToken token)
        {
            LastInstalledVersion = version;
            return InstallError is null ? Task.CompletedTask : Task.FromException(InstallError);
        }
        public async Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, IProgress<GameProgress> progress, CancellationToken token)
        {
            LaunchCalls++;
            LastRequest = request;
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return new GameLaunchResult(123, @"C:\tests\game.log", Exit);
        }
    }

    internal sealed class FakeAccountService : IAccountService
    {
        public GameSession? Session { get; set; }
        public Exception SignInError { get; set; } = new InvalidOperationException("应用未配置或认证失败");
        public Task<GameSession> SignInAsync(string clientId, CancellationToken token) =>
            Task.FromException<GameSession>(SignInError);
        public Task<GameSession?> RestoreAsync(string clientId, CancellationToken token) => Task.FromResult(Session);
        public Task SignOutAsync(string clientId, CancellationToken token) => Task.CompletedTask;
    }
}
