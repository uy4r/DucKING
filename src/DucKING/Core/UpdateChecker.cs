using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DucKING.Core;

/// <param name="Version">Sürüm etiketinden okunan sürüm (ör. v0.5.0 → 0.5.0).</param>
/// <param name="PageUrl">GitHub'daki sürüm sayfası; yalnızca bu deponun github.com adresi kabul edilir.</param>
public sealed record UpdateInfo(Version Version, string Tag, string PageUrl);

/// <summary>
/// Yeni sürüm denetimi: yalnızca bildirir, hiçbir şey indirmez ya da kurmaz. GitHub'ın herkese açık
/// "son sürüm" adresine tek bir GET isteği atılır; istekte DucKING'in sürümü dışında hiçbir bilgi gönderilmez.
/// <see cref="Repository"/> boşken özellik tamamen kapalıdır ve ağa hiç çıkılmaz.
/// </summary>
public static partial class UpdateChecker
{
    /// <summary>
    /// Depo GitHub'a yüklendiğinde buraya "KULLANICI/DucKING" biçiminde depo adı yazılır.
    /// Boşken güncelleme denetimi kapalıdır (Hakkında'daki düğme de gizlenir).
    /// </summary>
    public const string Repository = "";

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(Repository);

    /// <summary>Çalışan DucKING'in sürümü (ör. 0.4.0.0; gösterirken ToString(3)).</summary>
    public static Version CurrentVersion { get; } = Normalize(typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Bu oturumda bulunan daha yeni sürüm; yoksa null.</summary>
    public static UpdateInfo? Available { get; private set; }

    private const int MaxResponseBytes = 1024 * 1024;

    /// <summary>Açılışta arka planda bir kez çağrılır. Hatalar yalnızca günlüğe yazılır, kullanıcıya gösterilmez.</summary>
    public static async Task CheckAtStartupAsync()
    {
        if (!IsEnabled) return;
        try
        {
            // Pencere açılırken yapılan diğer işlerle yarışmasın.
            await Task.Delay(TimeSpan.FromSeconds(3));
            await CheckAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Güncelleme denetlenemedi", ex);
        }
    }

    /// <summary>
    /// Son sürümü sorar; daha yeniyse <see cref="Available"/>'a yazar ve kullanıcıya bildirir.
    /// Döner: daha yeni sürüm ya da null (güncel, henüz sürüm yayınlanmamış ya da özellik kapalı). Ağ hatasında hata fırlatır.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        if (!IsEnabled) return null;
        var latest = await FetchLatestAsync(Repository);
        if (latest is null || latest.Version <= CurrentVersion)
        {
            Log.Info($"Güncelleme denetlendi: v{CurrentVersion.ToString(3)} güncel" + (latest is null ? " (yayınlanmış sürüm yok)." : $" (son sürüm {latest.Tag})."));
            return null;
        }

        Available = latest;
        Log.Notify(Loc.T($"Yeni sürüm çıktı: DucKING {latest.Tag}. Sürüm sayfasını Hakkında bölümünden açabilirsiniz.",
            $"A new version is out: DucKING {latest.Tag}. You can open its release page from About.",
            $"Вышла новая версия: DucKING {latest.Tag}. Страницу выпуска можно открыть в разделе «О программе»."));
        return latest;
    }

    /// <summary>GitHub'daki son (taslak ve ön sürüm olmayan) sürüm. Depoda hiç sürüm yoksa null.</summary>
    internal static async Task<UpdateInfo?> FetchLatestAsync(string repository)
    {
        if (!RepositoryPattern().IsMatch(repository)) throw new ArgumentException($"Geçersiz depo adı: {repository}");

        // İzleme başlığı (traceparent) eklenmesin, çerez tutulmasın: istekte yalnızca aşağıdaki iki başlık olur.
        var handler = new SocketsHttpHandler { ActivityHeadersPropagator = null, UseCookies = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = MaxResponseBytes };
        // GitHub API'si User-Agent'sız istekleri reddeder; yalnızca program adı ve sürümü gönderilir.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DucKING", CurrentVersion.ToString(3)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.GetAsync($"https://api.github.com/repos/{repository}/releases/latest");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(), repository);
    }

    /// <summary>GitHub yanıtından sürümü ve sayfa adresini okur.</summary>
    internal static UpdateInfo Parse(string json, string repository)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
        var version = ParseTag(tag) ?? throw new InvalidDataException($"Sürüm etiketi okunamadı: \"{tag}\"");

        // Sayfa adresi yalnızca bu deponun github.com sayfasıysa kullanılır; değilse sürümler sayfası açılır.
        var expected = $"https://github.com/{repository}/";
        var url = root.TryGetProperty("html_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        if (url is null || !url.StartsWith(expected, StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            url = $"{expected}releases/latest";
        return new UpdateInfo(version, tag.Trim(), url);
    }

    /// <summary>"v0.4.0", "0.4", "v1.2.3-beta" gibi etiketlerden sürümü okur; sayılardan sonraki ek (-beta, rc1) yok sayılır.</summary>
    internal static Version? ParseTag(string? tag)
    {
        var match = TagPattern().Match(tag?.Trim() ?? "");
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? Normalize(version) : null;
    }

    /// <summary>0.4, 0.4.0 ve 0.4.0.0 aynı sayılsın diye eksik parçalar sıfırla doldurulur.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    [GeneratedRegex(@"^[A-Za-z0-9-]{1,39}/[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepositoryPattern();

    [GeneratedRegex(@"^[vV]?(\d{1,9}(?:\.\d{1,9}){1,3})(?![\d.]*\d)")]
    private static partial Regex TagPattern();
}
