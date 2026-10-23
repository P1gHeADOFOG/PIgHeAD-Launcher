using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class GameEnvironment
{
    public static string GetWorkingDirectory(LauncherSettings settings, string versionId)
    {
        var root = GameRootLocator.Resolve(settings.GameDirectory);
        if (string.IsNullOrWhiteSpace(versionId) || versionId is "." or ".."
            || versionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("版本名称不能用作隔离目录。");
        if (ImportedGameLayout.GetWorkingDirectory(root, versionId) is { } imported) return imported;
        foreach (var prefix in new[] { "versions", "instances" })
        {
            var candidate = SafeGamePath.Resolve(root, prefix + "/" + versionId);
            if (new[] { "mods", "config", "saves", "resourcepacks" }.Any(name => Directory.Exists(Path.Combine(candidate, name))))
                return candidate;
        }
        if (!settings.IsolateVersions) return Path.GetFullPath(root);
        return Path.Combine(Path.GetFullPath(root), "instances", versionId);
    }

    public static int CalculateAutomaticMemory(ulong totalMb, ulong availableMb)
    {
        // Leave at least 2 GiB available for the OS, cap the heap at half of physical RAM / 12 GiB.
        var budget = Math.Min(Math.Min(totalMb / 2, availableMb > 2048 ? availableMb - 2048 : 0), 12288UL);
        if (budget < 512) throw new InvalidOperationException("当前可用内存不足，暂不能自动分配至少 512 MiB。请关闭其他程序后重试。");
        return (int)(budget / 256 * 256);
    }

    public static int GetMemory(LauncherSettings settings)
    {
        if (!settings.AutomaticMemory) return settings.MaximumMemoryMb;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new InvalidOperationException("无法读取系统可用内存，请切换手动分配。");
        return CalculateAutomaticMemory(memory.TotalPhysical / 1048576, memory.AvailablePhysical / 1048576);
    }

    public static string? ResolveLanguage(GameLanguage language, CultureInfo culture) => language switch
    {
        GameLanguage.KeepExisting => null,
        GameLanguage.SimplifiedChinese => "zh_cn",
        GameLanguage.TraditionalChinese => "zh_tw",
        GameLanguage.English => "en_us",
        GameLanguage.FollowSystem when culture.TwoLetterISOLanguageName == "zh" =>
            culture.Name.Contains("TW", StringComparison.OrdinalIgnoreCase) || culture.Name.Contains("HK", StringComparison.OrdinalIgnoreCase)
                || culture.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ? "zh_tw" : "zh_cn",
        GameLanguage.FollowSystem => culture.Name.ToLowerInvariant() switch
        {
            "en-gb" => "en_gb", "pt-br" => "pt_br", "es-mx" => "es_mx",
            _ => culture.TwoLetterISOLanguageName switch
            {
                "ja" => "ja_jp", "ko" => "ko_kr", "de" => "de_de", "fr" => "fr_fr", "es" => "es_es",
                "ru" => "ru_ru", "it" => "it_it", "pt" => "pt_pt", "pl" => "pl_pl", "tr" => "tr_tr",
                "uk" => "uk_ua", "nl" => "nl_nl", _ => "en_us"
            }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    public static async Task ApplyLanguageAsync(string directory, string? language, CancellationToken cancellationToken)
    {
        if (language is null) return;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "options.txt");
        var lines = File.Exists(path) ? await File.ReadAllLinesAsync(path, cancellationToken) : [];
        var updated = lines.Where(line => !line.StartsWith("lang:", StringComparison.Ordinal)).Append("lang:" + language).ToArray();
        if (lines.SequenceEqual(updated)) return;
        if (File.Exists(path)) File.Copy(path, path + ".pighead-backup", overwrite: true);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllLinesAsync(temporary, updated, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
