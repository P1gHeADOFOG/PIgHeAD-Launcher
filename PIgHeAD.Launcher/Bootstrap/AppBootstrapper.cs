using PIgHeAD.Launcher.Services.Accounts;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Desktop;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;
using PIgHeAD.Launcher.Views;

namespace PIgHeAD.Launcher.Bootstrap;

public static class AppBootstrapper
{
    public static MainWindow CreateMainWindow()
    {
        var downloads = new DownloadsViewModel();
        var desktop = new DesktopService();
        var settings = new SettingsViewModel(new JsonSettingsService(), desktop, downloads);
        var games = new MinecraftGameService();
        var loaders = new ModLoaderService();
        var accounts = new AccountsViewModel(new MicrosoftAccountService(), settings, downloads);
        var versions = new VersionsViewModel(games, settings, downloads, new GameImportService(games, loaders), desktop);
        downloads.Catalog = new DownloadCatalogViewModel(games, loaders, desktop, settings, versions, downloads);
        var home = new HomeViewModel(settings, versions, accounts, downloads, games);
        return new MainWindow(new MainViewModel(home, settings, versions, accounts, downloads));
    }
}
