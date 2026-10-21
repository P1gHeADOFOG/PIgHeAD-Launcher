using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PIgHeAD.Launcher.Services.Minecraft;

public static class JavaRuntimeProbe
{
    public static int ParseMajorVersion(string output)
    {
        var match = Regex.Match(output, "(?:version\\s+\"|openjdk\\s+)(\\d+)(?:\\.(\\d+))?", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new InvalidOperationException("无法识别 Java 版本，请配置有效的 java.exe 或 javaw.exe。");
        }
        var major = int.Parse(match.Groups[1].Value);
        return major == 1 && match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : major;
    }

    public static async Task<int> CheckAsync(string executable, int minimumMajor, CancellationToken cancellationToken)
    {
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("找不到 Java。请设置 Java 路径，或允许下载以安装对应运行时。", executable);
        }
        var probePath = Path.Combine(Path.GetDirectoryName(executable)!, "java.exe");
        if (!File.Exists(probePath))
        {
            probePath = executable;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(probePath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true
            }
        };
        process.StartInfo.ArgumentList.Add("-version");
        process.Start();
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var major = ParseMajorVersion(await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
            if (process.ExitCode != 0 || major < minimumMajor)
            {
                throw new InvalidOperationException($"此版本需要 Java {minimumMajor} 或更新版本，当前为 Java {major}。");
            }
            return major;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Java 检查超时，请检查所配置的可执行文件。");
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* The probe exited between the check and Kill. */ }
            }
        }
    }
}
