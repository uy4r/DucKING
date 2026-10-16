using DucKING.Core;

namespace DucKING.Modules.Dns;

public sealed class DnsProvider : ObservableObject
{
    private double? _latencyMs;
    private bool _isTesting;
    private bool _isActive;
    private bool _isFastest;
    private bool _testFailed;

    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Ipv4 { get; init; }
    public required string[] Ipv6 { get; init; }
    public required string DohTemplate { get; init; }

    public string AddressText => string.Join("  ·  ", Ipv4);

    public double? LatencyMs
    {
        get => _latencyMs;
        set
        {
            if (Set(ref _latencyMs, value)) OnPropertyChanged(nameof(LatencyText));
        }
    }

    public bool TestFailed
    {
        get => _testFailed;
        set
        {
            if (Set(ref _testFailed, value)) OnPropertyChanged(nameof(LatencyText));
        }
    }

    public string LatencyText => IsTesting ? UiText.Get("home.measuring")
        : TestFailed ? Loc.T("yanıt yok", "no response", "нет ответа")
        : LatencyMs is { } ms ? $"{ms:0} {Loc.Ms}" : "—";

    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            if (Set(ref _isTesting, value)) OnPropertyChanged(nameof(LatencyText));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    public bool IsFastest
    {
        get => _isFastest;
        set => Set(ref _isFastest, value);
    }

    public static IReadOnlyList<DnsProvider> All { get; } =
    [
        new()
        {
            Name = "Cloudflare",
            Description = Loc.T("Dünyanın en büyük DNS ağlarından biri. Kayıt tutmama politikası.",
                "One of the world's largest DNS networks. No-logging policy.",
                "Одна из крупнейших DNS-сетей в мире. Не ведёт журналы запросов."),
            Ipv4 = ["1.1.1.1", "1.0.0.1"],
            Ipv6 = ["2606:4700:4700::1111", "2606:4700:4700::1001"],
            DohTemplate = "https://cloudflare-dns.com/dns-query",
        },
        new()
        {
            Name = "Google",
            Description = Loc.T("Çok kararlı, her yerde hızlı. Oyun sunucularına yakın CDN yönlendirmesi.",
                "Very stable and fast everywhere. CDN routing close to game servers.",
                "Очень стабильный и быстрый везде. CDN-маршрутизация ближе к игровым серверам."),
            Ipv4 = ["8.8.8.8", "8.8.4.4"],
            Ipv6 = ["2001:4860:4860::8888", "2001:4860:4860::8844"],
            DohTemplate = "https://dns.google/dns-query",
        },
        new()
        {
            Name = "Quad9",
            Description = Loc.T("Zararlı siteleri engeller, gizlilik odaklı.", "Blocks malicious sites; privacy-focused.",
                "Блокирует вредоносные сайты, ориентирован на конфиденциальность."),
            Ipv4 = ["9.9.9.9", "149.112.112.112"],
            Ipv6 = ["2620:fe::fe", "2620:fe::9"],
            DohTemplate = "https://dns.quad9.net/dns-query",
        },
        new()
        {
            Name = "AdGuard",
            Description = Loc.T("Reklam ve izleyicileri DNS seviyesinde engeller.", "Blocks ads and trackers at the DNS level.",
                "Блокирует рекламу и трекеры на уровне DNS."),
            Ipv4 = ["94.140.14.14", "94.140.15.15"],
            Ipv6 = ["2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff"],
            DohTemplate = "https://dns.adguard-dns.com/dns-query",
        },
        new()
        {
            Name = "OpenDNS",
            Description = Loc.T("Cisco altyapısı, uzun süredir kullanılan kararlı DNS.", "Cisco infrastructure; a stable, long-established DNS.",
                "Инфраструктура Cisco, стабильный DNS с долгой историей."),
            Ipv4 = ["208.67.222.222", "208.67.220.220"],
            Ipv6 = ["2620:119:35::35", "2620:119:53::53"],
            DohTemplate = "https://doh.opendns.com/dns-query",
        },
        new()
        {
            Name = "Control D",
            Description = Loc.T("Filtresiz, hızlı ve ücretsiz anycast DNS.", "Unfiltered, fast and free anycast DNS.",
                "Быстрый бесплатный anycast DNS без фильтрации."),
            Ipv4 = ["76.76.2.0", "76.76.10.0"],
            Ipv6 = ["2606:1a40::", "2606:1a40:1::"],
            DohTemplate = "https://freedns.controld.com/p0",
        },
    ];
}
