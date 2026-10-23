using System.IO;

namespace PIgHeAD.Launcher.Models;

public sealed record GameDirectoryEntry(string DirectoryPath)
{
    public string DisplayName => Directory.Exists(DirectoryPath) ? DirectoryPath : DirectoryPath + "（目录已移动或不存在）";
}
