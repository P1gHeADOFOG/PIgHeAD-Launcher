using System.IO;
using System.Text.RegularExpressions;
using PIgHeAD.Launcher.Services.Settings;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class LocalJavaLocator
{
    public static IReadOnlyList<string> FindCandidates(string gameDirectory, string launcherDirectory, int minimumMajor)
    {
        var roots = new[] { Path.GetDirectoryName(LauncherPaths.GetGameDataDirectory(gameDirectory))!,
            GameRootLocator.Resolve(gameDirectory), launcherDirectory }.Distinct(StringComparer.OrdinalIgnoreCase);
        var found = new List<(string Path, int Major)>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var folder in Directory.EnumerateDirectories(root).Where(IsJavaFolder))
            {
                Add(folder);
                foreach (var child in Directory.EnumerateDirectories(folder).Where(path =>
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)) Add(child);
            }
        }
        return found.Where(item => item.Major == 0 || item.Major >= minimumMajor)
            .OrderBy(item => item.Major == minimumMajor ? 0 : item.Major == 0 ? 2 : 1)
            .ThenBy(item => item.Major).Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        void Add(string directory)
        {
            var java = new[] { Path.Combine(directory, "bin", "javaw.exe"), Path.Combine(directory, "bin", "java.exe") }
                .FirstOrDefault(File.Exists);
            if (java is null) return;
            var release = Path.Combine(directory, "release");
            var text = File.Exists(release) ? File.ReadLines(release).FirstOrDefault(line => line.StartsWith("JAVA_VERSION=")) ?? "" : "";
            var match = Regex.Match(text, "JAVA_VERSION=\"(?<major>\\d+)(?:\\.(?<minor>\\d+))?");
            var major = match.Success ? int.Parse(match.Groups["major"].Value) : 0;
            if (major == 1 && match.Groups["minor"].Success) major = int.Parse(match.Groups["minor"].Value);
            // The launch-time probe remains authoritative; folder names are only a hint without a release file.
            if (major == 0 && Regex.Match(Path.GetFileName(directory), @"(?:java|jdk|jre)[-_]?(\d+)", RegexOptions.IgnoreCase) is { Success: true } hint)
                major = int.TryParse(hint.Groups[1].Value, out var number) ? number : 0;
            found.Add((java, major));
        }
    }

    private static bool IsJavaFolder(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
        && Regex.IsMatch(Path.GetFileName(path), @"^(java|jdk|jre|runtime)", RegexOptions.IgnoreCase);
}
