using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DucKING.Modules.Games;

/// <summary>
/// Oyun ayar dosyalarını okur/yazar. Yalnızca dosyada ZATEN bulunan anahtarlar değiştirilir;
/// oyunun tanımadığı bir şey eklenmez, dosyanın geri kalanı (yorumlar, sıralama) aynen korunur.
/// </summary>
public static partial class GameConfigWriter
{
    [GeneratedRegex("^(\\s*\")([^\"]+)(\"\\s+\")([^\"]*)(\".*)$")]
    private static partial Regex Cs2Line();

    [GeneratedRegex(@"^(\s*)([^=;\[\s][^=]*?)(\s*=\s*)(.*?)(\s*)$")]
    private static partial Regex KeyValueLine();

    /// <summary>Dosyadaki (anahtar → değer) eşlemesini döner.</summary>
    public static Dictionary<string, string> Read(string format, string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var match = Parse(format, line);
            if (match is not null) values.TryAdd(match.Value.Key, match.Value.Value);
        }
        return values;
    }

    /// <summary>Profilin dosyadaki karşılığı: hangi anahtar ne olacak (yalnızca dosyada var olanlar).</summary>
    public static List<(string Key, string From, string To)> Diff(string format, IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> profile)
    {
        var changes = new List<(string, string, string)>();
        foreach (var (key, from) in current)
        {
            if (FindProfileValue(format, key, profile) is { } to && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                changes.Add((key, from, to));
        }
        return changes;
    }

    /// <summary>Profili dosyaya yazar; değişen anahtar sayısını döner.</summary>
    public static int Apply(string format, string path, IReadOnlyDictionary<string, string> profile)
    {
        var lines = File.ReadAllLines(path);
        var changed = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Parse(format, lines[i]) is not { } entry) continue;
            if (FindProfileValue(format, entry.Key, profile) is not { } value) continue;
            if (string.Equals(entry.Value, value, StringComparison.OrdinalIgnoreCase)) continue;

            lines[i] = entry.Prefix + entry.Key + entry.Separator + value + entry.Suffix;
            changed++;
        }

        if (changed == 0) return 0;
        var attributes = File.GetAttributes(path);
        var readOnly = attributes.HasFlag(FileAttributes.ReadOnly);
        if (readOnly) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        try
        {
            File.WriteAllLines(path, lines, DetectEncoding(path));
        }
        finally
        {
            if (readOnly) File.SetAttributes(path, attributes); // kullanıcının kilidi korunur
        }
        return changed;
    }

    /// <summary>
    /// Profil anahtarı dosyadakiyle birebir ya da Unreal'da ek ile eşleşir:
    /// "sg.ShadowQuality" → "sg.ShadowQuality_BEND_STUDIO" (Days Gone).
    /// </summary>
    private static string? FindProfileValue(string format, string fileKey, IReadOnlyDictionary<string, string> profile)
    {
        if (profile.TryGetValue(fileKey, out var exact)) return exact;
        if (format != "unreal-ini") return null;
        foreach (var (key, value) in profile)
        {
            if (fileKey.StartsWith(key + "_", StringComparison.OrdinalIgnoreCase)) return value;
        }
        return null;
    }

    private readonly record struct Entry(string Prefix, string Key, string Separator, string Value, string Suffix);

    private static Entry? Parse(string format, string line)
    {
        var match = format == "cs2-video" ? Cs2Line().Match(line) : KeyValueLine().Match(line);
        if (!match.Success) return null;
        var g = match.Groups;
        return new Entry(g[1].Value, g[2].Value.Trim(), g[3].Value, g[4].Value, g[5].Value);
    }

    private static Encoding DetectEncoding(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        reader.Peek();
        // BOM'suz UTF-8 olarak bırak; oyunların çoğu BOM'u sevmez.
        return reader.CurrentEncoding is UTF8Encoding ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: HasBom(path)) : reader.CurrentEncoding;
    }

    private static bool HasBom(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using var stream = File.OpenRead(path);
        return stream.Read(head) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
    }
}
