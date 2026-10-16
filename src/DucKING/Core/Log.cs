using System.IO;

namespace DucKING.Core;

public static class Log
{
    private static readonly object Sync = new();

    /// <summary>Arayüzdeki durum çubuğuna kısa mesaj iletmek için.</summary>
    public static event Action<string, bool>? Status;

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.Message}");

    /// <summary>Hem dosyaya yazar hem de kullanıcıya gösterir.</summary>
    public static void Notify(string message, bool isError = false)
    {
        Write(isError ? "ERROR" : "INFO", message);
        Status?.Invoke(message, isError);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(AppPaths.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Loglama hiçbir zaman asıl işlemi bozmamalı.
        }
    }
}
