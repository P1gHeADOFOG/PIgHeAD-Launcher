using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using PIgHeAD.Launcher.Services.Consent;

namespace PIgHeAD.Launcher.Services.Presentation;

internal static class Translations
{
    private static readonly Dictionary<string, string> English = Load();
    private static readonly (Regex Pattern, string Translation)[] Templates = English
        .Where(item => item.Key.Contains("{0}", StringComparison.Ordinal))
        .OrderByDescending(item => item.Key.Length)
        .Select(item => (new Regex("^" + Regex.Replace(Regex.Escape(item.Key), @"\\\{\d+}", "(.+?)") + "$",
            RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(50)), item.Value)).ToArray();

    public static string ToEnglish(string text) => Translate(text, 0);

    private static string Translate(string text, int depth)
    {
        if (English.TryGetValue(text, out var translated)) return translated;
        if (depth > 4 || !text.Any(c => c is >= '\u4e00' and <= '\u9fff')) return text;
        foreach (var (pattern, translation) in Templates)
        {
            var match = pattern.Match(text);
            if (match.Success)
                return string.Format(CultureInfo.InvariantCulture, translation,
                    match.Groups.Cast<Group>().Skip(1).Select(group => (object)Translate(group.Value, depth + 1)).ToArray());
        }
        return text;
    }

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(Translations).Assembly.GetManifestResourceStream("PIgHeAD.Strings.en.json")!;
        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        result[UserConsentService.AgreementText] = UserConsentService.AgreementEnglishText;
        result[UserConsentService.DisclaimerText] = UserConsentService.DisclaimerEnglishText;
        return result;
    }
}
