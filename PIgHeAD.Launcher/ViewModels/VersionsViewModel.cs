using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.ViewModels;

public partial class VersionsViewModel : ObservableObject
{
    private readonly IGameService games;
    private readonly SettingsViewModel settings;
    private readonly DownloadsViewModel downloads;
    private readonly IGameImportService? imports;
    private readonly IDesktopService? desktop;
    private string? configuredDirectory;

    public VersionsViewModel(IGameService games, SettingsViewModel settings, DownloadsViewModel downloads,
        IGameImportService? imports = null, IDesktopService? desktop = null)
    {
        this.games = games;
        this.settings = settings;
        this.downloads = downloads;
        this.imports = imports;
        this.desktop = desktop;
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SavedSettings))
            {
                var directory = settings.SavedSettings?.GameDirectory;
                if (!string.Equals(configuredDirectory, directory, StringComparison.OrdinalIgnoreCase))
                {
                    configuredDirectory = directory;
                    AvailableVersions.Clear();
                    SelectedGameVersion = null;
                    Status = "游戏目录已更新，请刷新版本列表。";
                }
            }
            if (e.PropertyName is nameof(SettingsViewModel.SavedSettings) or nameof(SettingsViewModel.IsAccountOperationInProgress) or nameof(SettingsViewModel.IsBusy))
            {
                NotifyCommands();
            }
        };
        downloads.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadsViewModel.IsIdle))
            {
                NotifyCommands();
            }
        };
    }

    public ObservableCollection<GameVersionInfo> AvailableVersions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand), nameof(RepairSelectedCommand))]
    public partial GameVersionInfo? SelectedGameVersion { get; set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = "正在等待配置…";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshLocalCommand), nameof(LoadOnlineCommand), nameof(InstallSelectedCommand),
        nameof(RepairSelectedCommand), nameof(ImportArchiveCommand), nameof(ImportDirectoryCommand))]
    public partial bool IsLoading { get; private set; }

    private bool CanRefresh() => settings.SavedSettings is not null && downloads.IsIdle && !IsLoading
        && !settings.IsAccountOperationInProgress && !settings.IsBusy;
    private bool CanInstall() => CanRefresh() && SelectedGameVersion is not null;
    private bool CanRepair() => CanRefresh() && SelectedGameVersion?.IsInstalled == true;
    public bool CanImport => CanRefresh() && imports is not null;
    private bool CanPickImport() => CanImport && desktop is not null;
    [ObservableProperty] public partial bool CompleteImportedFiles { get; set; } = true;
    [ObservableProperty] public partial bool IsDragOver { get; set; }
    partial void OnIsLoadingChanged(bool value) => NotifyCommands();

    private void NotifyCommands()
    {
        RefreshLocalCommand.NotifyCanExecuteChanged();
        LoadOnlineCommand.NotifyCanExecuteChanged();
        InstallSelectedCommand.NotifyCanExecuteChanged();
        RepairSelectedCommand.NotifyCanExecuteChanged();
        ImportArchiveCommand.NotifyCanExecuteChanged();
        ImportDirectoryCommand.NotifyCanExecuteChanged();
        ImportPathCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanImport));
    }

    [RelayCommand(CanExecute = nameof(CanPickImport))]
    private async Task ImportArchiveAsync()
    {
        try
        {
            if (desktop?.SelectFile("游戏整合包|*.zip;*.mrpack|ZIP 整合包|*.zip|Modrinth 整合包|*.mrpack") is { } path)
                await ImportPathCommand.ExecuteAsync(path);
        }
        catch (Exception exception) { Status = "无法选择整合包：" + exception.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanPickImport))]
    private async Task ImportDirectoryAsync()
    {
        try
        {
            if (desktop?.SelectFolder(settings.SavedSettings?.GameDirectory ?? "") is { } path)
                await ImportPathCommand.ExecuteAsync(path);
        }
        catch (Exception exception) { Status = "无法选择游戏目录：" + exception.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanImport), IncludeCancelCommand = true)]
    private async Task ImportPathAsync(string source, CancellationToken cancellationToken)
    {
        if (!CanImport || settings.SavedSettings is not { } snapshot) return;
        downloads.Begin("导入游戏 / 整合包", ImportPathCommand.Cancel);
        try
        {
            var result = await imports!.ImportAsync(source, snapshot, CompleteImportedFiles, downloads.CreateProgress(), cancellationToken);
            // Once the import has committed, complete its small settings registration without cancellation.
            await settings.ActivateImportedGameAsync(result, CancellationToken.None);
            downloads.End(result.Description);
            await RefreshAsync(false, CancellationToken.None);
            SelectedGameVersion = AvailableVersions.FirstOrDefault(version => version.Id == result.SelectedVersionId) ?? SelectedGameVersion;
            Status = $"{result.Description} 当前目录：{result.GameDirectory}";
        }
        catch (OperationCanceledException) { Status = "导入已取消，原压缩包与当前游戏设置保持不变。"; downloads.End(Status); }
        catch (Exception exception) { Status = "导入未完成：" + exception.Message; downloads.End(Status); }
    }

    [RelayCommand(CanExecute = nameof(CanRepair), IncludeCancelCommand = true)]
    private async Task RepairSelectedAsync(CancellationToken cancellationToken)
    {
        if (settings.SavedSettings is not { } snapshot || SelectedGameVersion is not { } version) return;
        downloads.Begin("修复游戏 · " + version.Id, RepairSelectedCommand.Cancel);
        try
        {
            if (imports is not null) await imports.RepairAsync(snapshot, version, downloads.CreateProgress(), cancellationToken);
            else await games.RepairAsync(snapshot, version, downloads.CreateProgress(), cancellationToken);
            Status = "游戏文件校验及修复完成；存档与自定义配置保留。";
            downloads.End(Status);
        }
        catch (OperationCanceledException) { Status = "修复已取消，可稍后继续。"; downloads.End(Status); }
        catch (Exception exception) { Status = "修复未完成：" + exception.Message + "；加载器或自定义版本资料损坏时，请在下载中心重新安装对应版本。"; downloads.End(Status); }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshLocalAsync(CancellationToken cancellationToken) => RefreshAsync(false, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task LoadOnlineAsync(CancellationToken cancellationToken) => RefreshAsync(true, cancellationToken);

    private async Task RefreshAsync(bool online, CancellationToken cancellationToken)
    {
        if (settings.SavedSettings is not { } snapshot)
        {
            return;
        }
        IsLoading = true;
        Status = online ? "正在读取官方版本列表…" : "正在扫描本地版本…";
        var previous = SelectedGameVersion?.Id ?? snapshot.PreferredVersionId;
        try
        {
            var versions = await games.GetVersionsAsync(snapshot.GameDirectory, online, cancellationToken);
            if (snapshot != settings.SavedSettings)
            {
                Status = "配置已变化，请重新刷新版本。";
                return;
            }
            AvailableVersions.Clear();
            foreach (var version in versions)
            {
                AvailableVersions.Add(version);
            }
            SelectedGameVersion = AvailableVersions.FirstOrDefault(v => v.Id == previous)
                ?? AvailableVersions.FirstOrDefault(v => v.IsInstalled)
                ?? AvailableVersions.FirstOrDefault();
            Status = versions.Count == 0
                ? "没有本地版本，请检查游戏目录或读取在线列表后下载。"
                : $"共 {versions.Count} 个版本。标记“本地”表示已有版本元数据，启动前仍可检查缺失文件。";
        }
        catch (OperationCanceledException) { Status = "版本读取已取消。"; }
        catch (Exception exception) { Status = $"版本读取失败：{exception.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(CanInstall), IncludeCancelCommand = true)]
    private async Task InstallSelectedAsync(CancellationToken cancellationToken)
    {
        if (settings.SavedSettings is not { } snapshot || SelectedGameVersion is not { } version)
        {
            return;
        }
        downloads.Begin("开始准备游戏文件…", InstallSelectedCommand.Cancel);
        try
        {
            await games.InstallAsync(snapshot, version, downloads.CreateProgress(), cancellationToken);
            downloads.End("下载及校验完成");
            await RefreshAsync(false, cancellationToken);
        }
        catch (OperationCanceledException) { downloads.End("下载已取消，可稍后重新检查并继续。"); }
        catch (Exception exception) { downloads.End($"下载失败：{exception.Message}"); }
        finally
        {
            if (downloads.IsBusy)
            {
                downloads.End("准备已结束");
            }
        }
    }
}
