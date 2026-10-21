using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly SettingsViewModel settings;
    private readonly IGameService games;

    public HomeViewModel(SettingsViewModel settings, VersionsViewModel versions,
        AccountsViewModel accounts, DownloadsViewModel downloads, IGameService games)
    {
        this.settings = settings;
        this.games = games;
        Versions = versions;
        Accounts = accounts;
        Downloads = downloads;
        settings.PropertyChanged += OnDependencyChanged;
        versions.PropertyChanged += OnDependencyChanged;
        accounts.PropertyChanged += OnDependencyChanged;
        downloads.PropertyChanged += OnDependencyChanged;
    }

    public VersionsViewModel Versions { get; }
    public AccountsViewModel Accounts { get; }
    public DownloadsViewModel Downloads { get; }

    public string SelectedVersion => Versions.SelectedGameVersion?.Id ?? "未选择版本";
    public string VersionDescription => Versions.SelectedGameVersion?.DisplayName ?? "请在 Versions 选择本地版本，或读取官方列表";
    public string AccountName => Accounts.DisplayName;
    public string AccountDescription => Accounts.Description;
    public string MemorySummary => settings.SavedSettings is { } saved
        ? saved.AutomaticMemory ? "自动 · 启动时分配" : saved.MaximumMemoryMb.ToString("N0", CultureInfo.CurrentCulture) + " MiB" : "尚未加载配置";
    public string GameDirectory => settings.SavedSettings is { } saved
        ? Versions.SelectedGameVersion is { } selected
            ? Services.Minecraft.GameEnvironment.GetWorkingDirectory(saved, selected.Id) : saved.GameDirectory
        : "尚未加载配置";
    public string DownloadStatus => Downloads.Status;

    [ObservableProperty]
    public partial string JavaStatus { get; private set; } = "启动时自动检查";

    [ObservableProperty]
    public partial string LaunchNotice { get; private set; } = "选择版本与账户后即可启动。";

    [ObservableProperty]
    public partial bool AllowDownloads { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPreparing { get; private set; }

    private bool CanLaunch() => settings.SavedSettings is not null
        && Versions.SelectedGameVersion is not null && Downloads.IsIdle
        && !Versions.IsLoading && !Accounts.IsBusy;

    private void OnDependencyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Versions) && e.PropertyName == nameof(VersionsViewModel.SelectedGameVersion))
        {
            OnPropertyChanged(nameof(SelectedVersion));
            OnPropertyChanged(nameof(VersionDescription));
            OnPropertyChanged(nameof(GameDirectory));
        }
        if (ReferenceEquals(sender, Accounts) && e.PropertyName is nameof(AccountsViewModel.DisplayName) or nameof(AccountsViewModel.Description))
        {
            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(AccountDescription));
        }
        if (ReferenceEquals(sender, Downloads) && e.PropertyName == nameof(DownloadsViewModel.Status))
        {
            OnPropertyChanged(nameof(DownloadStatus));
        }
        if (ReferenceEquals(sender, settings) && e.PropertyName == nameof(SettingsViewModel.SavedSettings))
        {
            OnPropertyChanged(nameof(MemorySummary));
            OnPropertyChanged(nameof(GameDirectory));
            JavaStatus = string.IsNullOrWhiteSpace(settings.SavedSettings?.JavaPath)
                ? "启动时自动检查" : "已配置 · " + Path.GetFileName(settings.SavedSettings.JavaPath);
        }
        if (e.PropertyName is nameof(SettingsViewModel.SavedSettings) or nameof(VersionsViewModel.SelectedGameVersion)
            or nameof(VersionsViewModel.IsLoading) or nameof(AccountsViewModel.IsBusy) or nameof(DownloadsViewModel.IsIdle))
        {
            LaunchGameCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunch), IncludeCancelCommand = true)]
    private async Task LaunchGameAsync(CancellationToken cancellationToken)
    {
        if (settings.SavedSettings is not { } snapshot || Versions.SelectedGameVersion is not { } version)
        {
            return;
        }
        IsPreparing = true;
        Downloads.Begin("验证账户并准备启动…", LaunchGameCommand.Cancel);
        LaunchNotice = "正在准备游戏，可取消准备操作。";
        try
        {
            var session = await Accounts.GetLaunchSessionAsync(cancellationToken);
            var operationProgress = Downloads.CreateProgress();
            var progress = new Progress<GameProgress>(value =>
            {
                operationProgress.Report(value);
                if (IsPreparing && value.Message.StartsWith("Java ", StringComparison.Ordinal))
                {
                    JavaStatus = value.Message;
                }
            });
            var result = await games.LaunchAsync(new GameLaunchRequest(snapshot, version, session, AllowDownloads),
                progress, cancellationToken);
            IsPreparing = false;
            Downloads.IsGameRunning = true;
            Downloads.LogFilePath = result.LogFilePath;
            Downloads.End($"游戏运行中 · PID {result.ProcessId}");
            LaunchNotice = $"游戏已启动。日志：{result.LogFilePath}";
            var exitCode = await result.Completion;
            LaunchNotice = exitCode == 0
                ? "游戏已正常退出。"
                : $"游戏退出码 {exitCode}。请查看日志：{result.LogFilePath}";
            Downloads.End(exitCode == 0 ? "游戏已退出" : $"游戏异常退出 · {exitCode}");
        }
        catch (OperationCanceledException)
        {
            LaunchNotice = "启动准备已取消。";
            Downloads.End("准备已取消");
        }
        catch (Exception exception)
        {
            LaunchNotice = $"启动失败：{exception.Message}";
            Downloads.End("启动失败");
        }
        finally
        {
            IsPreparing = false;
            Downloads.IsGameRunning = false;
            if (Downloads.IsBusy)
            {
                Downloads.End("准备已结束");
            }
        }
    }
}
