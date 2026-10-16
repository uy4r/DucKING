using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DucKING.Core;
using DucKING.Modules.Games;

namespace DucKING.Modules.Dpi;

public sealed class DpiPresetFile
{
    public int Schema { get; set; }
    public int Version { get; set; }

    /// <summary>Araç kimliği (goodbyedpi, zapret, byedpi) → presetler.</summary>
    public Dictionary<string, List<DpiPresetEntry>> Tools { get; set; } = [];
}

public sealed class DpiPresetEntry
{
    public LocalizedText? Name { get; set; }
    public string Arguments { get; set; } = "";

    /// <summary>turkey, russia ya da boş.</summary>
    public string? Region { get; set; }

    /// <summary>Presetin alındığı yer (https).</summary>
    public string? Source { get; set; }
}

/// <summary>
/// DPI presetleri: programla gelen Profiles/dpi-presets.json ya da GitHub'dan indirilen daha yeni sürüm (oyun
/// profilleriyle aynı düzen). Argümanlar SYSTEM olarak çalışan bir servise verildiğinden indirilen dosya da yalnızca
/// veri sayılır: her araç için izin verilen seçenekler dışında hiçbir şey kabul edilmez, dosya yolları yalnızca
/// paketteki files\ klasörünü gösterebilir.
/// </summary>
public static partial class DpiPresets
{
    /// <summary>
    /// Depo GitHub'a yüklendiğinde buraya ham dosya adresi yazılır, örneğin:
    /// https://raw.githubusercontent.com/KULLANICI/DucKING/main/src/DucKING/Profiles/dpi-presets.json
    /// Boşken çevrim içi güncelleme kapalıdır.
    /// </summary>
    public const string UpdateUrl = "";

    public const int SupportedSchema = 1;
    private const string EmbeddedResource = "DucKING.Profiles.dpi-presets.json";
    private const int MaxPresetsPerTool = 40;
    private const int MaxArgumentsLength = 2000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Tembel yükleme: doğrulamanın kullandığı izin listeleri (aşağıdaki statik alanlar) hazır olmadan dosya okunmasın.
    private static DpiPresetFile? _current;
    private static Dictionary<string, IReadOnlyList<DpiPreset>>? _presets;

    public static DpiPresetFile Current => _current ??= LoadEmbedded();

    private static bool _fromGitHub;

    /// <summary>"yerleşik" ya da "GitHub".</summary>
    public static string Source => _fromGitHub ? "GitHub" : Loc.T("yerleşik", "built-in", "встроенные");

    public static IReadOnlyList<DpiPreset> For(string toolId) => (_presets ??= Build(Current)).GetValueOrDefault(toolId) ?? [];

    private static DpiPresetFile LoadEmbedded()
    {
        try
        {
            using var stream = typeof(DpiPresets).Assembly.GetManifestResourceStream(EmbeddedResource)
                               ?? throw new FileNotFoundException(EmbeddedResource);
            var file = JsonSerializer.Deserialize<DpiPresetFile>(stream, Json);
            if (file is not null && Validate(file) is not { } error) return file;
            Log.Error($"Gömülü DPI preset dosyası geçersiz: {(file is null ? "boş" : Validate(file))}");
        }
        catch (Exception ex)
        {
            Log.Error("Gömülü DPI preset dosyası okunamadı", ex);
        }
        return new DpiPresetFile { Schema = SupportedSchema };
    }

    /// <summary>GitHub'daki preset dosyası daha yeniyse bu oturum için onu kullanır (diske yazılmaz). Döner: güncellendiyse true.</summary>
    public static async Task<bool> TryUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateUrl)) return false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var text = await http.GetStringAsync(UpdateUrl);
            if (text.Length > 256 * 1024) throw new InvalidDataException("Preset dosyası çok büyük.");

            var file = JsonSerializer.Deserialize<DpiPresetFile>(text, Json)
                       ?? throw new InvalidDataException("Preset dosyası boş.");
            if (Validate(file) is { } error) throw new InvalidDataException(error);
            if (file.Version <= Current.Version) return false;

            _current = file;
            _presets = Build(file);
            _fromGitHub = true;
            Log.Info($"DPI presetleri GitHub'dan güncellendi (v{file.Version}).");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("DPI presetleri güncellenemedi", ex);
            return false;
        }
    }

    private static Dictionary<string, IReadOnlyList<DpiPreset>> Build(DpiPresetFile file) =>
        file.Tools.ToDictionary(t => t.Key, t => (IReadOnlyList<DpiPreset>)t.Value
            .Select(e => new DpiPreset(e.Name!.Text, e.Arguments, ParseRegion(e.Region), string.IsNullOrEmpty(e.Source) ? null : e.Source))
            .ToList());

    private static DpiRegion ParseRegion(string? region) => region switch
    {
        "turkey" => DpiRegion.Turkey,
        "russia" => DpiRegion.Russia,
        _ => DpiRegion.None,
    };

    // ---- Doğrulama ----

    /// <summary>zapret (winws) seçenekleri. Dosya yazan (--debug, --wf-save, --hostlist-auto…), Lua/kod yükleyen ve
    /// DucKING'in kendi eklediği (--hostlist-exclude) seçenekler bilerek yoktur.</summary>
    private static readonly HashSet<string> ZapretOptions =
    [
        "new", "wf-tcp", "wf-udp", "wf-l3", "wf-tcp-in", "wf-tcp-out", "wf-udp-in", "wf-udp-out",
        "filter-tcp", "filter-udp", "filter-l3", "filter-l7", "hostlist", "hostlist-domains", "ipset", "ipset-ip", "ipset-exclude",
        "dpi-desync", "dpi-desync-ttl", "dpi-desync-ttl6", "dpi-desync-autottl", "dpi-desync-autottl6", "dpi-desync-fooling",
        "dpi-desync-repeats", "dpi-desync-split-pos", "dpi-desync-split-seqovl", "dpi-desync-split-seqovl-pattern",
        "dpi-desync-fakedsplit-pattern", "dpi-desync-fakedsplit-mod", "dpi-desync-hostfakesplit-midhost", "dpi-desync-hostfakesplit-mod",
        "dpi-desync-fake-tls", "dpi-desync-fake-tls-mod", "dpi-desync-fake-quic", "dpi-desync-fake-http", "dpi-desync-fake-discord",
        "dpi-desync-fake-stun", "dpi-desync-fake-unknown", "dpi-desync-fake-unknown-udp", "dpi-desync-fake-syndata",
        "dpi-desync-fake-wireguard", "dpi-desync-fake-dht", "dpi-desync-udplen-increment", "dpi-desync-udplen-pattern",
        "dpi-desync-any-protocol", "dpi-desync-cutoff", "dpi-desync-start", "dpi-desync-badseq-increment", "dpi-desync-badack-increment",
        "dpi-desync-ts-increment", "dpi-desync-skip-nosni", "dpi-desync-tcp-flags-set", "dpi-desync-tcp-flags-unset",
        "orig-ttl", "orig-ttl6", "orig-autottl", "orig-autottl6", "orig-mod-start", "orig-mod-cutoff", "orig-tcp-flags-set", "orig-tcp-flags-unset",
        "dup", "dup-ttl", "dup-ttl6", "dup-autottl", "dup-autottl6", "dup-fooling", "dup-start", "dup-cutoff", "dup-replace",
        "dup-tcp-flags-set", "dup-tcp-flags-unset", "ip-id", "ctrack-timeouts", "ctrack-disable", "wssize", "wssize-cutoff",
        "synack-split", "methodeol", "hostcase", "hostspell", "hostnospace", "domcase",
    ];

    /// <summary>Değeri dosya olabilen zapret seçenekleri; dosya yalnızca "{dir}\files\ad" biçiminde ve pakette varsa kabul edilir.</summary>
    private static readonly HashSet<string> ZapretFileOptions =
    [
        "hostlist", "ipset", "ipset-exclude", "dpi-desync-split-seqovl-pattern", "dpi-desync-fakedsplit-pattern", "dpi-desync-fake-tls",
        "dpi-desync-fake-quic", "dpi-desync-fake-http", "dpi-desync-fake-discord", "dpi-desync-fake-stun", "dpi-desync-fake-unknown",
        "dpi-desync-fake-unknown-udp", "dpi-desync-fake-syndata", "dpi-desync-fake-wireguard", "dpi-desync-fake-dht", "dpi-desync-udplen-pattern",
    ];

    /// <summary>GoodbyeDPI seçenekleri; --blacklist'i DucKING kendisi ekler.</summary>
    private static readonly HashSet<string> GoodbyeDpiOptions =
    [
        "--port", "--ip-id", "--dns-addr", "--dns-port", "--dnsv6-addr", "--dnsv6-port", "--dns-verb", "--allow-no-sni", "--frag-by-sni",
        "--set-ttl", "--auto-ttl", "--min-ttl", "--wrong-chksum", "--wrong-seq", "--native-frag", "--reverse-frag", "--max-payload",
        "--fake-from-hex", "--fake-gen", "--fake-resend", "--fake-with-sni",
    ];

    private const string GoodbyeDpiShort = "pqrsmfkneaw123456789";

    /// <summary>ByeDPI (ciadpi) seçenekleri; dinleme adresi/portu (DucKING ekler), dosya okuyan/yazan ve arka plan seçenekleri yoktur.</summary>
    private static readonly HashSet<string> ByeDpiOptions =
    [
        "--split", "--disorder", "--oob", "--disoob", "--fake", "--ttl", "--tlsrec", "--udp-fake", "--proto", "--auto", "--auto-mode",
        "--timeout", "--fake-offset", "--fake-tls-mod", "--fake-sni", "--tls-sni", "--mod-http", "--md5sig", "--drop-sack", "--def-ttl",
        "--buf-size", "--cache-ttl", "--max-conn", "--no-domain", "--no-udp", "--tfo", "--pf", "--round",
    ];

    private const string ByeDpiShort = "sdoqftraKALTOQnMSYgbucNUFRV";

    [GeneratedRegex(@"^--([a-z0-9-]+)(?:=(.*))?$")]
    private static partial Regex ZapretToken();

    [GeneratedRegex(@"^[A-Za-z0-9_.,:=!+\-]*$")]
    private static partial Regex SafeValue();

    [GeneratedRegex(@"^""\{dir\}\\files\\([A-Za-z0-9_.\-]{1,80})""$")]
    private static partial Regex BundledFile();

    [GeneratedRegex(@"^[\x20-\x7E]+$")]
    private static partial Regex PrintableAscii();

    public static string? Validate(DpiPresetFile file)
    {
        if (file.Schema != SupportedSchema) return $"Desteklenmeyen şema: {file.Schema}";
        if (file.Version < 1) return "Sürüm numarası geçersiz.";

        foreach (var tool in DpiService.Tools)
        {
            if (!file.Tools.TryGetValue(tool.Id, out var list) || list.Count == 0) return $"{tool.Id}: preset yok.";
        }

        foreach (var (toolId, list) in file.Tools)
        {
            var tool = DpiService.Tools.FirstOrDefault(t => t.Id == toolId);
            if (tool is null) return $"Bilinmeyen araç: {toolId}";
            if (list.Count > MaxPresetsPerTool) return $"{toolId}: en fazla {MaxPresetsPerTool} preset olabilir.";

            foreach (var entry in list)
            {
                var name = entry.Name?.Text ?? "";
                if (name.Length is 0 or > 60 || entry.Name!.Variants.Values.Any(v => v.Length is 0 or > 60))
                    return $"{toolId}: preset adı boş ya da çok uzun.";
                if (entry.Region is not (null or "turkey" or "russia")) return $"{toolId} / {name}: bilinmeyen bölge.";
                if (!string.IsNullOrEmpty(entry.Source)
                    && (entry.Source.Length > 300 || !Uri.TryCreate(entry.Source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                    return $"{toolId} / {name}: kaynak bir https adresi olmalı.";
                if (ValidateArguments(tool, entry.Arguments) is { } error) return $"{toolId} / {name}: {error}";
            }
        }
        return null;
    }

    private static string? ValidateArguments(DpiTool tool, string arguments)
    {
        if (arguments.Length is 0 or > MaxArgumentsLength || !PrintableAscii().IsMatch(arguments)) return "argümanlar boş, çok uzun ya da geçersiz karakterli.";

        var tokens = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tool == DpiService.Zapret) return ValidateZapret(tool, tokens);
        if (tool == DpiService.GoodbyeDpi) return ValidateGetopt(tokens, GoodbyeDpiOptions, GoodbyeDpiShort);
        return ValidateGetopt(tokens, ByeDpiOptions, ByeDpiShort);
    }

    private static string? ValidateZapret(DpiTool tool, string[] tokens)
    {
        foreach (var token in tokens)
        {
            var match = ZapretToken().Match(token);
            if (!match.Success) return $"beklenmeyen öğe: {token}";
            var option = match.Groups[1].Value;
            if (!ZapretOptions.Contains(option)) return $"izin verilmeyen seçenek: --{option}";
            if (!match.Groups[2].Success) continue;

            var value = match.Groups[2].Value;
            if (value.StartsWith('"'))
            {
                var file = BundledFile().Match(value);
                if (!ZapretFileOptions.Contains(option) || !file.Success) return $"geçersiz dosya değeri: {token}";
                if (!File.Exists(Path.Combine(AppPaths.BundledToolsDir, tool.Folder, "files", file.Groups[1].Value)))
                    return $"pakette olmayan dosya: {file.Groups[1].Value}";
            }
            else if (!SafeValue().IsMatch(value))
            {
                return $"geçersiz değer: {token}";
            }
        }
        return null;
    }

    /// <summary>GoodbyeDPI ve ByeDPI: "--uzun[=değer]", "-k[değer]" ya da ayrı değer (negatif sayılar dahil).</summary>
    private static string? ValidateGetopt(string[] tokens, HashSet<string> longOptions, string shortOptions)
    {
        foreach (var token in tokens)
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var eq = token.IndexOf('=');
                var option = eq < 0 ? token : token[..eq];
                if (!longOptions.Contains(option)) return $"izin verilmeyen seçenek: {option}";
                if (eq >= 0 && !SafeValue().IsMatch(token[(eq + 1)..])) return $"geçersiz değer: {token}";
            }
            else if (token.Length >= 2 && token[0] == '-')
            {
                // "-5" GoodbyeDPI'da kip seçeneği, ByeDPI'da ("--fake -1") negatif sayı değeridir.
                if (char.IsAsciiDigit(token[1]) && !shortOptions.Contains(token[1]))
                {
                    if (!SafeValue().IsMatch(token)) return $"geçersiz değer: {token}";
                    continue;
                }
                if (!shortOptions.Contains(token[1])) return $"izin verilmeyen seçenek: -{token[1]}";
                if (!SafeValue().IsMatch(token[2..])) return $"geçersiz değer: {token}";
            }
            else if (!SafeValue().IsMatch(token))
            {
                return $"geçersiz değer: {token}";
            }
        }
        return null;
    }
}
