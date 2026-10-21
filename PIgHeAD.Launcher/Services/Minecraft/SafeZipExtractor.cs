using System.IO;
using System.IO.Compression;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class SafeZipExtractor
{
    public const long MaximumExpandedBytes = 16L * 1024 * 1024 * 1024;
    public static async Task ExtractAsync(string archivePath, string destination, IProgress<GameProgress> progress, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 100000) throw new InvalidDataException("压缩包文件数量超过支持范围（100000）。");
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = SafeGamePath.Resolve(destination, entry.FullName.TrimEnd('/', '\\'));
            if (!targets.Add(path)) throw new InvalidDataException("压缩包包含重名文件，已停止导入。");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & 0x400) != 0)
                throw new InvalidDataException("压缩包包含符号链接，已停止导入。");
            total = checked(total + entry.Length);
            if (total > MaximumExpandedBytes) throw new InvalidDataException("整合包解压后超过 16 GiB，暂不支持自动导入。");
        }
        var completed = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = SafeGamePath.Resolve(destination, entry.FullName.TrimEnd('/', '\\'));
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) Directory.CreateDirectory(target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                long written = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    written += count;
                    if (written > entry.Length) throw new InvalidDataException("压缩包实际文件大小与目录记录不符。");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (written != entry.Length) throw new InvalidDataException("压缩包内容不完整。");
            }
            completed++;
            if (completed % 30 == 0 || completed == archive.Entries.Count)
                progress.Report(new GameProgress($"正在解压整合包 {completed}/{archive.Entries.Count}", 100d * completed / Math.Max(archive.Entries.Count, 1)));
        }
    }
}
