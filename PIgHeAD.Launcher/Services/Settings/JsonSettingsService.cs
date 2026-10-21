using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using PIgHeAD.Launcher.Services.Minecraft;

namespace PIgHeAD.Launcher.Services.Settings;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly bool discoverDefault;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonSettingsService(string? settingsFilePath = null)
    {
        discoverDefault = settingsFilePath is null;
        SettingsFilePath = Path.GetFullPath(settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PIgHeAD", "Launcher", "settings.json"));
    }

    public string SettingsFilePath { get; }

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = new FileStream(SettingsFilePath, FileMode.Open,
                FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            var settings = await JsonSerializer.DeserializeAsync<LauncherSettings>(
                stream, SerializerOptions, cancellationToken).ConfigureAwait(false);

            if (settings is null)
            {
                throw new JsonException("Settings must be a JSON object.");
            }

            settings.Validate();
            return new SettingsLoadResult(ResolveDirectory(settings));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SettingsLoadResult(ResolveDirectory(new LauncherSettings()));
        }
        catch (Exception exception) when (exception is JsonException or ValidationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var backupPath = $"{SettingsFilePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json";
            File.Copy(SettingsFilePath, backupPath);
            return new SettingsLoadResult(new LauncherSettings(), backupPath);
        }
    }

    private LauncherSettings ResolveDirectory(LauncherSettings settings)
    {
        var directory = GameRootLocator.Resolve(settings.GameDirectory);
        if (discoverDefault && directory.Equals(new LauncherSettings().GameDirectory, StringComparison.OrdinalIgnoreCase)
            && !Directory.Exists(Path.Combine(directory, "versions")))
        {
            directory = GameRootLocator.Discover(Environment.CurrentDirectory, AppContext.BaseDirectory) ?? directory;
        }
        return settings with { GameDirectory = directory };
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        settings.Validate();

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        var temporaryPath = $"{SettingsFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Replace from the same directory so readers never see a partially written JSON file.
            File.Move(temporaryPath, SettingsFilePath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Cleanup must not hide the original save error or cancellation.
            }
        }
    }
}
