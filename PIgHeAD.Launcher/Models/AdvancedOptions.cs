namespace PIgHeAD.Launcher.Models;

public enum GameWindowMode { Windowed, Fullscreen, Maximized }
public enum GamePriority { BelowNormal, Normal, AboveNormal, High }
public enum GameLanguage { KeepExisting, FollowSystem, SimplifiedChinese, TraditionalChinese, English }
public sealed record SettingChoice<T>(T Value, string Label);
