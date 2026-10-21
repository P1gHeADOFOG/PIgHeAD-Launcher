using System.ComponentModel.DataAnnotations;
using System.IO;

namespace PIgHeAD.Launcher.Models;

public sealed record LauncherSettings
{
    public const int MinimumMemoryMb = 512;
    public const int MaximumSupportedMemoryMb = 65536;
    public const string DefaultMicrosoftClientId = "2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a";

    public string GameDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PIgHeAD", "Launcher", "instances", "default");

    public string JavaPath { get; init; } = string.Empty;

    public int MaximumMemoryMb { get; init; } = 4096;

    public string MicrosoftClientId { get; init; } = DefaultMicrosoftClientId;
    public string PreferredVersionId { get; init; } = "";
    public GameWindowMode WindowMode { get; init; } = GameWindowMode.Windowed;
    public int WindowWidth { get; init; } = 1280;
    public int WindowHeight { get; init; } = 720;
    public bool AutomaticMemory { get; init; }
    public bool IsolateVersions { get; init; }
    public string ExtraJvmArguments { get; init; } = "";
    public string ExtraGameArguments { get; init; } = "";
    public GamePriority ProcessPriority { get; init; } = GamePriority.Normal;
    public GameLanguage Language { get; init; } = GameLanguage.KeepExisting;
    public bool EnableAnimations { get; init; } = true;

    public void Validate()
    {
        if (MicrosoftClientId is null || (MicrosoftClientId.Length > 0 && !Guid.TryParse(MicrosoftClientId, out _)))
        {
            throw new ValidationException("Microsoft Client ID 必须留空或填写有效的应用程序 GUID。");
        }

        ValidateAbsolutePath(GameDirectory, "游戏目录");

        if (!Enum.IsDefined(WindowMode) || !Enum.IsDefined(ProcessPriority) || !Enum.IsDefined(Language))
            throw new ValidationException("高级设置包含不支持的选项。");
        if (WindowWidth is < 320 or > 16384 || WindowHeight is < 240 or > 16384)
            throw new ValidationException("窗口宽度须为 320–16384，高度须为 240–16384 像素。");
        if (ExtraJvmArguments is null || ExtraGameArguments is null
            || ExtraJvmArguments.Length > 8192 || ExtraGameArguments.Length > 8192)
            throw new ValidationException("额外参数不能为 null，且每组最多 8192 个字符。");
        LaunchArgumentParser.Validate(ExtraJvmArguments, isJvm: true);
        LaunchArgumentParser.Validate(ExtraGameArguments, isJvm: false);

        if (JavaPath is null)
        {
            throw new ValidationException("Java 路径不能为 null；未指定时请留空。");
        }

        if (JavaPath.Length > 0)
        {
            ValidateAbsolutePath(JavaPath, "Java 路径");
        }

        if (MaximumMemoryMb is < MinimumMemoryMb or > MaximumSupportedMemoryMb)
        {
            throw new ValidationException(
                $"最大内存必须在 {MinimumMemoryMb} 到 {MaximumSupportedMemoryMb} MiB 之间。");
        }
    }

    private static void ValidateAbsolutePath(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
            || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || path.Contains('*') || path.Contains('?'))
        {
            throw new ValidationException($"{label}必须是有效的绝对路径。");
        }

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ValidationException($"{label}必须是有效的绝对路径。", exception);
        }
    }
}
