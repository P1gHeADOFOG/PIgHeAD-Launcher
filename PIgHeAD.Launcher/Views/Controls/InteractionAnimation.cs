using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PIgHeAD.Launcher.Views.Controls;

public static class InteractionAnimation
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(InteractionAnimation), new PropertyMetadata(false, OnEnabledChanged));
    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.RegisterAttached(
        "AnimationsEnabled", typeof(bool), typeof(InteractionAnimation),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits, OnPreferenceChanged));
    private static readonly DependencyProperty ScaleProperty = DependencyProperty.RegisterAttached(
        "Scale", typeof(ScaleTransform), typeof(InteractionAnimation));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
    public static void SetAnimationsEnabled(DependencyObject element, bool value) => element.SetValue(AnimationsEnabledProperty, value);
    public static bool GetAnimationsEnabled(DependencyObject element) => (bool)element.GetValue(AnimationsEnabledProperty);

    private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if ((bool)e.NewValue)
        {
            if (element.GetValue(ScaleProperty) is null)
            {
                var scale = new ScaleTransform(1, 1);
                var group = new TransformGroup();
                group.Children.Add(element.RenderTransform);
                group.Children.Add(scale);
                element.RenderTransform = group;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.SetValue(ScaleProperty, scale);
            }
            element.PreviewMouseLeftButtonDown += OnPress;
            element.PreviewMouseLeftButtonUp += OnRelease;
            element.MouseLeave += OnLeave;
            element.LostMouseCapture += OnLostCapture;
            element.PreviewKeyDown += OnKeyDown;
            element.PreviewKeyUp += OnKeyUp;
            element.Unloaded += OnUnloaded;
            element.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnClick), true);
        }
        else
        {
            element.PreviewMouseLeftButtonDown -= OnPress;
            element.PreviewMouseLeftButtonUp -= OnRelease;
            element.MouseLeave -= OnLeave;
            element.LostMouseCapture -= OnLostCapture;
            element.PreviewKeyDown -= OnKeyDown;
            element.PreviewKeyUp -= OnKeyUp;
            element.Unloaded -= OnUnloaded;
            element.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnClick));
            Reset(element);
        }
    }

    private static void OnPreferenceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) Reset(sender);
    }
    private static void OnPress(object sender, MouseButtonEventArgs e) => Animate((FrameworkElement)sender, 0.975, 65);
    private static void OnRelease(object sender, MouseButtonEventArgs e) => Animate((FrameworkElement)sender, 1, 160);
    private static void OnLeave(object sender, MouseEventArgs e) => Animate((FrameworkElement)sender, 1, 120);
    private static void OnLostCapture(object sender, MouseEventArgs e) => Animate((FrameworkElement)sender, 1, 120);
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Reset((DependencyObject)sender);
    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!e.IsRepeat && e.Key is Key.Space or Key.Enter) Animate((FrameworkElement)sender, 0.975, 65);
    }
    private static void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter) Animate((FrameworkElement)sender, 1, 160);
    }
    private static void OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (!CanAnimate(element) || element.GetValue(ScaleProperty) is not ScaleTransform scale) return;
        var animation = new DoubleAnimation(0.975, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
    private static bool CanAnimate(FrameworkElement element) => element.IsEnabled && GetAnimationsEnabled(element) && SystemParameters.ClientAreaAnimation;
    private static void Animate(FrameworkElement element, double value, int milliseconds)
    {
        if (!CanAnimate(element)) { Reset(element); return; }
        if (element.GetValue(ScaleProperty) is not ScaleTransform scale) return;
        var animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(milliseconds))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
    private static void Reset(DependencyObject element)
    {
        if (element.GetValue(ScaleProperty) is not ScaleTransform scale) return;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }
}
