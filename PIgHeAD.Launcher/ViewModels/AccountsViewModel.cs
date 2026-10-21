using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Accounts;

namespace PIgHeAD.Launcher.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly IAccountService accounts;
    private readonly SettingsViewModel settings;
    private readonly DownloadsViewModel downloads;
    private string? sessionClientId;

    public AccountsViewModel(IAccountService accounts, SettingsViewModel settings, DownloadsViewModel downloads)
    {
        this.accounts = accounts;
        this.settings = settings;
        this.downloads = downloads;
        downloads.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadsViewModel.IsIdle))
            {
                NotifyCommands();
            }
        };
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SavedSettings))
            {
                OnPropertyChanged(nameof(ClientIdStatus));
                OnPropertyChanged(nameof(MinecraftApiStatus));
                if (sessionClientId != settings.SavedSettings?.MicrosoftClientId)
                {
                    MicrosoftSession = null;
                    UseMicrosoft = false;
                }
            }
            if (e.PropertyName == nameof(SettingsViewModel.SavedSettings))
            {
                NotifyCommands();
            }
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string OfflineName { get; set; } = "Player";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName), nameof(Description))]
    public partial bool UseMicrosoft { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName), nameof(Description))]
    public partial GameSession? MicrosoftSession { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignOutCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = "可使用离线玩家名，或登录拥有 Minecraft Java 的 Microsoft 账户。";

    public string DisplayName => UseMicrosoft ? MicrosoftSession?.UserName ?? "Microsoft 未登录" : OfflineName + " · 离线";
    public string Description => UseMicrosoft
        ? MicrosoftSession is null ? "Microsoft 身份尚未认证 · 请完成 Xbox 与 Minecraft 验证" : "已验证的 Minecraft Java 正版账户"
        : "离线身份 · 不提供正版服务器认证";
    public string ClientIdStatus => string.IsNullOrEmpty(settings.SavedSettings?.MicrosoftClientId)
        ? "尚未配置 Client ID。请在 Settings 中填写你自己的公开桌面应用 ID。"
        : "已配置公开客户端 · 系统浏览器登录 · PKCE";

    public string MinecraftApiStatus
    {
        get
        {
            if (!Guid.TryParse(settings.SavedSettings?.MicrosoftClientId, out var id))
            {
                return "Minecraft API：尚未配置应用。";
            }
            // Project-owner status as of 2026-10-21; this is a notice, not an API permission grant.
            return id == Guid.Parse(LauncherSettings.DefaultMicrosoftClientId)
                ? "Minecraft API：根据当前项目记录，此应用尚未提交审核，尚未获批。Microsoft OAuth 注册不授予 Minecraft API 访问权限。"
                : "Minecraft API：此应用的审核状态尚未验证。配置 Client ID 不代表已获批。";
        }
    }

    private bool CanChangeAccount() => !IsBusy && downloads.IsIdle && settings.SavedSettings is not null;
    public bool CanEdit => CanChangeAccount();

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit));
        SignInCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        settings.IsAccountOperationInProgress = value;
        OnPropertyChanged(nameof(CanEdit));
    }

    [RelayCommand(CanExecute = nameof(CanChangeAccount), IncludeCancelCommand = true)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        Status = "等待系统浏览器完成 Microsoft 登录…";
        try
        {
            var clientId = settings.SavedSettings?.MicrosoftClientId ?? "";
            var session = await accounts.SignInAsync(clientId, cancellationToken);
            if (clientId != settings.SavedSettings?.MicrosoftClientId)
            {
                throw new InvalidOperationException("登录期间 Client ID 已变化，请重新登录。");
            }
            MicrosoftSession = session;
            sessionClientId = clientId;
            UseMicrosoft = true;
            Status = $"已验证 Minecraft Java 账户：{session.UserName}";
        }
        catch (OperationCanceledException) { Status = "登录已取消。"; }
        catch (Exception exception) { Status = $"登录未完成：{exception.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var clientId = settings.SavedSettings?.MicrosoftClientId ?? "";
            var session = await accounts.RestoreAsync(clientId, cancellationToken);
            if (session is not null && clientId == settings.SavedSettings?.MicrosoftClientId)
            {
                MicrosoftSession = session;
                sessionClientId = clientId;
                UseMicrosoft = true;
                Status = $"已恢复并验证账户：{session.UserName}";
            }
        }
        catch (OperationCanceledException) { Status = "账户恢复已取消。"; }
        catch (Exception exception) { Status = $"账户未恢复，请重新登录：{exception.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task<GameSession> GetLaunchSessionAsync(CancellationToken cancellationToken)
    {
        if (!UseMicrosoft)
        {
            return OfflineSessionFactory.Create(OfflineName.Trim());
        }
        var session = await accounts.RestoreAsync(settings.SavedSettings?.MicrosoftClientId ?? "", cancellationToken);
        if (session is null)
        {
            MicrosoftSession = null;
            throw new InvalidOperationException("Microsoft 登录已失效或尚未完成，请前往 Accounts 重新登录。");
        }
        MicrosoftSession = session;
        return session;
    }

    [RelayCommand(CanExecute = nameof(CanChangeAccount))]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            await accounts.SignOutAsync(sessionClientId ?? settings.SavedSettings?.MicrosoftClientId ?? "", CancellationToken.None);
            MicrosoftSession = null;
            UseMicrosoft = false;
            Status = "已清除此启动器的登录缓存。浏览器账户状态不受影响。";
        }
        catch (Exception exception) { Status = $"退出失败：{exception.Message}"; }
        finally { IsBusy = false; }
    }
}
