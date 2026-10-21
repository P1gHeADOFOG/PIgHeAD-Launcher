namespace PIgHeAD.Launcher.Models;

public sealed record GameVersionInfo(string Id, string Type, bool IsInstalled)
{
    public string DisplayName => $"{Id} · {(IsInstalled ? "本地" : "可下载")} · {Type}";
}
