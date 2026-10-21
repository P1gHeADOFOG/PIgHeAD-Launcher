namespace PIgHeAD.Launcher.Models;

public enum DownloadComponent { Vanilla, Forge, NeoForge, Fabric, Quilt, OptiFine }
public sealed record LoaderBuild(string Id, string Label);
public sealed record DownloadHistoryItem(string Name, string Result, DateTimeOffset FinishedAt);
