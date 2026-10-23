using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private NavigationItemViewModel selectedNavigationItem;

    public MainViewModel(HomeViewModel home, SettingsViewModel settings, VersionsViewModel versions,
        AccountsViewModel accounts, DownloadsViewModel downloads)
    {
        Home = home;
        Settings = settings;
        Versions = versions;
        Accounts = accounts;
        Downloads = downloads;
        NavigationItems =
        [
            new("Home", "主页", home),
            new("Versions", "版本管理", versions),
            new("Downloads", "下载中心", downloads),
            new("Accounts", "账户管理", accounts),
            new("Settings", "启动设置", settings)
        ];
        selectedNavigationItem = NavigationItems[0];
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SavedSettings)) OnPropertyChanged(nameof(AnimationsEnabled));
        };
    }

    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public VersionsViewModel Versions { get; }
    public AccountsViewModel Accounts { get; }
    public DownloadsViewModel Downloads { get; }
    public bool AnimationsEnabled => Settings.SavedSettings?.EnableAnimations ?? true;
    public string VersionDisplay => LauncherVersion.Display;
    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public NavigationItemViewModel SelectedNavigationItem
    {
        get => selectedNavigationItem;
        set
        {
            if (value is not null && NavigationItems.Contains(value) && SetProperty(ref selectedNavigationItem, value))
            {
                OnPropertyChanged(nameof(CurrentPage));
            }
        }
    }

    public ObservableObject CurrentPage => SelectedNavigationItem.Page;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!Settings.IsLoaded) await Settings.LoadCommand.ExecuteAsync(null);
        if (Settings.SavedSettings is not null)
        {
            using var registration = cancellationToken.Register(CancelPendingOperations);
            await Task.WhenAll(Versions.RefreshLocalCommand.ExecuteAsync(null),
                Accounts.RestoreAsync(cancellationToken), Downloads.Catalog?.EnsureLoadedAsync() ?? Task.CompletedTask);
        }
    }

    public void CancelPendingOperations()
    {
        Settings.LoadCommand.Cancel();
        Settings.SaveCommand.Cancel();
        Settings.SwitchGameDirectoryCommand.Cancel();
        Versions.RefreshLocalCommand.Cancel();
        Versions.ShowDetailsCommand.Cancel();
        Versions.RequestDeleteCommand.Cancel();
        Versions.ConfirmDeleteCommand.Cancel();
        Versions.ViewModsCommand.Cancel();
        Versions.ImportPathCommand.Cancel();
        Versions.RepairSelectedCommand.Cancel();
        Accounts.SignInCommand.Cancel();
        Home.LaunchGameCommand.Cancel();
        Downloads.Catalog?.CancelPendingOperations();
    }

    [RelayCommand]
    private void ShowHome() => NavigateTo(Home);

    [RelayCommand]
    private void ShowVersions() => NavigateTo(Versions);

    [RelayCommand]
    private void ShowDownloads() => NavigateTo(Downloads);

    [RelayCommand]
    private void ShowAccounts() => NavigateTo(Accounts);

    [RelayCommand]
    private void ShowSettings() => NavigateTo(Settings);

    private void NavigateTo(ObservableObject page) =>
        SelectedNavigationItem = NavigationItems.Single(item => ReferenceEquals(item.Page, page));
}
