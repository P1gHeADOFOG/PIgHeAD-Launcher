using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Consent;
using PIgHeAD.Launcher.Services.Minecraft;
using PIgHeAD.Launcher.Services.Settings;
using PIgHeAD.Launcher.ViewModels;

namespace PIgHeAD.Launcher.Tests;

public sealed class ConsentAndEnvironmentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PIgHeAD.AdvancedTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ConsentRequiresBothChoicesAndMatchingDocumentHash()
    {
        var path = Path.Combine(directory, "consent.json");
        var service = new UserConsentService(path);
        var viewModel = new ConsentViewModel(service);
        Assert.False(await service.IsAcceptedAsync(CancellationToken.None));
        Assert.False(viewModel.AgreementAccepted);
        Assert.False(viewModel.DisclaimerAccepted);
        Assert.False(viewModel.AcceptCommand.CanExecute(null));
        viewModel.AgreementAccepted = true;
        Assert.False(viewModel.AcceptCommand.CanExecute(null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(true, false, CancellationToken.None));
        viewModel.DisclaimerAccepted = true;
        bool? result = null;
        viewModel.CloseRequested += value => result = value;
        await viewModel.AcceptCommand.ExecuteAsync(null);
        Assert.True(result);
        Assert.True(await service.IsAcceptedAsync(CancellationToken.None));
        Assert.Contains("PIgHeAD_OFOG", viewModel.AgreementText);
        Assert.Contains("p1gheadofog@gmail.com", viewModel.DisclaimerText);
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        saved["ContentHash"] = "old-documents";
        await File.WriteAllTextAsync(path, saved.ToJsonString());
        Assert.False(await service.IsAcceptedAsync(CancellationToken.None));
        await File.WriteAllTextAsync(path, "broken json");
        Assert.False(await service.IsAcceptedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DecliningDoesNotPersistConsentAndWriteFailureDoesNotCloseDialog()
    {
        var path = Path.Combine(directory, "consent.json");
        var viewModel = new ConsentViewModel(new UserConsentService(path));
        bool? result = null;
        viewModel.CloseRequested += value => result = value;
        viewModel.DeclineCommand.Execute(null);
        Assert.False(result);
        Assert.False(File.Exists(path));

        Directory.CreateDirectory(path); // A directory at the target path makes atomic replacement fail.
        result = null;
        viewModel.AgreementAccepted = viewModel.DisclaimerAccepted = true;
        await viewModel.AcceptCommand.ExecuteAsync(null);
        Assert.Null(result);
        Assert.False(viewModel.IsBusy);
        Assert.Contains("无法保存", viewModel.Status);
    }

    [Theory]
    [InlineData(8192UL, 6000UL, 3840)]
    [InlineData(32768UL, 28000UL, 12288)]
    [InlineData(4096UL, 2800UL, 512)]
    public void AutomaticMemoryReservesCapacityAndAlignsHeap(ulong total, ulong available, int expected) =>
        Assert.Equal(expected, GameEnvironment.CalculateAutomaticMemory(total, available));

    [Fact]
    public void InsufficientMemoryAndDirectoryTraversalAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => GameEnvironment.CalculateAutomaticMemory(4096, 512));
        Assert.Throws<InvalidOperationException>(() => GameEnvironment.GetWorkingDirectory(new LauncherSettings { IsolateVersions = true }, "../escape"));
        Assert.Throws<InvalidDataException>(() => ModLoaderService.ValidateIdentifier("../escape"));
    }

    [Fact]
    public async Task LanguageUpdatePreservesOtherOptionsAndBacksUpOriginal()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "options.txt");
        var original = "music:0.5\nlang:en_us\nfullscreen:false\n";
        await File.WriteAllTextAsync(path, original);
        await GameEnvironment.ApplyLanguageAsync(directory, null, CancellationToken.None);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
        await GameEnvironment.ApplyLanguageAsync(directory, "zh_cn", CancellationToken.None);
        Assert.Equal(original, await File.ReadAllTextAsync(path + ".pighead-backup"));
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Contains("music:0.5", lines); Assert.Contains("fullscreen:false", lines);
        Assert.Equal("lang:zh_cn", Assert.Single(lines, line => line.StartsWith("lang:")));
        Assert.Equal("zh_tw", GameEnvironment.ResolveLanguage(GameLanguage.FollowSystem, CultureInfo.GetCultureInfo("zh-TW")));
        Assert.Equal("en_us", GameEnvironment.ResolveLanguage(GameLanguage.FollowSystem, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public async Task AdvancedSettingsRoundTripAndInvalidDraftKeepsSavedSnapshot()
    {
        var store = new JsonSettingsService(Path.Combine(directory, "settings.json"));
        var settings = new SettingsViewModel(store);
        await settings.LoadCommand.ExecuteAsync(null);
        settings.AutomaticMemory = settings.IsolateVersions = true;
        settings.WindowMode = GameWindowMode.Maximized;
        settings.ProcessPriority = GamePriority.AboveNormal;
        settings.Language = GameLanguage.FollowSystem;
        settings.ExtraJvmArguments = "-XX:+UseG1GC";
        settings.ExtraGameArguments = "--test \"hello world\"";
        settings.EnableAnimations = false;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.HasError);
        var saved = settings.SavedSettings;
        Assert.Equal(saved, (await store.LoadAsync()).Settings);
        settings.WindowWidth = "abc";
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.True(settings.HasError);
        Assert.Same(saved, settings.SavedSettings);
        settings.ResetDefaultsCommand.Execute(null);
        Assert.Same(saved, settings.SavedSettings); // Reset edits only the draft.
        Assert.False(settings.IsolateVersions);
    }

    [Fact]
    public void ArgumentsPreserveQuotedValuesAndWindowsPathsWithoutShellExpansion()
    {
        Assert.Equal(new[] { "--name", "hello world", "--path", @"\\server\shared folder", "$(untouched)" },
            LaunchArgumentParser.Parse("--name \"hello world\" --path \"\\\\server\\shared folder\" $(untouched)"));
        Assert.Throws<ValidationException>(() => new LauncherSettings { ExtraGameArguments = "--accessToken=bad" }.Validate());
        Assert.Throws<ValidationException>(() => new LauncherSettings { ExtraJvmArguments = "-Xmx10G" }.Validate());
        Assert.Throws<ValidationException>(() => LaunchArgumentParser.Parse("\"unfinished"));
    }

    public void Dispose()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PIgHeAD.AdvancedTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
