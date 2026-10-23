using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class LocalVersionViewModelTests
{
    [Fact]
    public async Task DetailsExposeModsOnlyForRecognizedLoadersAndSelectionClearsStaleDetails()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var local = new FakeLocalVersions();
        var versions = new VersionsViewModel(context.GameService, context.Settings, context.Downloads, localVersions: local);
        await versions.RefreshLocalCommand.ExecuteAsync(null);
        await versions.ShowDetailsCommand.ExecuteAsync(versions.SelectedGameVersion);
        Assert.True(versions.HasDetails);
        Assert.False(versions.CanViewMods);
        Assert.False(versions.ViewModsCommand.CanExecute(null));
        var modded = new GameVersionInfo("modded", "release", true);
        await versions.ShowDetailsCommand.ExecuteAsync(modded);
        Assert.True(versions.CanViewMods);
        await versions.ViewModsCommand.ExecuteAsync(null);
        Assert.True(versions.IsModsVisible);
        Assert.Equal("Example", Assert.Single(versions.Mods).Name);
        versions.SelectedGameVersion = new("other", "release", true);
        Assert.False(versions.HasDetails);
        Assert.False(versions.CanViewMods);
        Assert.Empty(versions.Mods);
        Assert.Equal(0, context.GameService.OnlineCatalogCalls);
    }

    [Fact]
    public async Task DeleteRequiresReviewAndConfirmationAndBlocksConflictingOperations()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var local = new FakeLocalVersions { DeleteCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var versions = new VersionsViewModel(context.GameService, context.Settings, context.Downloads, localVersions: local);
        await versions.RefreshLocalCommand.ExecuteAsync(null);
        await versions.RequestDeleteCommand.ExecuteAsync(versions.SelectedGameVersion);
        Assert.True(versions.IsDeleteConfirmationVisible);
        Assert.Equal(0, local.DeleteCalls);
        versions.CancelDeleteCommand.Execute(null);
        await versions.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, local.DeleteCalls);
        await versions.RequestDeleteCommand.ExecuteAsync(versions.SelectedGameVersion);
        var deletion = versions.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Equal(1, local.DeleteCalls);
        Assert.True(context.Downloads.IsBusy);
        Assert.False(context.Home.LaunchGameCommand.CanExecute(null));
        Assert.False(versions.CancelDeleteCommand.CanExecute(null));
        Assert.False(versions.RefreshLocalCommand.CanExecute(null));
        local.DeleteCompletion.SetResult();
        await deletion;
        Assert.False(versions.IsDeleteConfirmationVisible);
        Assert.False(versions.HasDetails);
        Assert.True(context.Downloads.IsIdle);
        Assert.Contains("回收站", versions.Status);
    }

    [Fact]
    public async Task DeletionFailureKeepsTheVersionAndMakesRetryPossible()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var versions = new VersionsViewModel(context.GameService, context.Settings, context.Downloads,
            localVersions: new FakeLocalVersions { DeleteError = new InvalidOperationException("dependent version") });
        await versions.RefreshLocalCommand.ExecuteAsync(null);
        await versions.RequestDeleteCommand.ExecuteAsync(versions.SelectedGameVersion);
        await versions.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Contains("dependent version", versions.Status);
        Assert.True(versions.IsDeleteConfirmationVisible);
        Assert.True(versions.ConfirmDeleteCommand.CanExecute(null));
        Assert.Single(versions.AvailableVersions);
        Assert.True(context.Downloads.IsIdle);
    }

    [Fact]
    public async Task LateDetailResponseDoesNotReplaceANewSelection()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var pending = new TaskCompletionSource<LocalVersionDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var versions = new VersionsViewModel(context.GameService, context.Settings, context.Downloads,
            localVersions: new FakeLocalVersions { PendingDetails = pending });
        var loading = versions.ShowDetailsCommand.ExecuteAsync(new GameVersionInfo("modded", "release", true));
        versions.SelectedGameVersion = new("other", "release", true);
        pending.SetResult(FakeLocalVersions.Details("modded"));
        await loading;
        Assert.False(versions.HasDetails);
        Assert.False(versions.CanViewMods);
    }

    internal sealed class FakeLocalVersions : ILocalVersionService
    {
        public TaskCompletionSource<LocalVersionDetails>? PendingDetails { get; init; }
        public TaskCompletionSource? DeleteCompletion { get; init; }
        public Exception? DeleteError { get; init; }
        public int DeleteCalls { get; private set; }
        public Task<LocalVersionDetails> GetDetailsAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token) =>
            PendingDetails?.Task ?? Task.FromResult(Details(version.Id));
        public Task<IReadOnlyList<LocalModInfo>> GetModsAsync(LocalVersionDetails details, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<LocalModInfo>>([new("Example", "example", "1.2.3", "example.jar", "已读取本地资料")]);
        public async Task DeleteAsync(LauncherSettings settings, GameVersionInfo version, CancellationToken token)
        {
            DeleteCalls++;
            if (DeleteError is not null) throw DeleteError;
            if (DeleteCompletion is not null) await DeleteCompletion.Task.WaitAsync(token);
        }
        public static LocalVersionDetails Details(string id) => new(id, "1.21.4", id == "modded" ? "Fabric" : "", "0.16.10",
            21, "release", @"C:\tests\.minecraft", @"C:\tests\.minecraft\versions\" + id, @"C:\tests\.minecraft", 1048576,
            DateTimeOffset.Parse("2025-01-01T00:00:00Z"), id == "modded");
    }
}
