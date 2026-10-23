using System.Net;
using System.Net.Http;
using System.Text;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class DownloadCatalogTests
{
    [Fact]
    public async Task StartupPreloadsCatalogAndSelectingLoaderAutomaticallyFindsBuilds()
    {
        var context = new LauncherTestContext();
        var loaders = new FakeLoader();
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        context.Downloads.Catalog = catalog;
        await context.Main.InitializeAsync(CancellationToken.None);
        Assert.NotEmpty(catalog.MinecraftVersions);
        Assert.True(catalog.InstallCommand.CanExecute(null));
        Assert.Empty(context.Downloads.History); // Loading metadata is not an installation task.
        catalog.Component = DownloadComponent.Fabric;
        await (catalog.LoadBuildsCommand.ExecutionTask ?? Task.CompletedTask);
        Assert.NotNull(catalog.Build);
        Assert.True(catalog.InstallCommand.CanExecute(null));
        Assert.Equal(1, loaders.BuildCalls);
        await catalog.EnsureLoadedAsync();
        Assert.Equal(1, loaders.BuildCalls);
        Assert.Equal(1, context.GameService.OnlineCatalogCalls);
        catalog.Component = DownloadComponent.Vanilla;
        await catalog.DownloadVersionCommand.ExecuteAsync(catalog.QuickVersions[0]);
        Assert.Equal(catalog.QuickVersions[0].Id, context.GameService.LastInstalledVersion?.Id);
        Assert.Contains(context.Downloads.History, item => item.Result.Contains("完成"));
    }

    [Fact]
    public async Task FailedAutomaticCatalogCanRetryWithoutBlockingLocalLaunch()
    {
        var context = new LauncherTestContext();
        context.GameService.CatalogError = new HttpRequestException("offline");
        var catalog = new DownloadCatalogViewModel(context.GameService, new FakeLoader(), new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        context.Downloads.Catalog = catalog;
        await context.Main.InitializeAsync(CancellationToken.None);
        Assert.Empty(catalog.MinecraftVersions);
        Assert.Contains("offline", catalog.Status);
        Assert.False(catalog.IsLoading);
        Assert.True(context.Home.LaunchGameCommand.CanExecute(null));
        Assert.True(catalog.LoadMinecraftCommand.CanExecute(null));
        context.GameService.CatalogError = null;
        await catalog.EnsureLoadedAsync();
        Assert.NotEmpty(catalog.MinecraftVersions);
        Assert.Equal(2, context.GameService.OnlineCatalogCalls);
    }

    [Fact]
    public async Task SlowCatalogDoesNotLockLocalGameAndShutdownCancelsWithoutNewBuildRequests()
    {
        var context = new LauncherTestContext();
        context.GameService.PendingCatalog = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var loaders = new FakeLoader();
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        context.Downloads.Catalog = catalog;
        catalog.Component = DownloadComponent.Fabric;
        var initialization = context.Main.InitializeAsync(CancellationToken.None);
        Assert.True(catalog.IsLoading);
        Assert.True(context.Home.LaunchGameCommand.CanExecute(null));
        context.Main.CancelPendingOperations();
        await initialization.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(catalog.IsLoading);
        Assert.Equal(0, loaders.BuildCalls);
    }

    [Fact]
    public async Task SwitchingLoaderRejectsStaleResponseAndLoadsCurrentSelection()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var stale = new TaskCompletionSource<IReadOnlyList<LoaderBuild>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loaders = new FakeLoader
        {
            Fetch = (component, _) =>
            {
                if (component == DownloadComponent.Fabric) return stale.Task; // Simulate a server ignoring cancellation.
                latestStarted.TrySetResult();
                return Task.FromResult<IReadOnlyList<LoaderBuild>>([new("quilt-build", "quilt-build")]);
            }
        };
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        await catalog.EnsureLoadedAsync();
        catalog.Component = DownloadComponent.Fabric;
        Assert.True(catalog.IsLoading);
        Assert.True(context.Downloads.IsIdle);
        Assert.False(catalog.InstallCommand.CanExecute(null));
        catalog.Component = DownloadComponent.Quilt;
        stale.SetResult([new("obsolete-fabric", "obsolete-fabric")]);
        await latestStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await catalog.LoadBuildsCommand.ExecutionTask!;
        Assert.Equal("quilt-build", catalog.Build?.Id);
        Assert.DoesNotContain(catalog.Builds, item => item.Id == "obsolete-fabric");
        Assert.False(catalog.IsLoading);
    }

    [Theory]
    [InlineData(DownloadComponent.Fabric, "https://meta.fabricmc.net/v2/versions/loader/1.21.4")]
    [InlineData(DownloadComponent.Quilt, "https://meta.quiltmc.org/v3/versions/loader/1.21.4")]
    public async Task MetaCatalogReadsOfficialEndpointAndPreservesSelectedBuild(DownloadComponent component, string endpoint)
    {
        using var handler = new MetaHandler("""[{"loader":{"version":"0.16.10","stable":true}},{"loader":{"version":"0.16.9","stable":false}}]""");
        using var http = new HttpClient(handler);
        var service = new ModLoaderService(http);
        var builds = await service.GetBuildsAsync(component, "1.21.4", CancellationToken.None);
        Assert.Equal(endpoint, handler.LastUri);
        Assert.Equal("0.16.10", builds[0].Id);
        Assert.Contains("稳定", builds[0].Label);
        Assert.Equal(2, builds.Count);
    }

    [Fact]
    public async Task CatalogClearsIncompatibleSelectionAndCancellationReleasesOperation()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var loaders = new FakeLoader();
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        await catalog.LoadMinecraftCommand.ExecuteAsync(null);
        catalog.Component = DownloadComponent.Fabric;
        Assert.False(catalog.InstallCommand.CanExecute(null));
        await catalog.LoadBuildsCommand.ExecuteAsync(null);
        Assert.True(catalog.InstallCommand.CanExecute(null));
        catalog.Component = DownloadComponent.Quilt;
        Assert.Null(catalog.Build);
        Assert.Empty(catalog.Builds);
        await catalog.LoadBuildsCommand.ExecuteAsync(null);
        loaders.WaitForCancellation = true;
        var task = catalog.InstallCommand.ExecuteAsync(null);
        Assert.True(context.Downloads.IsBusy);
        Assert.False(context.Home.LaunchGameCommand.CanExecute(null));
        Assert.False(context.Accounts.SignInCommand.CanExecute(null));
        Assert.False(catalog.LoadMinecraftCommand.CanExecute(null));
        context.Downloads.CancelCurrentCommand.Execute(null);
        await task;
        Assert.True(context.Downloads.IsIdle);
        Assert.Contains("取消", catalog.Status);
        Assert.False(context.Downloads.CancelCurrentCommand.CanExecute(null));
        Assert.Contains(context.Downloads.History, item => item.Result.Contains("取消"));
    }

    [Fact]
    public async Task FailedInstallDoesNotClaimSuccessOrLeaveControlsLocked()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var loaders = new FakeLoader { Error = new HttpRequestException("offline") };
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, new FakeDesktop(), context.Settings, context.Versions, context.Downloads);
        await catalog.LoadMinecraftCommand.ExecuteAsync(null);
        catalog.Component = DownloadComponent.Forge;
        await catalog.LoadBuildsCommand.ExecuteAsync(null);
        await catalog.InstallCommand.ExecuteAsync(null);
        Assert.Contains("安装失败", catalog.Status);
        Assert.DoesNotContain("完成", catalog.Status);
        Assert.True(context.Downloads.IsIdle);
        Assert.True(context.Home.LaunchGameCommand.CanExecute(null));
    }

    [Fact]
    public async Task SettingsCannotSaveOrReloadDuringInstallationOrAuthentication()
    {
        var operations = new DownloadsViewModel();
        var settings = new SettingsViewModel(new LauncherTestContext.MemorySettingsService(), operations: operations);
        await settings.LoadCommand.ExecuteAsync(null);
        operations.Begin("install");
        Assert.False(settings.CanEdit);
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.False(settings.LoadCommand.CanExecute(null));
        operations.End("cancelled");
        Assert.True(settings.CanEdit);
        settings.IsAccountOperationInProgress = true;
        Assert.False(settings.SaveCommand.CanExecute(null));
        settings.IsAccountOperationInProgress = false;
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task OptiFineUsesOfficialWebsiteAndDoesNotInvokeAutomaticInstall()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var desktop = new FakeDesktop();
        var loaders = new FakeLoader();
        var catalog = new DownloadCatalogViewModel(context.GameService, loaders, desktop, context.Settings, context.Versions, context.Downloads);
        catalog.Component = DownloadComponent.OptiFine;
        catalog.OpenOfficialWebsiteCommand.Execute(null);
        Assert.Equal("https://optifine.net/downloads", desktop.Address);
        Assert.False(catalog.InstallCommand.CanExecute(null));
        await catalog.RunOptiFineInstallerCommand.ExecuteAsync(null); // No file selected.
        Assert.Equal(0, loaders.InstallCalls);
        Assert.True(context.Downloads.IsIdle);
    }

    private sealed class MetaHandler(string json) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            LastUri = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class FakeDesktop : IDesktopService
    {
        public string? Address { get; private set; }
        public string? SelectFolder(string initial) => null;
        public string? SelectFile(string filter) => null;
        public void OpenWebsite(string address) => Address = address;
        public void OpenFolder(string path) { }
        public void ShowLegalDocuments() { }
    }
    private sealed class FakeLoader : ILoaderService
    {
        public int BuildCalls { get; private set; }
        public Func<DownloadComponent, CancellationToken, Task<IReadOnlyList<LoaderBuild>>>? Fetch { get; init; }
        public bool WaitForCancellation { get; set; }
        public Exception? Error { get; init; }
        public int InstallCalls { get; private set; }
        public Task<IReadOnlyList<LoaderBuild>> GetBuildsAsync(DownloadComponent component, string minecraft, CancellationToken token)
        {
            BuildCalls++;
            return Fetch?.Invoke(component, token) ?? Task.FromResult<IReadOnlyList<LoaderBuild>>([new("test-build", "test-build")]);
        }
        public async Task<string> InstallAsync(LauncherSettings settings, DownloadComponent component, string minecraft,
            LoaderBuild build, IProgress<GameProgress> progress, CancellationToken token)
        {
            InstallCalls++;
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            if (Error is not null) throw Error;
            return "installed-test";
        }
        public Task RunOptiFineInstallerAsync(LauncherSettings settings, string path, IProgress<GameProgress> progress, CancellationToken token) => Task.CompletedTask;
    }
}
