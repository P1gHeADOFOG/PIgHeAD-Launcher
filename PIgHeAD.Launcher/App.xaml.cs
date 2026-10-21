using System.Windows;
using PIgHeAD.Launcher.Bootstrap;
using PIgHeAD.Launcher.Services.Consent;
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
            var consent = new UserConsentService();
            if (!await consent.IsAcceptedAsync(lifetime.Token)
                && new ConsentWindow(new ConsentViewModel(consent)).ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            var window = AppBootstrapper.CreateMainWindow();
            mainViewModel = (MainViewModel)window.DataContext;
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
            MessageBox.Show($"启动器无法启动：{exception.Message}", "PIgHeAD - 猪头启动器",
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

