using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PIgHeAD.Launcher.Views.Controls;

public sealed class AnimatedContentControl : ContentControl
{
    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.Register(
        nameof(AnimationsEnabled), typeof(bool), typeof(AnimatedContentControl), new PropertyMetadata(true, OnEnabledChanged));
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }

    public AnimatedContentControl()
    {
        RenderTransform = new TranslateTransform();
        Loaded += (_, _) => Animate();
        Unloaded += (_, _) => StopAnimations();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (IsLoaded) Animate();
    }

    private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) ((AnimatedContentControl)sender).StopAnimations();
    }

    private void Animate()
    {
        StopAnimations();
        if (!AnimationsEnabled || !SystemParameters.ClientAreaAnimation) return;
        var duration = TimeSpan.FromMilliseconds(180);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0.3, 1, duration) { FillBehavior = FillBehavior.Stop });
        if (RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duration)
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }

    private void StopAnimations()
    {
        BeginAnimation(OpacityProperty, null);
        if (RenderTransform is TranslateTransform translate) translate.BeginAnimation(TranslateTransform.YProperty, null);
    }
}
