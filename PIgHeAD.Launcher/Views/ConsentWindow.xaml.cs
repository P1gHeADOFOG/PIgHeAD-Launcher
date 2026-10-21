using System.ComponentModel;
using System.Windows;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Views;

public partial class ConsentWindow : Window
{
    public ConsentWindow(ConsentViewModel viewModel, bool readOnly = false)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (readOnly) ConsentActions.Visibility = Visibility.Collapsed;
        viewModel.CloseRequested += accepted => DialogResult = accepted;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is ConsentViewModel { IsBusy: true } && DialogResult is null) e.Cancel = true;
        base.OnClosing(e);
    }
}
