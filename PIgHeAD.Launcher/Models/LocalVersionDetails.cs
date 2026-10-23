namespace PIgHeAD.Launcher.Models;

public sealed record LocalVersionDetails(string VersionId, string MinecraftVersion, string LoaderName,
    string LoaderVersion, int? JavaMajorVersion, string VersionType, string GameDirectory,
    string VersionDirectory, string WorkingDirectory, long VersionSizeBytes, DateTimeOffset? ReleaseTime,
    bool SupportsMods)
{
    public string LoaderSummary => SupportsMods ? $"{LoaderName} {LoaderVersion}".Trim() : "原版 / 未识别到模组加载器";
    public string JavaSummary => JavaMajorVersion is { } major ? $"Java {major}+" : "未声明（启动时检查）";
    public string SizeSummary => $"{VersionSizeBytes / 1048576d:N2} MiB";
    public string ReleaseSummary => ReleaseTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "未声明";
}

public sealed record LocalModInfo(string Name, string ModId, string Version, string FileName, string MetadataStatus);
