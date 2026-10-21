using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService settingsService;
    private readonly IDesktopService? desktop;
    private readonly DownloadsViewModel? operations;

    public SettingsViewModel(ISettingsService settingsService, IDesktopService? desktop = null, DownloadsViewModel? operations = null)
    {
        this.settingsService = settingsService;
        this.desktop = desktop;
        this.operations = operations;
        if (operations is not null) operations.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadsViewModel.IsIdle)) NotifyAvailability();
        };
    }

    public IReadOnlyList<SettingChoice<GameWindowMode>> WindowModes { get; } =
        [new(GameWindowMode.Windowed, "窗口化"), new(GameWindowMode.Fullscreen, "全屏"), new(GameWindowMode.Maximized, "最大化窗口")];
    public IReadOnlyList<SettingChoice<GamePriority>> Priorities { get; } =
        [new(GamePriority.BelowNormal, "低于正常"), new(GamePriority.Normal, "正常（推荐）"), new(GamePriority.AboveNormal, "高于正常"), new(GamePriority.High, "高")];
    public IReadOnlyList<SettingChoice<GameLanguage>> Languages { get; } =
        [new(GameLanguage.KeepExisting, "保留游戏设置"), new(GameLanguage.FollowSystem, "每次启动跟随系统语言"),
         new(GameLanguage.SimplifiedChinese, "简体中文"), new(GameLanguage.TraditionalChinese, "繁体中文"), new(GameLanguage.English, "English")];

    public string SettingsFilePath => settingsService.SettingsFilePath;
    public string VersionDisplay => LauncherVersion.Display;

    [ObservableProperty]
    public partial string GameDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string JavaPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaximumMemoryMb { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MicrosoftClientId { get; set; } = string.Empty;

    [ObservableProperty] public partial GameWindowMode WindowMode { get; set; }
    [ObservableProperty] public partial string WindowWidth { get; set; } = "1280";
    [ObservableProperty] public partial string WindowHeight { get; set; } = "720";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditManualMemory))]
    public partial bool AutomaticMemory { get; set; }
    public bool CanEditManualMemory => !AutomaticMemory;
    [ObservableProperty] public partial bool IsolateVersions { get; set; }
    [ObservableProperty] public partial string ExtraJvmArguments { get; set; } = "";
    [ObservableProperty] public partial string ExtraGameArguments { get; set; } = "";
    [ObservableProperty] public partial GamePriority ProcessPriority { get; set; } = GamePriority.Normal;
    [ObservableProperty] public partial GameLanguage Language { get; set; }
    [ObservableProperty] public partial bool EnableAnimations { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAccountOperationInProgress { get; set; }
    partial void OnIsAccountOperationInProgressChanged(bool value) => NotifyAvailability();

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanEdit));
        SaveCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
        ResetDefaultsCommand.NotifyCanExecuteChanged();
        BrowseDirectoryCommand.NotifyCanExecuteChanged();
        BrowseJavaCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "正在读取配置…";

    [ObservableProperty]
    public partial bool HasError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(LoadCommand))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    public partial LauncherSettings? SavedSettings { get; private set; }

    public bool CanEdit => IsLoaded && CanLoad();

    private bool CanSave() => CanEdit;

    private bool CanLoad() => !IsBusy && operations?.IsIdle != false && !IsAccountOperationInProgress;
    private bool CanBrowse() => CanEdit && desktop is not null;
    partial void OnIsBusyChanged(bool value) => NotifyAvailability();
    partial void OnIsLoadedChanged(bool value) => NotifyAvailability();

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private void BrowseDirectory()
    {
        try { if (desktop?.SelectFolder(GameDirectory) is { } path) GameDirectory = path; }
        catch (Exception exception) { HasError = true; StatusMessage = exception.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private void BrowseJava()
    {
        try { if (desktop?.SelectFile("Java 可执行文件|java.exe;javaw.exe|可执行文件|*.exe") is { } path) JavaPath = path; }
        catch (Exception exception) { HasError = true; StatusMessage = exception.Message; }
    }

    [RelayCommand]
    private void ShowLegalDocuments()
    {
        try { desktop?.ShowLegalDocuments(); }
        catch (Exception exception) { HasError = true; StatusMessage = exception.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void ResetDefaults()
    {
        LoadDraft(new LauncherSettings());
        StatusMessage = "已恢复默认草稿；点击保存后生效。原游戏文件不会移动或删除。";
        HasError = false;
    }

    private void LoadDraft(LauncherSettings saved)
    {
        GameDirectory = saved.GameDirectory; JavaPath = saved.JavaPath; MicrosoftClientId = saved.MicrosoftClientId;
        MaximumMemoryMb = saved.MaximumMemoryMb.ToString(CultureInfo.InvariantCulture);
        WindowMode = saved.WindowMode; WindowWidth = saved.WindowWidth.ToString(CultureInfo.InvariantCulture);
        WindowHeight = saved.WindowHeight.ToString(CultureInfo.InvariantCulture); AutomaticMemory = saved.AutomaticMemory;
        IsolateVersions = saved.IsolateVersions; ExtraJvmArguments = saved.ExtraJvmArguments;
        ExtraGameArguments = saved.ExtraGameArguments; ProcessPriority = saved.ProcessPriority;
        Language = saved.Language; EnableAnimations = saved.EnableAnimations;
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        IsLoaded = false;
        HasError = false;
        StatusMessage = "正在读取配置…";

        try
        {
            var result = await settingsService.LoadAsync(cancellationToken);
            LoadDraft(result.Settings);
            SavedSettings = result.Settings;
            IsLoaded = true;
            HasError = result.BackupFilePath is not null;
            StatusMessage = result.BackupFilePath is null
                ? "配置已就绪。修改后请保存。"
                : $"原配置损坏，已加载默认设置。原文件备份：{result.BackupFilePath}。保存后将替换原配置。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "读取已取消。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HasError = true;
            StatusMessage = $"无法读取配置：{exception.Message} 请检查文件访问权限后重新读取。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        HasError = false;

        try
        {
            if (!int.TryParse(MaximumMemoryMb, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory))
            {
                if (!AutomaticMemory) throw new ValidationException("最大内存必须是以 MiB 为单位的整数。");
                memory = SavedSettings?.MaximumMemoryMb ?? 4096;
            }
            if (AutomaticMemory && memory is < LauncherSettings.MinimumMemoryMb or > LauncherSettings.MaximumSupportedMemoryMb)
                memory = SavedSettings?.MaximumMemoryMb ?? 4096;

            var settings = new LauncherSettings
            {
                GameDirectory = GameDirectory.Trim(),
                JavaPath = JavaPath.Trim(),
                MicrosoftClientId = MicrosoftClientId.Trim(),
                MaximumMemoryMb = memory,
                WindowMode = WindowMode,
                WindowWidth = int.TryParse(WindowWidth, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ? width : 0,
                WindowHeight = int.TryParse(WindowHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ? height : 0,
                AutomaticMemory = AutomaticMemory, IsolateVersions = IsolateVersions,
                ExtraJvmArguments = ExtraJvmArguments.Trim(), ExtraGameArguments = ExtraGameArguments.Trim(),
                ProcessPriority = ProcessPriority, Language = Language, EnableAnimations = EnableAnimations
            };
            settings.Validate();
            settings = settings with { GameDirectory = GameRootLocator.Resolve(settings.GameDirectory) };
            if (string.Equals(settings.GameDirectory, SavedSettings?.GameDirectory, StringComparison.OrdinalIgnoreCase))
                settings = settings with { PreferredVersionId = SavedSettings?.PreferredVersionId ?? "" };

            await settingsService.SaveAsync(settings, cancellationToken);
            GameDirectory = settings.GameDirectory;
            JavaPath = settings.JavaPath;
            SavedSettings = settings;
            StatusMessage = "设置已保存。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "保存已取消。";
        }
        catch (Exception exception) when (exception is ValidationException or IOException or UnauthorizedAccessException)
        {
            HasError = true;
            StatusMessage = $"未保存：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ActivateImportedGameAsync(GameImportResult result, CancellationToken cancellationToken)
    {
        if (SavedSettings is null) throw new InvalidOperationException("请先读取启动器设置。");
        // Preserve the user's unsaved form; only the saved active game root changes here.
        var updated = SavedSettings with { GameDirectory = result.GameDirectory, PreferredVersionId = result.SelectedVersionId };
        updated.Validate();
        await settingsService.SaveAsync(updated, cancellationToken);
        GameDirectory = updated.GameDirectory;
        SavedSettings = updated;
        StatusMessage = "已启用导入的游戏目录；其他设置草稿保持不变。";
    }
}
