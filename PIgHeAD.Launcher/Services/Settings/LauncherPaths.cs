using System.IO;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.Services.Settings;

public sealed class LauncherPaths(string launcherDirectory)
{
    public string LauncherDirectory { get; } = Path.GetFullPath(launcherDirectory);
    public string DataDirectory => Path.Combine(LauncherDirectory, "PHL");
    public string ConsentFile => Path.Combine(DataDirectory, "consent.json");
    public string AuthDirectory => Path.Combine(DataDirectory, "auth");
    public string ImportDirectory => Path.Combine(DataDirectory, "imports");
    public static string GetGameDataDirectory(string gameDirectory)
    {
        var root = Path.TrimEndingDirectorySeparator(GameRootLocator.Resolve(gameDirectory));
        var container = Path.GetFileName(root).Equals(".minecraft", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(root)! : root;
        return Path.Combine(container, "PHL");
    }
}
