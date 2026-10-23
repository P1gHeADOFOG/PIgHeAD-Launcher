using System.Windows;
using PIgHeAD.Launcher.Bootstrap;
using PIgHeAD.Launcher.Services.Consent;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.Services.Presentation;
using PIgHeAD.Launcher.ViewModels;
using PIgHeAD.Launcher.Views;

namespace PIgHeAD.Launcher;

public partial class App : Application
{
    private MainViewModel? mainViewModel;
    private readonly CancellationTokenSource lifetime = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var paths = new LauncherPaths(AppContext.BaseDirectory);
            System.IO.Directory.CreateDirectory(paths.DataDirectory);
            await PresentationSettings.Current.InitializeAsync(System.IO.Path.Combine(paths.DataDirectory, "appearance.json"), lifetime.Token);
            var consent = new UserConsentService(paths.ConsentFile);
            if (!await consent.IsAcceptedAsync(lifetime.Token)
                && new ConsentWindow(new ConsentViewModel(consent)).ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            var window = AppBootstrapper.CreateMainWindow(paths);
            mainViewModel = (MainViewModel)window.DataContext;
            await mainViewModel.Settings.LoadCommand.ExecuteAsync(null);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            await mainViewModel.InitializeAsync(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The main window waits for startup work to unwind before closing.
        }
        catch (Exception exception)
        {
            MessageBox.Show(PresentationSettings.Current.Localize($"启动器无法启动：{exception.Message}"), PresentationSettings.Current.Localize("PIgHeAD - 猪头启动器"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    internal void CancelStartup() => lifetime.Cancel();

    protected override void OnExit(ExitEventArgs e)
    {
        lifetime.Cancel();
        mainViewModel?.CancelPendingOperations();
        base.OnExit(e);
    }
}

