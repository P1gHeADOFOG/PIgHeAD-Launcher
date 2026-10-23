using PIgHeAD.Launcher.Services.Accounts;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.Tests;

public sealed class LaunchWorkflowTests
{
    [Fact]
    public async Task LaunchUsesSavedSettingsAndBlocksDuplicateLaunchUntilExit()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.GameService.Exit = exit.Task;
        context.Home.AllowDownloads = false;

        var launch = context.Home.LaunchGameCommand.ExecuteAsync(null);

        Assert.True(context.Downloads.IsGameRunning);
        Assert.False(context.Home.LaunchGameCommand.CanExecute(null));
        Assert.False(context.Versions.RepairSelectedCommand.CanExecute(null));
        Assert.False(context.Accounts.SignInCommand.CanExecute(null));
        Assert.Equal(4096, context.GameService.LastRequest!.Settings.MaximumMemoryMb);
        Assert.False(context.GameService.LastRequest.AllowDownloads);
        Assert.False(context.GameService.LastRequest.Session.IsMicrosoft);
        exit.SetResult(0);
        await launch;

        Assert.False(context.Downloads.IsGameRunning);
        Assert.True(context.Home.LaunchGameCommand.CanExecute(null));
        Assert.Equal(1, context.GameService.LaunchCalls);
    }

    [Fact]
    public async Task UnauthenticatedMicrosoftSelectionNeverFallsBackToOffline()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.Accounts.UseMicrosoft = true;

        await context.Home.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(0, context.GameService.LaunchCalls);
        Assert.Contains("登录", context.Home.LaunchNotice);
        Assert.Null(context.Accounts.MicrosoftSession);
    }

    [Fact]
    public async Task InvalidOfflineNameNeverStartsGame()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.Accounts.OfflineName = "bad name";

        await context.Home.LaunchGameCommand.ExecuteAsync(null);

        Assert.Equal(0, context.GameService.LaunchCalls);
        Assert.Contains("玩家名", context.Home.LaunchNotice);
    }

    [Fact]
    public async Task CancellationRestoresControls()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.GameService.WaitForCancellation = true;

        var task = context.Home.LaunchGameCommand.ExecuteAsync(null);
        Assert.True(context.Home.IsPreparing);
        context.Home.LaunchGameCommand.Cancel();
        await task;

        Assert.False(context.Home.IsPreparing);
        Assert.True(context.Downloads.IsIdle);
        Assert.Contains("取消", context.Home.LaunchNotice);
    }

    [Fact]
    public async Task FailedAuthenticationNeverClaimsSuccess()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);

        await context.Accounts.SignInCommand.ExecuteAsync(null);

        Assert.Null(context.Accounts.MicrosoftSession);
        Assert.False(context.Accounts.UseMicrosoft);
        Assert.Contains("登录未完成", context.Accounts.Status);
    }

    [Fact]
    public async Task MissingClientIdDoesNotOpenBrowserOrCreateSession()
    {
        var service = new MicrosoftAccountService();

        Assert.Null(await service.RestoreAsync("", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SignInAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task FailedRepairReleasesOperationState()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.GameService.InstallError = new InvalidOperationException("Network unavailable");

        await context.Versions.RepairSelectedCommand.ExecuteAsync(null);

        Assert.True(context.Downloads.IsIdle);
        Assert.Contains("Network unavailable", context.Downloads.Status);
    }

    [Theory]
    [InlineData("java version \"1.8.0_402\"", 8)]
    [InlineData("openjdk version \"17.0.12\"", 17)]
    [InlineData("openjdk 21.0.4 2024-07-16", 21)]
    public void JavaVersionOutputIsParsed(string output, int expected) =>
        Assert.Equal(expected, JavaRuntimeProbe.ParseMajorVersion(output));

    [Fact]
    public void OfflineIdentityIsStableAndClearlyUnlicensed()
    {
        var first = OfflineSessionFactory.Create("Player");
        var second = OfflineSessionFactory.Create("Player");
        Assert.Equal(first.Uuid, second.Uuid);
        Assert.Equal(32, first.Uuid.Length);
        Assert.False(first.IsMicrosoft);
        Assert.DoesNotContain(first.AccessToken, first.ToString());
    }
}
