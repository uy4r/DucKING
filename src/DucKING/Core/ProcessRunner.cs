using System.Diagnostics;
using System.Text;

namespace DucKING.Core;

public sealed record ProcessResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;
}

public static class ProcessRunner
{
    /// <param name="logFailure">Başarısızlığın beklenen bir durum olduğu sorgularda (ör. "var mı?") false verin.</param>
    public static ProcessResult Run(string fileName, string arguments, int timeoutMs = 60_000, Encoding? encoding = null, bool logFailure = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            Log.Error($"Zaman aşımı: {fileName} {arguments}");
            return new ProcessResult(-1, output.ToString());
        }

        process.WaitForExit();
        var result = new ProcessResult(process.ExitCode, output.ToString().Trim());
        if (!result.Success && logFailure)
            Log.Error($"{fileName} {arguments} -> {result.ExitCode}: {result.Output}");
        return result;
    }

    /// <summary>
    /// Betiği -EncodedCommand ile çalıştırır; böylece tırnak/kaçış sorunları olmaz.
    /// Çıktı UTF-8 olarak alınır (Türkçe karakterler bozulmasın diye).
    /// </summary>
    public static ProcessResult PowerShell(string script, int timeoutMs = 120_000)
    {
        var full = "$ErrorActionPreference='Stop';[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ProgressPreference='SilentlyContinue';\n" + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        return Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            timeoutMs, Encoding.UTF8);
    }

    /// <summary>PowerShell'e tek tırnaklı string olarak güvenle gömmek için.</summary>
    public static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";
}
