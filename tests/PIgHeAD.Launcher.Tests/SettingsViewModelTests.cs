using System.IO;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class SettingsViewModelTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("511")]
    [InlineData("65537")]
    [InlineData("99999999999999")]
    public async Task InvalidMemoryDoesNotReachStorage(string memory)
    {
        var service = new FakeSettingsService();
        var viewModel = new SettingsViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.MaximumMemoryMb = memory;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(0, service.SaveCalls);
    }

    [Fact]
    public async Task FailedSaveRetainsLastSavedSettingsAndRestoresCommands()
    {
        var service = new FakeSettingsService { SaveError = new IOException("Disk full") };
        var viewModel = new SettingsViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var original = viewModel.SavedSettings;
        viewModel.MaximumMemoryMb = "8192";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Same(original, viewModel.SavedSettings);
        Assert.Contains("Disk full", viewModel.StatusMessage);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedLoadCanBeRetriedBeforeSaving()
    {
        var service = new FakeSettingsService { LoadError = new IOException("File locked") };
        var viewModel = new SettingsViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(viewModel.CanEdit);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.LoadCommand.CanExecute(null));

        service.LoadError = null;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.True(viewModel.CanEdit);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task RecoveryDisplaysBackupWarning()
    {
        var service = new FakeSettingsService { BackupFilePath = @"C:\settings.corrupt.json" };
        var viewModel = new SettingsViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.True(viewModel.CanEdit);
        Assert.Contains(service.BackupFilePath, viewModel.StatusMessage);
    }

    [Fact]
    public async Task NavigationPreservesDraftAndHomeUsesOnlySavedSettings()
    {
        var service = new FakeSettingsService();
        var settings = new SettingsViewModel(service);
        var context = new LauncherTestContext(settings);
        var home = context.Home;
        var main = context.Main;
        await settings.LoadCommand.ExecuteAsync(null);
        var originalSummary = home.MemorySummary;

        main.ShowSettingsCommand.Execute(null);
        settings.MaximumMemoryMb = "8192";
        settings.GameDirectory = @"C:\Custom\Minecraft";
        main.ShowHomeCommand.Execute(null);

        Assert.Same(home, main.CurrentPage);
        Assert.Equal(originalSummary, home.MemorySummary);
        main.ShowSettingsCommand.Execute(null);
        Assert.Equal("8192", settings.MaximumMemoryMb);
        await settings.SaveCommand.ExecuteAsync(null);

        Assert.NotEqual(originalSummary, home.MemorySummary);
        Assert.Equal(8192, settings.SavedSettings!.MaximumMemoryMb);
        Assert.Equal(settings.GameDirectory, home.GameDirectory);
        Assert.Equal(1, service.SaveCalls);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public string SettingsFilePath => @"C:\test\settings.json";
        public Exception? LoadError { get; set; }
        public Exception? SaveError { get; init; }
        public string? BackupFilePath { get; init; }
        public int SaveCalls { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            LoadError is null
                ? Task.FromResult(new SettingsLoadResult(new LauncherSettings(), BackupFilePath))
                : Task.FromException<SettingsLoadResult>(LoadError);

        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return SaveError is null ? Task.CompletedTask : Task.FromException(SaveError);
        }
    }
}
