namespace PIgHeAD.Launcher.Models;

public sealed record SettingsLoadResult(LauncherSettings Settings, string? BackupFilePath = null);
