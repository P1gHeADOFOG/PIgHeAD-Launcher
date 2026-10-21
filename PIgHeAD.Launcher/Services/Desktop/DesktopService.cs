using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Consent;
using PIgHeAD.Launcher.ViewModels;
using PIgHeAD.Launcher.Views;

namespace PIgHeAD.Launcher.Services.Desktop;

public sealed class DesktopService : IDesktopService
{
    public string? SelectFolder(string initialDirectory)
    {
        var dialog = new OpenFolderDialog { Title = "选择游戏根目录" };
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }
    public string? SelectFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }
    public void OpenWebsite(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("只支持打开 HTTPS 官方网站。");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    public void OpenFolder(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("目录尚不存在。");
        Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
    public void ShowLegalDocuments() => new ConsentWindow(new ConsentViewModel(new UserConsentService()), readOnly: true)
        { Owner = Application.Current.MainWindow }.ShowDialog();
}
