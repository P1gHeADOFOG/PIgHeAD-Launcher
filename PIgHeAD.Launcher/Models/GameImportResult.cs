namespace PIgHeAD.Launcher.Models;

public sealed record GameImportResult(string GameDirectory, string SelectedVersionId, bool FilesCompleted, string Description);
