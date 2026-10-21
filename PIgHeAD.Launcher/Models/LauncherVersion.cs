using System.Reflection;

namespace PIgHeAD.Launcher.Models;

public static class LauncherVersion
{
    public static string Value { get; } = typeof(LauncherVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.4beta";
    public static string Display => "v" + Value;
}
