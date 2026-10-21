using System.IO;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class SafeGamePath
{
    public static string Resolve(string root, string relative)
    {
        var normalized = relative.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/')
            || normalized.Split('/').Any(part => part is ".." || part.Contains(':') || part.EndsWith(' ') || part.EndsWith('.')))
            throw new InvalidDataException("整合包包含不安全的文件路径。");
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(prefix, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("整合包文件路径超出目标目录。");
        for (var path = Path.GetDirectoryName(target); path is not null && path.Length >= prefix.Length - 1; path = Path.GetDirectoryName(path))
        {
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("导入目录包含链接，请使用普通目录。");
        }
        if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("目标文件是链接，已停止写入。");
        return target;
    }
}
