using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PIgHeAD.Launcher.Services.Presentation;

/// <summary>Launcher appearance is shared across game profiles and loaded before the first window.</summary>
public partial class PresentationSettings : ObservableObject
{
    public static PresentationSettings Current { get; } = new();
    private string? file;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    public string Language { get; private set; } = "zh-CN";
    public string Theme { get; private set; } = "Dark";
    public bool IsEnglish => Language == "en-US";
    public string LanguageSwitchLabel => IsEnglish ? "中文" : "EN";
    public string ThemeSwitchLabel => Localize(Theme == "Dark" ? "浅色模式" : "深色模式");
    public string this[string text] => Localize(text);
    [ObservableProperty] public partial string Status { get; private set; } = "";

    public async Task InitializeAsync(string path, CancellationToken token = default)
    {
        file = path;
        try
        {
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<Preferences>(await File.ReadAllTextAsync(path, token));
                Language = saved?.Language == "en-US" ? "en-US" : "zh-CN";
                Theme = saved?.Theme == "Light" ? "Light" : "Dark";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Status = "界面偏好读取失败，将使用默认外观。";
        }
        Refresh();
    }

    [RelayCommand]
    private async Task ToggleLanguageAsync()
    {
        Language = IsEnglish ? "zh-CN" : "en-US";
        Refresh();
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        Theme = Theme == "Dark" ? "Light" : "Dark";
        Refresh();
        await SaveAsync();
    }

    private void Refresh()
    {
        if (ReferenceEquals(this, Current) && Application.Current is { } app)
        {
            var palette = new ResourceDictionary
            {
                Source = new Uri($"/PIgHeAD.Launcher;component/Resources/Styles/{(Theme == "Light" ? "LightColors" : "Colors")}.xaml", UriKind.Relative)
            };
            foreach (string key in palette.Keys) app.Resources[key] = palette[key];
        }
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(LanguageSwitchLabel));
        OnPropertyChanged(nameof(ThemeSwitchLabel));
        OnPropertyChanged("Item[]");
    }

    private async Task SaveAsync()
    {
        if (file is null) return;
        await saveGate.WaitAsync();
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Preferences(Language, Theme)));
            File.Move(temporary, file, overwrite: true);
            Status = "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Status = "界面偏好保存失败，当前外观仍然有效。";
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            saveGate.Release();
        }
    }

    public string Localize(string text) => IsEnglish ? Translations.ToEnglish(text) : text;
    private sealed record Preferences(string Language, string Theme);
}
