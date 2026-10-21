using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PIgHeAD.Launcher.Bootstrap;
using PIgHeAD.Launcher.Services.Consent;
using PIgHeAD.Launcher.ViewModels;
using PIgHeAD.Launcher.Views;
using PIgHeAD.Launcher.Views.Controls;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Tests;

public sealed class WpfCompositionTests
{
    [Fact]
    public void CompiledViewsLoadResourcesAndResolveNavigationBindings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var originalLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
            using var messages = new StringWriter();
            using var listener = new TextWriterTraceListener(messages);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            try
            {
                var application = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var window = AppBootstrapper.CreateMainWindow();
                var main = Assert.IsType<MainViewModel>(window.DataContext);
                var root = Assert.IsType<Grid>(window.Content);
                Assert.Equal("PIgHeAD - 猪头启动器", window.Title);
                Assert.Equal("v0.1.4beta", LauncherVersion.Display);
                Assert.Equal("0.1.4.0", typeof(App).Assembly.GetName().Version!.ToString());
                Assert.True(window.AllowDrop);
                Assert.Same(main.Home, main.CurrentPage);
                var navigation = Assert.IsType<ListBox>(window.FindName("PrimaryNavigation"));
                Assert.Equal(new[] { "Home", "Versions", "Downloads", "Accounts", "Settings" },
                    main.NavigationItems.Select(item => item.Title));

                Layout(root);
                var home = FindDescendant<HomeView>(root);
                Assert.NotNull(home);
                Assert.Same(main.Home, home.DataContext);
                var brand = Assert.Single(Descendants<TextBlock>(home), block => block.Text == "PIgHeAD");
                var textBrush = Assert.IsType<SolidColorBrush>(window.FindResource("TextBrush"));
                Assert.Equal(textBrush.Color, Assert.IsType<SolidColorBrush>(brand.Foreground).Color);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.GameDirectory);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.SelectedVersion);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.AccountName);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.JavaStatus);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.MemorySummary);
                Assert.Contains(Descendants<TextBlock>(home), block => block.Text == main.Home.DownloadStatus);
                SavePreview(root, "home.png");

                Layout(root, window.MinWidth, window.MinHeight);
                var status = Assert.IsType<Border>(home.FindName("HomeStatusArea"));
                var statusOrigin = status.TranslatePoint(new Point(), root);
                Assert.InRange(statusOrigin.Y + status.ActualHeight, 0, root.ActualHeight);
                SavePreview(root, "home-compact.png");

                var launch = Assert.Single(Descendants<Button>(home),
                    button => AutomationProperties.GetName(button) == "Launch Game");
                Assert.False(launch.IsEnabled);
                Assert.IsType<BitmapImage>(window.FindResource("PigBrandImage"));

                var versionsLink = Assert.Single(Descendants<Button>(home),
                    button => Equals(button.Content, "切换版本  →"));
                Assert.True(InteractionAnimation.GetEnabled(versionsLink));
                InteractionAnimation.SetAnimationsEnabled(versionsLink, false);
                versionsLink.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var transforms = Assert.IsType<TransformGroup>(versionsLink.RenderTransform);
                Assert.False(Assert.Single(transforms.Children.OfType<ScaleTransform>()).HasAnimatedProperties);
                versionsLink.Command.Execute(null);
                Assert.Same(main.Versions, main.CurrentPage);

                foreach (var (page, viewType) in new (ObservableObject Page, Type ViewType)[]
                         { (main.Versions, typeof(VersionsView)), (main.Downloads, typeof(DownloadsView)), (main.Accounts, typeof(AccountsView)) })
                {
                    navigation.SelectedItem = main.NavigationItems.Single(item => ReferenceEquals(item.Page, page));
                    Layout(root);
                    var preview = Descendants<UserControl>(root).SingleOrDefault(view => view.GetType() == viewType);
                    Assert.NotNull(preview);
                    Assert.Same(page, preview.DataContext);
                    SavePreview(root, viewType.Name + ".png");
                }

                main.ShowSettingsCommand.Execute(null);
                Layout(root);
                var settings = FindDescendant<SettingsView>(root);
                Assert.NotNull(settings);
                Assert.Same(main.Settings, settings.DataContext);
                var save = Assert.Single(Descendants<Button>(settings), button => Equals(button.Content, "保存设置"));
                Assert.Same(main.Settings.SaveCommand, save.Command);
                Assert.False(save.IsEnabled);
                Assert.IsType<SolidColorBrush>(settings.FindResource("AccentBrush"));
                SavePreview(root, "settings.png");
                var consentViewModel = new ConsentViewModel(new UserConsentService());
                var consent = new ConsentWindow(consentViewModel);
                var consentRoot = Assert.IsType<Grid>(consent.Content);
                consent.Content = null;
                var consentPreview = new Border
                {
                    Background = (Brush)consent.FindResource("BackgroundBrush"), Child = consentRoot, DataContext = consentViewModel
                };
                TextElement.SetForeground(consentPreview, consent.Foreground);
                TextElement.SetFontFamily(consentPreview, consent.FontFamily);
                TextElement.SetFontSize(consentPreview, consent.FontSize);
                Layout(consentPreview, 850, 740);
                Assert.Equal(2, Descendants<CheckBox>(consentRoot).Count());
                Assert.All(Descendants<CheckBox>(consentRoot), checkbox => Assert.False(checkbox.IsChecked));
                Assert.False(consentViewModel.AcceptCommand.CanExecute(null));
                var accept = Assert.Single(Descendants<Button>(consentRoot), button => Equals(button.Content, "同意并继续"));
                Assert.False(accept.IsEnabled);
                Assert.Contains(Descendants<TextBox>(consentRoot), box => box.Text == UserConsentService.AgreementText);
                Assert.InRange(accept.TranslatePoint(new Point(), consentPreview).Y + accept.ActualHeight, 0, 740);
                SavePreview(consentPreview, "consent.png");
                consent.Close();
                listener.Flush();
                Assert.True(string.IsNullOrEmpty(messages.ToString()), messages.ToString());

                window.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = originalLevel;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF composition verification timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void Layout(FrameworkElement root, double width = 1280, double height = 820)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static void SavePreview(FrameworkElement root, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("PIGHEAD_TEST_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, fileName));
        encoder.Save(stream);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject =>
        Descendants<T>(root).FirstOrDefault();

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
