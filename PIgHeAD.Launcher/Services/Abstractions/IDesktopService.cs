namespace PIgHeAD.Launcher.Services.Abstractions;

public interface IDesktopService
{
    string? SelectFolder(string initialDirectory);
    string? SelectFile(string filter);
    void OpenWebsite(string address);
    void OpenFolder(string path);
    void ShowLegalDocuments();
}
