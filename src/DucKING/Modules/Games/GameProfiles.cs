using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DucKING.Core;

namespace DucKING.Modules.Games;

/// <summary>
/// Profil dosyasındaki çevrilebilir metin: düz metin ("4x MSAA") ya da dillere göre nesne
/// ({ "tr": "Kapalı", "en": "Off", "ru": "Выкл." }). Seçili dilde yoksa İngilizcesi, o da yoksa Türkçesi gösterilir.
/// </summary>
[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText(IReadOnlyDictionary<string, string> variants)
{
    /// <summary>Dil kodu → metin; düz metin boş anahtarla ("") tutulur.</summary>
    public IReadOnlyDictionary<string, string> Variants { get; } = variants;

    public string Text =>
        Variants.GetValueOrDefault(Loc.Code(Loc.Current)) ?? Variants.GetValueOrDefault("en") ?? Variants.GetValueOrDefault("tr")
        ?? Variants.GetValueOrDefault("") ?? Variants.Values.FirstOrDefault() ?? "";

    public override string ToString() => Text;
}

internal sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => new LocalizedText(new Dictionary<string, string> { [""] = reader.GetString() ?? "" }),
        JsonTokenType.StartObject => new LocalizedText(JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) ?? []),
        _ => throw new JsonException("Metin ya da { \"tr\": …, \"en\": …, \"ru\": … } bekleniyor."),
    };

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        if (value.Variants.Count == 1 && value.Variants.TryGetValue("", out var plain)) writer.WriteStringValue(plain);
        else JsonSerializer.Serialize(writer, value.Variants, options);
    }
}

public sealed class GameProfileFile
{
    public int Schema { get; set; }
    public int Version { get; set; }

    /// <summary>Tekrar kullanılan değer adları, ör. "on-off": { "0": { "tr": "Kapalı", … }, "1": … }.</summary>
    public Dictionary<string, Dictionary<string, LocalizedText>> ValueSets { get; set; } = [];

    /// <summary>Biçime göre ortak ayar adları (ör. tüm Unreal oyunları için sg.* anahtarları).</summary>
    public Dictionary<string, Dictionary<string, SettingLabel>> FormatLabels { get; set; } = [];

    public List<GameDefinition> Games { get; set; } = [];
}

/// <summary>Bir ayar anahtarının kullanıcıya gösterilen adı ve değerlerinin anlamı.</summary>
public sealed class SettingLabel
{
    public LocalizedText Name { get; set; } = new(new Dictionary<string, string>());

    /// <summary>ValueSets'teki bir kümenin adı; Values ile birlikte verilirse Values önceliklidir.</summary>
    public string? Set { get; set; }

    public Dictionary<string, LocalizedText>? Values { get; set; }
}

public sealed class GameDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int? SteamAppId { get; set; }
    public string? EpicAppName { get; set; }
    public string? RiotProduct { get; set; }

    /// <summary>Kurulum klasörüne göre oyunun exe yolu (Windows'un oyuna özel ayarları için).</summary>
    public string? Exe { get; set; }

    /// <summary>Çalışıp çalışmadığını anlamak için işlem adı (.exe olmadan).</summary>
    public string? Process { get; set; }

    public GameConfigLocation? Config { get; set; }

    /// <summary>Bu oyuna özel ayar adları; biçimin ortak adlarının üzerine yazar.</summary>
    public Dictionary<string, SettingLabel> Labels { get; set; } = [];

    public List<GameProfile> Profiles { get; set; } = [];
}

public sealed class GameConfigLocation
{
    /// <summary>cs2-video, unreal-ini, key-value</summary>
    public string Format { get; set; } = "";

    /// <summary>steam-userdata, localappdata, documents</summary>
    public string Base { get; set; } = "";

    /// <summary>Base'e göre yol; "*" tek bir klasör seviyesini eşler (hesap/kullanıcı klasörleri).</summary>
    public string Path { get; set; } = "";
}

public sealed class GameProfile
{
    public string Id { get; set; } = "";
    public LocalizedText Name { get; set; } = new(new Dictionary<string, string>());
    public LocalizedText? Description { get; set; }
    public Dictionary<string, string> Values { get; set; } = [];
}

/// <summary>
/// Oyun profilleri: programla gelen Profiles/games.json ya da GitHub'dan indirilen daha yeni sürüm.
/// İndirilen dosya da yalnızca veri olarak kabul edilir ve katı kurallarla doğrulanır.
/// </summary>
public static partial class GameProfiles
{
    /// <summary>
    /// Depo GitHub'a yüklendiğinde buraya ham dosya adresi yazılır, örneğin:
    /// https://raw.githubusercontent.com/KULLANICI/DucKING/main/src/DucKING/Profiles/games.json
    /// Boşken çevrim içi güncelleme kapalıdır.
    /// </summary>
    public const string UpdateUrl = "";

    /// <summary>2: ad ve değer metinleri dillere göre nesne olabilir (bkz. LocalizedText).</summary>
    public const int SupportedSchema = 2;
    private static readonly string[] AllowedFormats = ["cs2-video", "unreal-ini", "key-value"];
    private static readonly string[] AllowedBases = ["steam-userdata", "localappdata", "documents"];
    private static readonly string[] AllowedExtensions = [".ini", ".txt", ".cfg"];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Oyun listesi yalnızca geliştiricinin yayınladığı kaynaklardan gelir: exe'ye gömülü dosya ya da
    /// UpdateUrl'deki GitHub dosyası. Diskte düzenlenebilir bir kopya tutulmaz; indirilen güncelleme
    /// yalnızca bellekte kullanılır. Bu yüzden kullanıcılar listeye oyun ekleyemez.
    /// </summary>
    private const string EmbeddedResource = "DucKING.Profiles.games.json";

    public static GameProfileFile Current { get; private set; } = LoadEmbedded();

    private static bool _fromGitHub;

    /// <summary>"yerleşik" ya da "GitHub".</summary>
    public static string Source => _fromGitHub ? "GitHub" : Loc.T("yerleşik", "built-in", "встроенные");

    private static GameProfileFile LoadEmbedded()
    {
        try
        {
            using var stream = typeof(GameProfiles).Assembly.GetManifestResourceStream(EmbeddedResource)
                               ?? throw new FileNotFoundException(EmbeddedResource);
            var file = JsonSerializer.Deserialize<GameProfileFile>(stream, Json);
            if (file is not null && Validate(file) is not { } error) return file;
            Log.Error($"Gömülü profil dosyası geçersiz: {(file is null ? "boş" : Validate(file))}");
        }
        catch (Exception ex)
        {
            Log.Error("Gömülü profil dosyası okunamadı", ex);
        }
        return new GameProfileFile { Schema = SupportedSchema };
    }

    /// <summary>GitHub'daki profil dosyası daha yeniyse bu oturum için onu kullanır (diske yazılmaz). Döner: güncellendiyse true.</summary>
    public static async Task<bool> TryUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateUrl)) return false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var text = await http.GetStringAsync(UpdateUrl);
            if (text.Length > 512 * 1024) throw new InvalidDataException("Profil dosyası çok büyük.");

            var file = JsonSerializer.Deserialize<GameProfileFile>(text, Json)
                       ?? throw new InvalidDataException("Profil dosyası boş.");
            if (Validate(file) is { } error) throw new InvalidDataException(error);
            if (file.Version <= Current.Version) return false;

            Current = file;
            _fromGitHub = true;
            Log.Info($"Oyun profilleri GitHub'dan güncellendi (v{file.Version}).");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Oyun profilleri güncellenemedi", ex);
            return false;
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.\-/]{1,96}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[a-z0-9\-]{1,40}$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Ayarın adını ve değerinin anlamını bulur. Önce oyuna özel, sonra biçimin ortak adlarına bakılır;
    /// Unreal'da "sg.ShadowQuality_BEND_STUDIO" gibi ekli anahtarlar temel adla eşleşir.
    /// Anlamı bilinmeyen değer olduğu gibi gösterilir.
    /// </summary>
    public static (string Name, Func<string?, string> Describe) DescribeSetting(GameDefinition game, string key)
    {
        var label = FindLabel(game.Labels, key, game.Config?.Format)
                    ?? (game.Config is { } config && Current.FormatLabels.TryGetValue(config.Format, out var shared)
                        ? FindLabel(shared, key, config.Format)
                        : null);
        if (label is null) return (key, value => value ?? "—");

        var values = label.Values ?? (label.Set is { } set && Current.ValueSets.TryGetValue(set, out var named) ? named : null);
        return (label.Name.Text, value => value is null ? "—" : values is not null && values.TryGetValue(value, out var text) ? text.Text : value);
    }

    private static SettingLabel? FindLabel(Dictionary<string, SettingLabel> labels, string key, string? format)
    {
        if (labels.TryGetValue(key, out var exact)) return exact;
        if (format != "unreal-ini") return null;
        return labels.FirstOrDefault(l => key.StartsWith(l.Key + "_", StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string? ValidateLabels(Dictionary<string, SettingLabel> labels, GameProfileFile file, string owner)
    {
        foreach (var (key, label) in labels)
        {
            if (!KeyPattern().IsMatch(key)) return $"{owner}: geçersiz etiket anahtarı '{key}'";
            if (!IsSafeText(label.Name, 60)) return $"{owner}: geçersiz etiket adı '{key}'";
            if (label.Set is { } set && !file.ValueSets.ContainsKey(set)) return $"{owner}: bilinmeyen değer kümesi '{set}'";
            if (label.Values is { } values && values.Any(v => v.Key.Length > 32 || !IsSafeText(v.Value, 40)))
                return $"{owner}: geçersiz değer adı '{key}'";
        }
        return null;
    }

    private static bool IsSafeText(string? text, int max) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= max && !text.Any(char.IsControl);

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex LanguagePattern();

    /// <summary>Her dil karşılığı güvenli olmalı; dil anahtarları iki harfli kod (düz metinde boş).</summary>
    private static bool IsSafeText(LocalizedText? text, int max) =>
        text is { Variants.Count: > 0 }
        && text.Variants.All(v => (v.Key.Length == 0 || LanguagePattern().IsMatch(v.Key)) && IsSafeText(v.Value, max));

    /// <summary>Hatalıysa açıklama, geçerliyse null döner.</summary>
    public static string? Validate(GameProfileFile file)
    {
        if (file.Schema != SupportedSchema) return $"desteklenmeyen şema {file.Schema}";
        foreach (var (name, set) in file.ValueSets)
        {
            if (!IdPattern().IsMatch(name) || set.Any(v => v.Key.Length > 32 || !IsSafeText(v.Value, 40)))
                return $"geçersiz değer kümesi '{name}'";
        }
        foreach (var (format, labels) in file.FormatLabels)
        {
            if (!AllowedFormats.Contains(format)) return $"bilinmeyen biçim etiketi '{format}'";
            if (ValidateLabels(labels, file, format) is { } error) return error;
        }

        var ids = new HashSet<string>();
        foreach (var game in file.Games)
        {
            if (!IdPattern().IsMatch(game.Id) || !ids.Add(game.Id)) return $"geçersiz oyun kimliği '{game.Id}'";
            if (string.IsNullOrWhiteSpace(game.Name) || game.Name.Length > 80) return $"{game.Id}: geçersiz ad";
            if (game.Exe is { } exe && !IsSafeRelativePath(exe, ".exe")) return $"{game.Id}: geçersiz exe yolu";
            if (game.Process is { } process && (process.Length > 80 || process.IndexOfAny(['/', '\\', ':']) >= 0))
                return $"{game.Id}: geçersiz işlem adı";

            if (game.Config is { } config)
            {
                if (!AllowedFormats.Contains(config.Format)) return $"{game.Id}: bilinmeyen biçim '{config.Format}'";
                if (!AllowedBases.Contains(config.Base)) return $"{game.Id}: izin verilmeyen konum '{config.Base}'";
                if (!IsSafeRelativePath(config.Path, AllowedExtensions)) return $"{game.Id}: geçersiz ayar dosyası yolu";
            }
            else
            {
                return $"{game.Id}: ayar dosyası tanımlı değil";
            }
            // Listede yalnızca geliştiricinin profil yazdığı oyunlar bulunur.
            if (game.Profiles.Count == 0) return $"{game.Id}: profil yok";
            if (ValidateLabels(game.Labels, file, game.Id) is { } labelError) return labelError;

            foreach (var profile in game.Profiles)
            {
                if (!IdPattern().IsMatch(profile.Id) || !IsSafeText(profile.Name, 80)) return $"{game.Id}: geçersiz profil";
                if (profile.Description is { } description && !IsSafeText(description, 600)) return $"{game.Id}/{profile.Id}: geçersiz açıklama";
                foreach (var (key, value) in profile.Values)
                {
                    if (!KeyPattern().IsMatch(key)) return $"{game.Id}/{profile.Id}: geçersiz anahtar '{key}'";
                    if (value.Length > 64 || value.IndexOfAny(['\r', '\n', '"', '\0']) >= 0)
                        return $"{game.Id}/{profile.Id}: geçersiz değer '{key}'";
                }
            }
        }
        return null;
    }

    private static bool IsSafeRelativePath(string path, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 200) return false;
        if (System.IO.Path.IsPathRooted(path) || path.Contains(':')) return false;
        var segments = path.Split('/', '\\');
        if (segments.Any(s => s is "" or "." or "..")) return false;
        return extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }
}
