using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using DucKING.Core;

namespace DucKING.Modules.Dpi;

public enum FinderOutcome
{
    /// <summary>Araç olmadan da tüm siteler açılıyor; preset aranmadı.</summary>
    NotBlocked,

    /// <summary>Araçsız duruma göre daha çok site açan bir preset bulundu ve kuruldu.</summary>
    Installed,

    /// <summary>Hiçbir preset araçsız durumdan iyi değil; önceki kurulum geri başlatıldı.</summary>
    NothingBetter,
}

/// <param name="Failed">Seçilen presetle (ya da araçsız) açılmayan sitelerin adları.</param>
public sealed record FinderResult(FinderOutcome Outcome, DpiPreset? Preset, int Passed, int Total, IReadOnlyList<string> Failed);

/// <summary>
/// Bir aracın presetlerini servis kurmadan sırayla kısa süre çalıştırıp engelli sitelerin gerçekten açılıp açılmadığına
/// bakar (zapret'in blockcheck aracındaki fikir). Önce araçsız ölçülür; site engeli yoksa preset aranmaz. Kurulu
/// DPI servisleri deneme boyunca durdurulur; daha iyi bir preset bulunamazsa yeniden başlatılır.
/// Discord sesli sohbet (UDP) gerçek bir ses sunucusu gerektirdiğinden denenmez.
/// </summary>
public static class PresetFinder
{
    // Türkiye'de ve Rusya'da engellenen siteler. Yanıt sonuna kadar okunur: ilk 16 KB'den sonra bağlantıyı
    // donduran engeller (Rusya'da Cloudflare) de böylece başarısız sayılır. Herhangi bir HTTP yanıtı yeterlidir.
    // Discord'un güncelleyicisi TLS 1.2 kullanır; bazı yöntemler yalnızca TLS 1.3'ü geçirdiğinden ayrıca denenir
    // (yoksa Discord açılış ekranında "güncelleme başarısız" diye takılır).
    private static readonly (string Name, string Url, bool Tls12)[] Targets =
    [
        ("Discord", "https://discord.com/", false),
        ("Discord CDN", "https://cdn.discordapp.com/", false),
        ("Discord güncelleme (TLS 1.2)", "https://updates.discord.com/distributions/app/manifests/latest?channel=stable&platform=win&arch=x64", true),
        ("YouTube", "https://www.youtube.com/", false),
        ("YouTube video", "https://redirector.googlevideo.com/report_mapping", false),
        ("Roblox", "https://www.roblox.com/", false),
    ];

    private static readonly TimeSpan SiteTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMilliseconds(1500);
    private const int MaxBodyBytes = 4 * 1024 * 1024;

    /// <summary>Deneme sırasında ByeDPI'ın dinlediği port (kurulumdaki 1080 ile karışmasın).</summary>
    private const int TestSocksPort = 10801;

    /// <param name="presets">Denenme sırası; eşit sonuçlarda öndeki seçilir.</param>
    /// <param name="option">Kurulumdaki seçenek; denemeler kurulacak argümanların aynısıyla yapılır.</param>
    /// <param name="progress">Kullanıcıya gösterilecek ilerleme metni.</param>
    public static async Task<FinderResult> RunAsync(DpiTool tool, IReadOnlyList<DpiPreset> presets, bool option, IProgress<string> progress)
    {
        var paused = await Task.Run(DpiService.PauseAll);
        var installed = false;
        try
        {
            progress.Report(Loc.T("Araç olmadan deneniyor…", "Testing without a tool…", "Проверка без инструмента…"));
            var baseline = await TestSitesAsync(socksPort: null);
            Log.Info($"PresetFinder: araçsız {Targets.Length - baseline.Count}/{Targets.Length} ({string.Join(", ", baseline)})");
            if (baseline.Count == 0)
                return new FinderResult(FinderOutcome.NotBlocked, null, Targets.Length, Targets.Length, []);

            await Task.Run(() => DpiService.CopyTool(tool));
            (DpiPreset Preset, List<string> Failed)? best = null;
            for (var i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                progress.Report(Loc.T($"Deneniyor: {preset.Name} ({i + 1}/{presets.Count})…",
                    $"Testing: {preset.Name} ({i + 1}/{presets.Count})…",
                    $"Проверка: {preset.Name} ({i + 1}/{presets.Count})…"));

                var failed = await TryPresetAsync(tool, preset, option);
                Log.Info($"PresetFinder: {tool.Name} / {preset.Name}: {Targets.Length - failed.Count}/{Targets.Length}"
                         + (failed.Count > 0 ? $" ({string.Join(", ", failed)})" : ""));
                if (best is null || failed.Count < best.Value.Failed.Count) best = (preset, failed);
                if (failed.Count == 0) break;
            }

            if (best is not { } winner || winner.Failed.Count >= baseline.Count)
                return new FinderResult(FinderOutcome.NothingBetter, null, Targets.Length - baseline.Count, Targets.Length, baseline);

            progress.Report(Loc.T($"Kuruluyor: {winner.Preset.Name}…", $"Installing: {winner.Preset.Name}…", $"Установка: {winner.Preset.Name}…"));
            await Task.Run(() => DpiService.Install(tool, winner.Preset, option));
            installed = true;
            return new FinderResult(FinderOutcome.Installed, winner.Preset, Targets.Length - winner.Failed.Count, Targets.Length, winner.Failed);
        }
        finally
        {
            // Kurulum yapılmadıysa kullanıcının önceki aracı eski hâline döner (Install zaten hepsini kaldırır).
            if (!installed) await Task.Run(() => DpiService.ResumeAll(paused));
        }
    }

    /// <summary>Preseti geçici bir süreç olarak başlatır, siteleri dener, süreci kapatır. Döner: açılmayan siteler.</summary>
    private static async Task<List<string>> TryPresetAsync(DpiTool tool, DpiPreset preset, bool option)
    {
        var isProxy = tool == DpiService.ByeDpi;
        var args = DpiService.BuildArguments(tool, preset, option, TestSocksPort);
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo(tool.ExePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tool.InstallDir,
            }) ?? throw new InvalidOperationException($"{tool.Executable} başlatılamadı");

            // WinDivert'in açılması / proxy'nin dinlemeye başlaması için kısa bekleme.
            await Task.Delay(StartupDelay);
            if (process.HasExited)
            {
                Log.Error($"PresetFinder: {tool.Executable} hemen kapandı (çıkış kodu {process.ExitCode}): {args}");
                return Targets.Select(t => t.Name).ToList();
            }
            return await TestSitesAsync(isProxy ? TestSocksPort : null);
        }
        catch (Exception ex)
        {
            Log.Error($"PresetFinder: {preset.Name} denenemedi", ex);
            return Targets.Select(t => t.Name).ToList();
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"PresetFinder: {tool.Executable} kapatılamadı", ex);
                }
                process.Dispose();
            }
        }
    }

    /// <summary>Tüm siteleri aynı anda dener. Döner: açılmayanların adları.</summary>
    private static async Task<List<string>> TestSitesAsync(int? socksPort)
    {
        // Her denemede yeni bağlantılar: önceki presetin açtığı bağlantı yeniden kullanılmasın.
        using var http = CreateClient(socksPort, tls12Only: false);
        using var http12 = CreateClient(socksPort, tls12Only: true);

        var results = await Task.WhenAll(Targets.Select(async t => (t.Name, Ok: await OpensAsync(t.Tls12 ? http12 : http, t.Url))));
        return results.Where(r => !r.Ok).Select(r => r.Name).ToList();
    }

    private static HttpClient CreateClient(int? socksPort, bool tls12Only)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = socksPort is not null,
            Proxy = socksPort is { } port ? new WebProxy($"socks5://127.0.0.1:{port}") : null,
            ConnectTimeout = SiteTimeout,
        };
        if (tls12Only) handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls12;
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        return http;
    }

    private static async Task<bool> OpensAsync(HttpClient http, string url)
    {
        using var cts = new CancellationTokenSource(SiteTimeout);
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[64 * 1024];
            long total = 0;
            int read;
            while (total < MaxBodyBytes && (read = await body.ReadAsync(buffer, cts.Token)) > 0)
                total += read;
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return false;
        }
    }
}
