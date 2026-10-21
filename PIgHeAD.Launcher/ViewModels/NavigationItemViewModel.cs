using CommunityToolkit.Mvvm.ComponentModel;

namespace PIgHeAD.Launcher.ViewModels;

public sealed record NavigationItemViewModel(string Title, string Subtitle, ObservableObject Page);
