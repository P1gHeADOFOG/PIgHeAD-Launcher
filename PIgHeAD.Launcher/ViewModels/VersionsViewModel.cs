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
    private readonly ILocalVersionService localVersions;
    private int detailsGeneration;
    private string? configuredDirectory;
    public SettingsViewModel Settings => settings;

    public VersionsViewModel(IGameService games, SettingsViewModel settings, DownloadsViewModel downloads,
        IGameImportService? imports = null, IDesktopService? desktop = null, ILocalVersionService? localVersions = null)
    {
        this.games = games;
        this.settings = settings;
        this.downloads = downloads;
        this.imports = imports;
        this.desktop = desktop;
        this.localVersions = localVersions ?? new Services.Minecraft.LocalVersionService();
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
            if (e.PropertyName == nameof(SettingsViewModel.SelectedGameDirectory)) SwitchDirectoryCommand.NotifyCanExecuteChanged();
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
    [NotifyCanExecuteChangedFor(nameof(RepairSelectedCommand))]
    public partial GameVersionInfo? SelectedGameVersion { get; set; }

    partial void OnSelectedGameVersionChanged(GameVersionInfo? value)
    {
        detailsGeneration++;
        Details = null;
        Mods.Clear();
        IsModsVisible = false;
        IsDeleteConfirmationVisible = false;
        NotifyCommands();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails), nameof(CanViewMods))]
    public partial LocalVersionDetails? Details { get; private set; }
    partial void OnDetailsChanged(LocalVersionDetails? value) => NotifyCommands();
    public bool HasDetails => Details is not null;
    public bool CanViewMods => Details?.SupportsMods == true;
    public ObservableCollection<LocalModInfo> Mods { get; } = [];
    [ObservableProperty] public partial bool IsModsVisible { get; private set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteCommand))]
    public partial bool IsDeleteConfirmationVisible { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = "正在等待配置…";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshLocalCommand),
        nameof(RepairSelectedCommand), nameof(ImportArchiveCommand), nameof(ImportDirectoryCommand))]
    public partial bool IsLoading { get; private set; }

    private bool CanRefresh() => settings.SavedSettings is not null && downloads.IsIdle && !IsLoading
        && !settings.IsAccountOperationInProgress && !settings.IsBusy;
    private bool CanRepair() => CanRefresh() && SelectedGameVersion?.IsInstalled == true;
    private bool CanManageVersion(GameVersionInfo? version) => CanRefresh() && version?.IsInstalled == true;
    private bool CanReadMods() => CanRefresh() && CanViewMods;
    private bool CanConfirmDelete() => CanRefresh() && IsDeleteConfirmationVisible && Details?.VersionId == SelectedGameVersion?.Id;
    public bool CanImport => CanRefresh() && imports is not null;
    private bool CanPickImport() => CanImport && desktop is not null;
    [ObservableProperty] public partial bool CompleteImportedFiles { get; set; } = true;
    [ObservableProperty] public partial bool IsDragOver { get; set; }
    partial void OnIsLoadingChanged(bool value) => NotifyCommands();

    private void NotifyCommands()
    {
        RefreshLocalCommand.NotifyCanExecuteChanged();
        ShowDetailsCommand.NotifyCanExecuteChanged();
        RequestDeleteCommand.NotifyCanExecuteChanged();
        ConfirmDeleteCommand.NotifyCanExecuteChanged();
        CancelDeleteCommand.NotifyCanExecuteChanged();
        ViewModsCommand.NotifyCanExecuteChanged();
        RepairSelectedCommand.NotifyCanExecuteChanged();
        ImportArchiveCommand.NotifyCanExecuteChanged();
        ImportDirectoryCommand.NotifyCanExecuteChanged();
        ImportPathCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanImport));
        SwitchDirectoryCommand.NotifyCanExecuteChanged();
    }

    private bool CanSwitchDirectory() => CanRefresh() && settings.CanSwitchGameDirectory;

    [RelayCommand(CanExecute = nameof(CanSwitchDirectory))]
    private async Task SwitchDirectoryAsync()
    {
        await settings.SwitchGameDirectoryCommand.ExecuteAsync(null);
        if (!settings.HasError) await RefreshAsync(CancellationToken.None);
        else Status = settings.StatusMessage;
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
            await RefreshAsync(CancellationToken.None);
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
    private Task RefreshLocalAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (settings.SavedSettings is not { } snapshot)
        {
            return;
        }
        IsLoading = true;
        Status = "正在扫描本地版本…";
        var previous = SelectedGameVersion?.Id ?? snapshot.PreferredVersionId;
        try
        {
            var versions = (await games.GetVersionsAsync(snapshot.GameDirectory, false, cancellationToken)).Where(version => version.IsInstalled).ToArray();
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
            Status = versions.Length == 0
                ? "没有本地版本，可接入已有目录或前往下载中心安装。"
                : $"共 {versions.Length} 个本地版本，可查看详情、修复或删除。";
        }
        catch (OperationCanceledException) { Status = "版本读取已取消。"; }
        catch (Exception exception) { Status = $"版本读取失败：{exception.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(CanManageVersion))]
    private Task ShowDetailsAsync(GameVersionInfo version, CancellationToken cancellationToken) => LoadDetailsAsync(version, cancellationToken);

    private async Task LoadDetailsAsync(GameVersionInfo version, CancellationToken token)
    {
        if (settings.SavedSettings is not { } snapshot || !version.IsInstalled) return;
        SelectedGameVersion = version;
        var generation = ++detailsGeneration;
        IsLoading = true;
        Details = null;
        IsModsVisible = false;
        IsDeleteConfirmationVisible = false;
        Mods.Clear();
        try
        {
            var details = await localVersions.GetDetailsAsync(snapshot, version, token);
            if (generation == detailsGeneration && snapshot.GameDirectory == settings.SavedSettings?.GameDirectory)
            {
                Details = details;
                Status = "本地版本详情已就绪。";
            }
        }
        catch (OperationCanceledException) { Status = "详情读取已取消。"; }
        catch (Exception error) { Status = "无法读取版本详情：" + error.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(CanReadMods))]
    private async Task ViewModsAsync(CancellationToken token)
    {
        var details = Details!;
        var generation = detailsGeneration;
        IsLoading = true;
        Mods.Clear();
        IsModsVisible = false;
        try
        {
            var mods = await localVersions.GetModsAsync(details, token);
            if (generation != detailsGeneration || Details != details) return;
            foreach (var mod in mods) Mods.Add(mod);
            IsModsVisible = true;
            Status = $"已读取 {mods.Count} 个本地模组条目。";
        }
        catch (OperationCanceledException) { Status = "模组读取已取消。"; }
        catch (Exception error) { Status = "无法读取模组列表：" + error.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand(CanExecute = nameof(CanManageVersion))]
    private async Task RequestDeleteAsync(GameVersionInfo version, CancellationToken token)
    {
        await LoadDetailsAsync(version, token);
        if (Details?.VersionId == version.Id) IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    private void ReturnToDetails() => IsModsVisible = false;

    private bool CanCancelDelete() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanCancelDelete))]
    private void CancelDelete() => IsDeleteConfirmationVisible = false;

    [RelayCommand]
    private void CloseDetails()
    {
        detailsGeneration++;
        Details = null;
        Mods.Clear();
        IsModsVisible = false;
        IsDeleteConfirmationVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDelete))]
    private async Task ConfirmDeleteAsync(CancellationToken token)
    {
        if (!CanConfirmDelete() || settings.SavedSettings is not { } snapshot || SelectedGameVersion is not { } version) return;
        IsLoading = true;
        downloads.Begin("删除本地版本");
        try
        {
            await localVersions.DeleteAsync(snapshot, version, token);
            CloseDetails();
            downloads.End("版本已移入回收站。");
            await RefreshAsync(CancellationToken.None);
            Status = "版本已移入回收站。共享资源与其他实例目录保留。";
        }
        catch (OperationCanceledException) { Status = "删除已取消。"; downloads.End(Status); }
        catch (Exception error) { Status = "无法删除版本：" + error.Message; downloads.End(Status); }
        finally { IsLoading = false; }
    }
}
