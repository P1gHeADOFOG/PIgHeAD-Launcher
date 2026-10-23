using System.Windows.Controls;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is DownloadsViewModel { Catalog: { } catalog }) await catalog.EnsureLoadedAsync();
        };
    }
}
