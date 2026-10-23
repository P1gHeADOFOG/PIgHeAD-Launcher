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
using PIgHeAD.Launcher.Services.Presentation;

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
                var application = new CompositionApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var resource in new[] { "Colors", "Icons", "Artwork", "Controls" })
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/PIgHeAD.Launcher;component/Resources/Styles/{resource}.xaml", UriKind.Relative)
                    });
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var window = AppBootstrapper.CreateMainWindow();
                var main = Assert.IsType<MainViewModel>(window.DataContext);
                var root = Assert.IsType<Grid>(window.Content);
                Assert.Equal("PIgHeAD - 猪头启动器", window.Title);
                Assert.Equal("beta v0.2.2", LauncherVersion.Display);
                Assert.Equal("0.2.2-beta", LauncherVersion.Value);
                Assert.Equal("0.2.2.0", typeof(App).Assembly.GetName().Version!.ToString());
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
                var textBrush = Assert.IsType<SolidColorBrush>(window.FindResource("HeroTextBrush"));
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
                Assert.Empty(Descendants<ScrollViewer>(home));

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

                PresentationSettings.Current.ToggleLanguageCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                PresentationSettings.Current.ToggleThemeCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Equal("Light", PresentationSettings.Current.Theme);
                Assert.Equal(Color.FromRgb(0xF4, 0xF6, 0xFA), ((SolidColorBrush)window.FindResource("BackgroundBrush")).Color);
                foreach (var item in main.NavigationItems)
                {
                    main.SelectedNavigationItem = item;
                    Layout(root, window.MinWidth, window.MinHeight);
                    Assert.DoesNotContain(Descendants<TextBlock>(root), block => block.Text != "中文"
                        && block.Text.Any(c => c is >= '\u4e00' and <= '\u9fff'));
                    SavePreview(root, item.Title.ToLowerInvariant() + "-light-en.png");
                }

                var context = new LauncherTestContext();
                context.Main.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                var localService = new LocalVersionViewModelTests.FakeLocalVersions();
                var localVersions = new VersionsViewModel(context.GameService, context.Settings, context.Downloads, localVersions: localService);
                localVersions.RefreshLocalCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                var modded = new GameVersionInfo("modded", "release", true);
                localVersions.AvailableVersions.Add(modded);
                var localMain = new MainViewModel(context.Home, context.Settings, localVersions, context.Accounts, context.Downloads);
                var localWindow = new MainWindow(localMain);
                var localRoot = (Grid)localWindow.Content;
                localMain.ShowVersionsCommand.Execute(null);
                Layout(localRoot, 1180, 780);
                var localView = FindDescendant<VersionsView>(localRoot)!;
                Assert.DoesNotContain(Descendants<Button>(localView), button => Equals(button.Content, "Online vanilla catalog") || Equals(button.Content, "Download selected version"));
                var check = Assert.Single(Descendants<CheckBox>(localView));
                check.IsChecked = true;
                Layout(localRoot, 1180, 780);
                var checkSurface = Assert.IsType<Border>(check.Template.FindName("CheckSurface", check));
                Assert.Equal(new CornerRadius(5), checkSurface.CornerRadius);
                Assert.Equal(Visibility.Visible, ((UIElement)check.Template.FindName("CheckMark", check)).Visibility);
                check.IsChecked = false;
                Layout(localRoot, 1180, 780);
                Assert.Equal(Visibility.Collapsed, ((UIElement)check.Template.FindName("CheckMark", check)).Visibility);
                check.IsChecked = null;
                Layout(localRoot, 1180, 780);
                Assert.Equal(Visibility.Visible, ((UIElement)check.Template.FindName("MixedMark", check)).Visibility);
                check.IsChecked = true;
                localVersions.ShowDetailsCommand.ExecuteAsync(localVersions.AvailableVersions[0]).GetAwaiter().GetResult();
                Layout(localRoot, 1180, 780);
                Assert.Equal(Visibility.Collapsed, ((Button)localView.FindName("ViewModsEntry")).Visibility);
                localVersions.ShowDetailsCommand.ExecuteAsync(modded).GetAwaiter().GetResult();
                Layout(localRoot, 1180, 780);
                Assert.Equal(Visibility.Visible, ((Button)localView.FindName("ViewModsEntry")).Visibility);
                SavePreview(localRoot, "version-basic-details.png");
                localVersions.ViewModsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Layout(localRoot, 1180, 780);
                Assert.Equal(Visibility.Collapsed, ((Button)localView.FindName("ViewModsEntry")).Visibility);
                Assert.Contains(Descendants<TextBlock>(localView), block => block.Text == "example.jar");
                Assert.DoesNotContain(Descendants<TextBlock>(localView), block => block.Text.Any(c => c is >= '\u4e00' and <= '\u9fff'));
                SavePreview(localRoot, "version-details-light-en.png");
                localVersions.RequestDeleteCommand.ExecuteAsync(modded).GetAwaiter().GetResult();
                Layout(localRoot, 1180, 780);
                Assert.Equal(0, localService.DeleteCalls);
                var confirmDeletion = Assert.Single(Descendants<Button>(localView), button => Equals(button.Content, "Confirm deletion"));
                Assert.InRange(confirmDeletion.TranslatePoint(new Point(), localRoot).Y + confirmDeletion.ActualHeight, 0, localRoot.ActualHeight);
                SavePreview(localRoot, "version-delete-confirmation.png");
                localVersions.CancelDeleteCommand.Execute(null);
                var goToDownloads = Assert.Single(Descendants<Button>(localView), button => Equals(button.Content, "Go to Downloads  →"));
                goToDownloads.Command.Execute(null);
                Assert.Same(context.Downloads, localMain.CurrentPage);
                localWindow.Close();

                main.ShowHomeCommand.Execute(null);
                Layout(root);
                home = FindDescendant<HomeView>(root)!;
                Assert.Same(window.FindResource("OverworldDayArtwork"), ((ImageBrush)((Border)home.FindName("HomeHero")).Background).ImageSource);
                foreach (var size in new[] { new Size(1000, 640), new Size(1135, 720), new Size(1280, 820) })
                {
                    Layout(root, size.Width, size.Height);
                    foreach (var element in Descendants<Button>(home).Cast<FrameworkElement>().Append((Border)home.FindName("HomeStatusArea")))
                    {
                        var origin = element.TranslatePoint(new Point(), root);
                        Assert.InRange(origin.Y + element.ActualHeight, 0, size.Height);
                        Assert.InRange(origin.X + element.ActualWidth, 0, size.Width);
                    }
                    Assert.Empty(Descendants<ScrollViewer>(home));
                    SavePreview(root, $"home-en-{size.Width}x{size.Height}.png");
                }
                var englishConsent = new ConsentWindow(new ConsentViewModel(new UserConsentService()));
                var englishConsentRoot = (Grid)englishConsent.Content;
                Layout(englishConsentRoot, 850, 740);
                Assert.Contains(Descendants<TextBox>(englishConsentRoot), box => box.Text == UserConsentService.AgreementEnglishText);
                englishConsent.Close();
                PresentationSettings.Current.ToggleThemeCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Layout(root);
                Assert.Same(window.FindResource("OverworldArtwork"), ((ImageBrush)((Border)home.FindName("HomeHero")).Background).ImageSource);
                SavePreview(root, "home-dark-en.png");
                var splash = (Border)window.FindName("StartupAnimation");
                splash.Visibility = Visibility.Visible;
                Layout(root);
                SavePreview(root, "startup.png");
                splash.Visibility = Visibility.Collapsed;
                PresentationSettings.Current.ToggleLanguageCommand.ExecuteAsync(null).GetAwaiter().GetResult();
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
                if (PresentationSettings.Current.IsEnglish) PresentationSettings.Current.ToggleLanguageCommand.ExecuteAsync(null).GetAwaiter().GetResult();
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

    // Layout tests must not run real startup, display consent dialogs, or touch user data.
    private sealed class CompositionApplication : Application
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private static void Layout(FrameworkElement root, double width = 1280, double height = 820)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
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
