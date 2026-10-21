namespace PIgHeAD.Launcher.Models;

public sealed record GameProgress(string Message, double? Percentage = null);

public sealed record GameLaunchRequest(
    LauncherSettings Settings, GameVersionInfo Version, GameSession Session, bool AllowDownloads);

public sealed record GameLaunchResult(int ProcessId, string LogFilePath, Task<int> Completion);
