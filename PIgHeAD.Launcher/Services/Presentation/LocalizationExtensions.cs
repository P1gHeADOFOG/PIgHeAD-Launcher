using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace PIgHeAD.Launcher.Services.Presentation;

/// <summary>Both static copy and bound status text update immediately when the language changes.</summary>
public sealed class LocExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{text}]") { Source = PresentationSettings.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

[ContentProperty(nameof(Value))]
public sealed class TranslateExtension : MarkupExtension
{
    public TranslateExtension() { }
    public TranslateExtension(BindingBase value) => Value = value;
    public BindingBase Value { get; set; } = new Binding();
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = Translator.Instance,
            ConverterParameter = (Value as Binding)?.StringFormat };
        binding.Bindings.Add(Value);
        binding.Bindings.Add(new Binding(nameof(PresentationSettings.Language)) { Source = PresentationSettings.Current });
        return binding.ProvideValue(serviceProvider);
    }

    private sealed class Translator : IMultiValueConverter
    {
        public static Translator Instance { get; } = new();
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string format)
                return string.Format(culture, PresentationSettings.Current.Localize(format), values[0]);
            return values[0] is string text ? PresentationSettings.Current.Localize(text) : values[0];
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
