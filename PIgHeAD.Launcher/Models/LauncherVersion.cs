using System.Reflection;

namespace PIgHeAD.Launcher.Models;

public static class LauncherVersion
{
    public static string Value { get; } = typeof(LauncherVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.2.2-beta";
    public static string Display => Value.EndsWith("-beta", StringComparison.Ordinal)
        ? "beta v" + Value[..^5] : "v" + Value;
}
