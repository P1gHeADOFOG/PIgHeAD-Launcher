using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PIgHeAD.Launcher.Models;

public static class LaunchArgumentParser
{
    // Tokenize without a shell; support quoted values and escaped quote/backslash pairs.
    public static IReadOnlyList<string> Parse(string input)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        var quoted = false;
        var started = false;
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '\0' || ch == '\r' || ch == '\n')
                throw new ValidationException("启动参数不能包含换行或空字符。");
            if (ch == '\\')
            {
                var count = 1;
                while (i + 1 < input.Length && input[i + 1] == '\\') { count++; i++; }
                if (i + 1 < input.Length && input[i + 1] == '"')
                {
                    token.Append('\\', count / 2);
                    i++;
                    if (count % 2 == 0) quoted = !quoted;
                    else token.Append('"');
                }
                else token.Append('\\', count);
                started = true;
            }
            else if (ch == '"') { quoted = !quoted; started = true; }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (started) { result.Add(token.ToString()); token.Clear(); started = false; }
            }
            else { token.Append(ch); started = true; }
        }
        if (quoted) throw new ValidationException("启动参数中的双引号未闭合。");
        if (started) result.Add(token.ToString());
        return result;
    }

    public static void Validate(string input, bool isJvm)
    {
        string[] reserved = isJvm
            ? ["-cp", "-classpath", "--class-path", "-jar", "-Xmx", "-Xms", "@"]
            : ["--accessToken", "--username", "--uuid", "--userType", "--gameDir", "--assetsDir", "--assetIndex", "--version", "--width", "--height", "--fullscreen", "--clientId", "--xuid", "--demo"];
        foreach (var argument in Parse(input))
        {
            if (reserved.Any(key => argument.Equals(key, StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)
                || (key is "-Xmx" or "-Xms" or "@" && argument.StartsWith(key, StringComparison.OrdinalIgnoreCase))))
                throw new ValidationException("账户、内存、窗口与游戏目录参数由启动器管理，请使用对应设置项。");
        }
    }
}
