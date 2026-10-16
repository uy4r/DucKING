using System.Diagnostics;
using System.IO;
using DucKING.Core;

namespace DucKING.Modules.Discord;

/// <summary>Kapatılmadan önce açık olan Discord sürümlerinin çalıştırılabilir dosya yolları.</summary>
public sealed record DiscordSession(IReadOnlyList<string> ExePaths)
{
    public bool WasRunning => ExePaths.Count > 0;
}

/// <summary>
/// DNS/DPI değişikliği Discord'un açık bağlantılarına ve DNS önbelleğine ancak yeniden başlatınca yansır.
/// Bu sınıf açık Discord'u (Stable, PTB, Canary) kapatır ve sonra kullanıcı yetkisiyle yeniden açar.
/// </summary>
public static class DiscordController
{
    private static readonly string[] ProcessNames = ["Discord", "DiscordPTB", "DiscordCanary"];

    private static readonly Dictionary<string, string> ShortcutNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Discord.exe"] = "Discord.lnk",
        ["DiscordPTB.exe"] = "Discord PTB.lnk",
        ["DiscordCanary.exe"] = "Discord Canary.lnk",
    };

    public static DiscordSession Close()
    {
        var paths = new List<string>();
        foreach (var name in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (path is not null && !paths.Contains(path, StringComparer.OrdinalIgnoreCase)) paths.Add(path);
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Ağaç kapatılırken süreç zaten sonlanmış olabilir.
                    }
                }
            }
        }

        if (paths.Count > 0) Log.Info($"Discord kapatıldı ({string.Join(", ", paths.Select(Path.GetFileName))}).");
        return new DiscordSession(paths);
    }

    /// <summary>Kapatılan sürümleri yeniden başlatır (Discord bu arada güncellendiyse en yeni app-* klasöründen).</summary>
    public static void Start(DiscordSession session)
    {
        foreach (var exe in session.ExePaths)
        {
            try
            {
                var target = FindShortcut(Path.GetFileName(exe)) ?? FindLatestExe(exe);
                if (target is null)
                {
                    Log.Error($"Discord yeniden başlatılamadı: {exe} bulunamadı");
                    continue;
                }
                StartUnelevated(target);
                Log.Info($"Discord yeniden başlatıldı ({Path.GetFileName(target)}).");
            }
            catch (Exception ex)
            {
                Log.Error("Discord yeniden başlatılamadı", ex);
            }
        }
    }

    /// <summary>
    /// DucKING yönetici olarak çalışır; Discord'u doğrudan başlatırsak o da yönetici olur. explorer.exe'ye
    /// verilen yol, oturumdaki mevcut Gezgin üzerinden normal kullanıcı yetkisiyle açılır.
    /// </summary>
    private static void StartUnelevated(string path)
    {
        if (!AppPaths.IsElevated)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false })?.Dispose();
    }

    /// <summary>Başlat menüsü/masaüstü kısayolu Discord'un kendi güncelleyicisi (Update.exe) üzerinden açar.</summary>
    private static string? FindShortcut(string exeName)
    {
        if (!ShortcutNames.TryGetValue(exeName, out var shortcut)) return null;
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Discord Inc", shortcut),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), shortcut),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindLatestExe(string exe)
    {
        // ...\Discord\app-1.0.9261\Discord.exe → ...\Discord\app-*\Discord.exe içinde en yenisi
        var appDir = Path.GetDirectoryName(exe);
        var root = appDir is null ? null : Path.GetDirectoryName(appDir);
        if (root is null || !Directory.Exists(root)) return File.Exists(exe) ? exe : null;

        return Directory.EnumerateDirectories(root, "app-*")
            .Select(d => Path.Combine(d, Path.GetFileName(exe)))
            .Where(File.Exists)
            .OrderByDescending(p => Version.TryParse(Path.GetFileName(Path.GetDirectoryName(p))!["app-".Length..], out var v) ? v : new Version())
            .FirstOrDefault();
    }
}

/// <summary>Ayar açıksa işlemi "Discord'u kapat → işlem → Discord'u aç" arasında çalıştırır.</summary>
public static class DiscordGuard
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        if (!AppSettings.Current.RestartDiscord) return await operation();

        var session = await Task.Run(DiscordController.Close);
        try
        {
            return await operation();
        }
        finally
        {
            // İşlem başarısız olsa da kullanıcı Discord'unu kaybetmesin.
            if (session.WasRunning)
            {
                await Task.Delay(800); // Bağlantılar/servis tam otursun.
                await Task.Run(() => DiscordController.Start(session));
            }
        }
    }

    public static Task RunAsync(Func<Task> operation) =>
        RunAsync(async () =>
        {
            await operation();
            return true;
        });
}
