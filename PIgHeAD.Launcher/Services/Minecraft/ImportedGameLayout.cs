using System.IO;
using System.Text.Json;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class ImportedGameLayout
{
    private const string FileName = ".pighead-import.json";
    public static async Task WriteAsync(string root, IReadOnlyDictionary<string, string> versionDirectories, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, FileName);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(versionDirectories), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string? GetWorkingDirectory(string root, string versionId)
    {
        var path = Path.Combine(root, FileName);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("导入实例目录记录过大。");
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("导入实例目录记录损坏，请重新导入或检查记录。");
        if (!map.TryGetValue(versionId, out var relative)) return null;
        return relative == "." ? Path.GetFullPath(root) : SafeGamePath.Resolve(root, relative);
    }
}
