using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PIgHeAD.Launcher.Services.Consent;

namespace PIgHeAD.Launcher.ViewModels;

public partial class ConsentViewModel(UserConsentService service) : ObservableObject
{
    public string AgreementText => UserConsentService.AgreementText;
    public string DisclaimerText => UserConsentService.DisclaimerText;
    public event Action<bool>? CloseRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial bool AgreementAccepted { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial bool DisclaimerAccepted { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand), nameof(DeclineCommand))]
    public partial bool IsBusy { get; private set; }
    [ObservableProperty]
    public partial string Status { get; private set; } = "两项均未默认勾选。您可以阅读后自愿同意，或退出启动器。";

    private bool CanAccept() => AgreementAccepted && DisclaimerAccepted && !IsBusy;
    private bool CanDecline() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private async Task AcceptAsync()
    {
        IsBusy = true;
        try
        {
            await service.AcceptAsync(AgreementAccepted, DisclaimerAccepted, CancellationToken.None);
            CloseRequested?.Invoke(true);
        }
        catch (Exception exception) { Status = $"无法保存同意记录：{exception.Message}。请重试或退出。"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanDecline))]
    private void Decline() => CloseRequested?.Invoke(false);
}
