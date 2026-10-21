using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private Action? cancelCurrent;
    private string operationName = "";
    private long generation;
    public DownloadCatalogViewModel? Catalog { get; set; }
    public ObservableCollection<DownloadHistoryItem> History { get; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(CancelCurrentCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsGameRunning { get; set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = "空闲";

    [ObservableProperty]
    public partial double Percentage { get; private set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string LogFilePath { get; set; } = string.Empty;

    public bool IsIdle => !IsBusy && !IsGameRunning;

    public void Begin(string message, Action? cancel = null)
    {
        if (!IsIdle) throw new InvalidOperationException("请先等待当前操作或游戏结束。");
        generation++;
        cancelCurrent = cancel;
        operationName = message;
        IsBusy = true;
        Percentage = 0;
        IsIndeterminate = true;
        Status = message;
        CancelCurrentCommand.NotifyCanExecuteChanged();
    }

    public IProgress<GameProgress> CreateProgress()
    {
        var currentGeneration = generation;
        return new Progress<GameProgress>(value => { if (currentGeneration == generation) Report(value); });
    }

    private bool CanCancel() => IsBusy && cancelCurrent is not null;
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelCurrent()
    {
        cancelCurrent?.Invoke();
        Status = "正在取消，请等待当前文件操作结束…";
    }

    public void Report(GameProgress progress)
    {
        if (!IsBusy)
        {
            return;
        }
        Status = progress.Message;
        IsIndeterminate = progress.Percentage is null;
        Percentage = progress.Percentage ?? 0;
    }

    public void End(string status)
    {
        if (IsBusy)
        {
            History.Insert(0, new DownloadHistoryItem(operationName, status, DateTimeOffset.Now));
            while (History.Count > 30) History.RemoveAt(History.Count - 1);
        }
        generation++;
        cancelCurrent = null;
        IsBusy = false;
        IsIndeterminate = false;
        Status = status;
        CancelCurrentCommand.NotifyCanExecuteChanged();
    }
}
