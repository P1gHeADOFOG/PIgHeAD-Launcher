using System.Windows;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PIgHeAD.Launcher.Services.Presentation;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Views;

public partial class MainWindow : Window
{
    private bool stoppingOperations;
    private readonly CancellationTokenSource animationLifetime = new();
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += PlayStartupAnimation;
        Closed += (_, _) => animationLifetime.Cancel();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // DWMWCP_ROUND preserves native shadows, resizing and snap on Windows 11.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var preference = 2;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref preference, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void UpdateWindowClip(object sender, SizeChangedEventArgs e)
    {
        var radius = WindowState == WindowState.Maximized ? 0 : 16;
        WindowSurface.Clip = new RectangleGeometry(new Rect(new Point(), e.NewSize), radius, radius);
    }

    private async void PlayStartupAnimation(object sender, RoutedEventArgs e)
    {
        Loaded -= PlayStartupAnimation;
        if (DataContext is not MainViewModel { AnimationsEnabled: true } || !SystemParameters.ClientAreaAnimation) return;
        StartupAnimation.Visibility = Visibility.Visible;
        var duration = TimeSpan.FromMilliseconds(600);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var scale = (ScaleTransform)StartupMark.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.72, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.72, 1, duration) { EasingFunction = ease });
        StartupMark.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        var dots = new[] { StartupDotOne, StartupDotTwo, StartupDotThree };
        for (var index = 0; index < dots.Length; index++)
            dots[index].BeginAnimation(OpacityProperty, new DoubleAnimation(.2, 1, TimeSpan.FromMilliseconds(260))
            { BeginTime = TimeSpan.FromMilliseconds(index * 120), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        try
        {
            await Task.Delay(760, animationLifetime.Token);
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240));
            fade.Completed += (_, _) => StartupAnimation.Visibility = Visibility.Collapsed;
            StartupAnimation.BeginAnimation(OpacityProperty, fade);
        }
        catch (OperationCanceledException) { }
        finally
        {
            foreach (var dot in dots) dot.BeginAnimation(OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private static string? GetDropPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths)
            return null;
        var path = paths[0];
        return Directory.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".mrpack" ? path : null;
    }

    private void HandleDragOver(object sender, DragEventArgs e)
    {
        var canImport = DataContext is MainViewModel vm && vm.Versions.CanImport && GetDropPath(e) is not null;
        e.Effects = canImport ? DragDropEffects.Copy : DragDropEffects.None;
        if (DataContext is MainViewModel main) main.Versions.IsDragOver = canImport;
        e.Handled = true;
    }

    private void HandleDragLeave(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Versions.IsDragOver = false;
    }

    private async void HandleDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not MainViewModel vm) return;
        vm.Versions.IsDragOver = false;
        if (!vm.Versions.CanImport || GetDropPath(e) is not { } path) return;
        vm.ShowVersionsCommand.Execute(null);
        await vm.Versions.ImportPathCommand.ExecuteAsync(path);
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        if (stoppingOperations) { e.Cancel = true; return; }
        if (DataContext is not MainViewModel viewModel || !HasPendingWork(viewModel)) return;
        e.Cancel = true;
        stoppingOperations = true;
        Title = PresentationSettings.Current.Localize(viewModel.Downloads.Catalog?.RunOptiFineInstallerCommand.IsRunning == true
            ? "PIgHeAD - 请先关闭外部 OptiFine 安装器" : "PIgHeAD - 正在结束任务，请稍候");
        if (Application.Current is App app) app.CancelStartup();
        viewModel.CancelPendingOperations();
        // Keep the window alive until file writes / authentication unwind; never kill a running game.
        // An external OptiFine installer belongs to the user and must be closed in its own window.
        while (HasPendingWork(viewModel)) await Task.Delay(50);
        stoppingOperations = false;
        Close();
    }

    private static bool HasPendingWork(MainViewModel viewModel) => viewModel.Downloads.IsBusy || viewModel.Downloads.Catalog?.IsLoading == true
        || PresentationSettings.Current.ToggleLanguageCommand.IsRunning || PresentationSettings.Current.ToggleThemeCommand.IsRunning
        || viewModel.Settings.IsBusy || viewModel.Accounts.IsBusy || viewModel.Versions.IsLoading;
}
