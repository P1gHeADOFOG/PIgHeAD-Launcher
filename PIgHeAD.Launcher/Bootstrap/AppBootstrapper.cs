using PIgHeAD.Launcher.Services.Accounts;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Desktop;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;
using PIgHeAD.Launcher.Views;

namespace PIgHeAD.Launcher.Bootstrap;

public static class AppBootstrapper
{
    public static MainWindow CreateMainWindow(LauncherPaths? paths = null)
    {
        paths ??= new LauncherPaths(AppContext.BaseDirectory);
        var downloads = new DownloadsViewModel();
        var desktop = new DesktopService();
        var legacySettings = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PIgHeAD", "Launcher", "settings.json");
        var settings = new SettingsViewModel(new PortableSettingsService(paths, legacySettings), desktop, downloads);
        var games = new MinecraftGameService();
        var loaders = new ModLoaderService();
        var accounts = new AccountsViewModel(new MicrosoftAccountService(paths.AuthDirectory), settings, downloads);
        var versions = new VersionsViewModel(games, settings, downloads, new GameImportService(games, loaders, paths.ImportDirectory), desktop, new LocalVersionService());
        downloads.Catalog = new DownloadCatalogViewModel(games, loaders, desktop, settings, versions, downloads);
        var home = new HomeViewModel(settings, versions, accounts, downloads, games);
        return new MainWindow(new MainViewModel(home, settings, versions, accounts, downloads));
    }
}
