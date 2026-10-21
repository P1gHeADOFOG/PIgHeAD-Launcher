using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;

namespace PIgHeAD.Launcher.ViewModels;

public partial class DownloadCatalogViewModel : ObservableObject
{
    private readonly IGameService games;
    private readonly ILoaderService loaders;
    private readonly IDesktopService desktop;
    private readonly SettingsViewModel settings;
    private readonly VersionsViewModel versions;
    private readonly DownloadsViewModel downloads;
    public DownloadCatalogViewModel(IGameService games, ILoaderService loaders, IDesktopService desktop,
        SettingsViewModel settings, VersionsViewModel versions, DownloadsViewModel downloads)
    {
        this.games = games; this.loaders = loaders; this.desktop = desktop;
        this.settings = settings; this.versions = versions; this.downloads = downloads;
        downloads.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DownloadsViewModel.IsIdle)) NotifyCommands(); };
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.SavedSettings) or nameof(SettingsViewModel.IsAccountOperationInProgress)) NotifyCommands();
        };
        versions.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VersionsViewModel.IsLoading)) NotifyCommands(); };
    }

    public IReadOnlyList<SettingChoice<DownloadComponent>> Components { get; } =
    [new(DownloadComponent.Vanilla, "Minecraft 原版"), new(DownloadComponent.Forge, "Forge"), new(DownloadComponent.Fabric, "Fabric"),
     new(DownloadComponent.Quilt, "Quilt"), new(DownloadComponent.NeoForge, "NeoForge"), new(DownloadComponent.OptiFine, "OptiFine HD")];
    public ObservableCollection<GameVersionInfo> MinecraftVersions { get; } = [];
    public ObservableCollection<LoaderBuild> Builds { get; } = [];
    [ObservableProperty] public partial DownloadComponent Component { get; set; }
    [ObservableProperty] public partial GameVersionInfo? MinecraftVersion { get; set; }
    [ObservableProperty] public partial LoaderBuild? Build { get; set; }
    [ObservableProperty] public partial string Status { get; private set; } = "先读取官方 Minecraft 列表，再选择组件与兼容版本。各加载器分别安装，不会自动组合。";
    public bool CanEdit => downloads.IsIdle && !settings.IsAccountOperationInProgress && !versions.IsLoading;
    private bool CanLoad() => CanEdit && settings.SavedSettings is not null;
    private bool CanLoadBuilds() => CanLoad() && MinecraftVersion is not null && Component is not (DownloadComponent.Vanilla or DownloadComponent.OptiFine);
    private bool CanInstall() => CanLoad() && MinecraftVersion is not null
        && (Component == DownloadComponent.Vanilla || (Component != DownloadComponent.OptiFine && Build is not null));
    private bool CanRunLocalInstaller() => CanLoad() && Component == DownloadComponent.OptiFine;
    public string ComponentDescription => Component switch
    {
        DownloadComponent.Vanilla => "从官方源下载和校验游戏文件、依赖与 Java。",
        DownloadComponent.Forge => "选择匹配的 Forge 版本自动安装；优先列出推荐版。安装模块可能打开 Forge 的支持页面。",
        DownloadComponent.Fabric => "通过 Fabric 官方 Meta API 读取兼容构建并自动安装。模组通常还需要自行安装对应 Fabric API。",
        DownloadComponent.Quilt => "通过 Quilt 官方 Meta API 读取兼容构建并自动安装。请自行核对所需模组和依赖。",
        DownloadComponent.NeoForge => "从 NeoForged Maven 读取匹配构建并安装；不等同于 Forge，也不会与其他加载器自动叠加。",
        _ => "OptiFine HD：在作者官网选择并下载对应 JAR，再打开本地安装器。请在官方安装窗口核对游戏目录；关闭后刷新版本。此入口不自动组合 Forge / OptiFine。"
    };

    partial void OnComponentChanged(DownloadComponent value)
    {
        Builds.Clear(); Build = null; OnPropertyChanged(nameof(ComponentDescription)); NotifyCommands();
    }
    partial void OnMinecraftVersionChanged(GameVersionInfo? value) { Builds.Clear(); Build = null; NotifyCommands(); }
    partial void OnBuildChanged(LoaderBuild? value) => NotifyCommands();

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit));
        LoadMinecraftCommand.NotifyCanExecuteChanged(); LoadBuildsCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged(); RunOptiFineInstallerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoad), IncludeCancelCommand = true)]
    private async Task LoadMinecraftAsync(CancellationToken cancellationToken)
    {
        var snapshot = settings.SavedSettings!;
        downloads.Begin("读取官方 Minecraft 列表", LoadMinecraftCommand.Cancel);
        try
        {
            var items = await games.GetVersionsAsync(snapshot.GameDirectory, true, cancellationToken);
            var selectedId = MinecraftVersion?.Id ?? versions.SelectedGameVersion?.Id;
            MinecraftVersions.Clear();
            foreach (var item in items.Where(v => v.Type == "release" && Regex.IsMatch(v.Id, @"^\d+\.\d+(?:\.\d+)?$")))
                MinecraftVersions.Add(item);
            MinecraftVersion = MinecraftVersions.FirstOrDefault(v => v.Id == selectedId) ?? MinecraftVersions.FirstOrDefault();
            Status = $"已读取 {MinecraftVersions.Count} 个正式版本。选择组件后可读取对应构建。";
            downloads.End(Status);
        }
        catch (OperationCanceledException) { Status = "版本列表读取已取消。"; downloads.End(Status); }
        catch (Exception exception) { Status = "读取失败：" + exception.Message; downloads.End(Status); }
    }

    [RelayCommand(CanExecute = nameof(CanLoadBuilds), IncludeCancelCommand = true)]
    private async Task LoadBuildsAsync(CancellationToken cancellationToken)
    {
        var selectedComponent = Component;
        var minecraftId = MinecraftVersion!.Id;
        downloads.Begin($"读取 {selectedComponent} 兼容构建", LoadBuildsCommand.Cancel);
        Builds.Clear(); Build = null;
        try
        {
            var items = await loaders.GetBuildsAsync(selectedComponent, minecraftId, cancellationToken);
            foreach (var item in items) Builds.Add(item);
            Build = Builds.FirstOrDefault();
            Status = items.Count == 0 ? "该组件没有适用于所选 Minecraft 的构建，请选择其他版本。" : $"找到 {items.Count} 个兼容构建，请选择后安装。";
            downloads.End(Status);
        }
        catch (OperationCanceledException) { Status = "构建读取已取消。"; downloads.End(Status); }
        catch (Exception exception) { Status = "构建读取失败：" + exception.Message; downloads.End(Status); }
    }

    [RelayCommand(CanExecute = nameof(CanInstall), IncludeCancelCommand = true)]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        var snapshot = settings.SavedSettings!;
        var component = Component; var minecraft = MinecraftVersion!; var build = Build;
        downloads.Begin($"安装 {component} · {minecraft.Id}", InstallCommand.Cancel);
        try
        {
            string installedId;
            if (component == DownloadComponent.Vanilla)
            {
                await games.InstallAsync(snapshot, minecraft, downloads.CreateProgress(), cancellationToken);
                installedId = minecraft.Id;
            }
            else installedId = await loaders.InstallAsync(snapshot, component, minecraft.Id, build!, downloads.CreateProgress(), cancellationToken);
            Status = "安装与校验完成：" + installedId;
            downloads.End(Status);
            await versions.RefreshLocalCommand.ExecuteAsync(null);
            versions.SelectedGameVersion = versions.AvailableVersions.FirstOrDefault(v => v.Id == installedId) ?? versions.SelectedGameVersion;
        }
        catch (OperationCanceledException) { Status = "安装已取消，可重新选择同一版本检查并补全文件。"; downloads.End(Status); }
        catch (Exception exception) { Status = "安装失败：" + exception.Message; downloads.End(Status); }
    }

    [RelayCommand]
    private void OpenOfficialWebsite()
    {
        var url = Component switch
        {
            DownloadComponent.Forge => "https://files.minecraftforge.net/", DownloadComponent.Fabric => "https://fabricmc.net/use/installer/",
            DownloadComponent.Quilt => "https://quiltmc.org/en/install/", DownloadComponent.NeoForge => "https://neoforged.net/",
            DownloadComponent.OptiFine => "https://optifine.net/downloads", _ => "https://www.minecraft.net/download"
        };
        try { desktop.OpenWebsite(url); }
        catch (Exception exception) { Status = "无法打开网站：" + exception.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanRunLocalInstaller))]
    private async Task RunOptiFineInstallerAsync()
    {
        var snapshot = settings.SavedSettings!;
        string? path;
        try { path = desktop.SelectFile("OptiFine 官方安装器|OptiFine_*.jar"); }
        catch (Exception exception) { Status = exception.Message; return; }
        if (path is null) return;
        downloads.Begin("等待 OptiFine 官方安装器完成（请在其窗口操作）");
        try
        {
            await loaders.RunOptiFineInstallerAsync(snapshot, path, downloads.CreateProgress(), CancellationToken.None);
            Status = "OptiFine 安装器已退出，请确认结果并刷新本地版本；退出不代表已安装成功。";
            downloads.End(Status);
            await versions.RefreshLocalCommand.ExecuteAsync(null);
        }
        catch (Exception exception) { Status = "安装器未完成：" + exception.Message; downloads.End(Status); }
    }
}
