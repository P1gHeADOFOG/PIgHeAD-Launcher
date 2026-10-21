using System.Windows;
using System.ComponentModel;
using System.IO;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Views;

public partial class MainWindow : Window
{
    private bool stoppingOperations;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
        Title = viewModel.Downloads.Catalog?.RunOptiFineInstallerCommand.IsRunning == true
            ? "PIgHeAD - 请先关闭外部 OptiFine 安装器" : "PIgHeAD - 正在结束任务，请稍候";
        if (Application.Current is App app) app.CancelStartup();
        viewModel.CancelPendingOperations();
        // Keep the window alive until file writes / authentication unwind; never kill a running game.
        // An external OptiFine installer belongs to the user and must be closed in its own window.
        while (HasPendingWork(viewModel)) await Task.Delay(50);
        stoppingOperations = false;
        Close();
    }

    private static bool HasPendingWork(MainViewModel viewModel) => viewModel.Downloads.IsBusy
        || viewModel.Settings.IsBusy || viewModel.Accounts.IsBusy || viewModel.Versions.IsLoading;
}
