using System.IO;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class GameRootLocator
{
    public static string Resolve(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (Directory.Exists(Path.Combine(full, "versions"))) return full;
        var minecraft = Path.Combine(full, ".minecraft");
        return Directory.Exists(minecraft) ? minecraft : full;
    }

    public static string? Discover(params string[] roots)
    {
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Resolve(root);
            if (Directory.Exists(Path.Combine(candidate, "versions"))) return candidate;
        }
        return null;
    }

    public static string? FindInExtractedPack(string directory)
    {
        var direct = Discover(directory);
        if (direct is not null) return direct;
        // One wrapper folder is common in shared ZIP modpacks. Never guess between multiple games.
        var candidates = Directory.EnumerateDirectories(directory).Where(path =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
            .Select(path => Discover(path)).Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (candidates.Length > 1) throw new InvalidDataException("压缩包包含多个游戏根目录，请分别导入。");
        return candidates.SingleOrDefault();
    }
}
