using System.IO;
using DucKING.Modules.Dns;
using DucKING.Modules.Dpi;
using DucKING.Modules.Tweaks;

namespace DucKING.Core;

/// <summary>
/// Kurulum programı kaldırılırken "DucKING.exe --uninstall [--revert]" ile pencere açmadan çalışır.
/// Servisler ve kopyalanan araçlar her zaman kaldırılır; ayarlar yalnızca --revert ile geri alınır.
/// </summary>
public static class Uninstaller
{
    public static async Task<int> RunAsync(bool revert)
    {
        Log.Info($"Kaldırma başladı (ayarları geri al: {revert}).");
        var failed = false;

        try
        {
            await Task.Run(DpiService.RemoveAll);
        }
        catch (Exception ex)
        {
            failed = true;
            Log.Error("DPI servisleri kaldırılamadı", ex);
        }

        if (revert)
        {
            // Yalnızca şu an uygulanmış görünen ayarlar geri alınır; DucKING'in dokunmadığı değerler değişmez.
            await TweakCatalog.RefreshAllAsync();
            var count = await TweakCatalog.RevertAllAsync(TweakCatalog.All);
            Log.Info($"{count} ayar geri alındı.");

            if (BackupStore.Keys.Any(k => k.StartsWith("dns|", StringComparison.Ordinal)))
            {
                try { await Task.Run(DnsService.Restore); }
                catch (Exception ex)
                {
                    failed = true;
                    Log.Error("DNS geri alınamadı", ex);
                }
            }
        }

        TryDelete(AppPaths.InstalledToolsDir);
        Log.Info("Kaldırma tamamlandı.");

        // Geri alınmadıysa yedekler, yeniden kurulumda geri alınabilsin diye saklanır.
        if (revert && !failed && BackupStore.Keys.Count == 0)
            TryDelete(AppPaths.DataDir);

        return failed ? 1 : 0;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Error($"Silinemedi: {dir}", ex);
        }
    }
}
