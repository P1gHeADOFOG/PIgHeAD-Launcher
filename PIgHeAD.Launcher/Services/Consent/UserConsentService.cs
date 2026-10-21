using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PIgHeAD.Launcher.Services.Consent;

public sealed class UserConsentService
{
    public const string Revision = "2026-10-21-v1";
    public static string AgreementText { get; } = ReadDocument("PIgHeAD.UserAgreement.md");
    public static string DisclaimerText { get; } = ReadDocument("PIgHeAD.Disclaimer.md");
    public static string ContentHash { get; } = Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(AgreementText + "\n" + DisclaimerText)));
    private readonly string path;

    public UserConsentService(string? path = null) => this.path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIgHeAD", "Launcher", "consent.json");

    public async Task<bool> IsAcceptedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var consent = await JsonSerializer.DeserializeAsync<ConsentRecord>(stream, cancellationToken: cancellationToken);
            return consent is { AgreementAccepted: true, DisclaimerAccepted: true }
                && consent.Revision == Revision && consent.ContentHash == ContentHash
                && consent.AcceptedAtUtc > DateTimeOffset.UnixEpoch;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return false;
        }
    }

    public async Task AcceptAsync(bool agreementAccepted, bool disclaimerAccepted, CancellationToken cancellationToken)
    {
        if (!agreementAccepted || !disclaimerAccepted) throw new InvalidOperationException("请分别阅读并同意用户协议与免责声明。");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new ConsentRecord(
                Revision, ContentHash, true, true, DateTimeOffset.UtcNow), new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string ReadDocument(string name)
    {
        using var stream = typeof(UserConsentService).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("无法读取随程序提供的协议文本。");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed record ConsentRecord(string Revision, string ContentHash, bool AgreementAccepted,
        bool DisclaimerAccepted, DateTimeOffset AcceptedAtUtc);
}
