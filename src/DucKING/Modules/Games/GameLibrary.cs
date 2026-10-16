using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DucKING.Core;
using Microsoft.Win32;

namespace DucKING.Modules.Games;

/// <summary>Bulunan bir oyun: kurulum klasörü, exe ve ayar dosyaları (hangisi bulunabildiyse).</summary>
public sealed record DetectedGame(GameDefinition Definition, string? Source, string? InstallDir, string? ExePath, IReadOnlyList<string> ConfigFiles);

/// <summary>Steam, Epic ve Riot kütüphanelerini tarar; ayar dosyası bulunan oyunları da ekler.</summary>
public static partial class GameLibrary
{
    public static IReadOnlyList<DetectedGame> Detect(IEnumerable<GameDefinition> definitions)
    {
        var steam = SteamInstallDirs();
        var epic = EpicInstallDirs();
        var riot = RiotInstallDirs();
        var result = new List<DetectedGame>();

        // Yalnızca profili olan (geliştiricinin ayar yazdığı) oyunlar listelenir.
        foreach (var game in definitions.Where(d => d.Profiles.Count > 0 && d.Config is not null))
        {
            string? dir = null, source = null;
            if (game.SteamAppId is { } appId && steam.TryGetValue(appId, out var steamDir)) (dir, source) = (steamDir, "Steam");
            else if (game.EpicAppName is { } epicName && epic.TryGetValue(epicName, out var epicDir)) (dir, source) = (epicDir, "Epic");
            else if (game.RiotProduct is { } product && riot.FirstOrDefault(r => r.Contains(product, StringComparison.OrdinalIgnoreCase)) is { } riotDir)
                (dir, source) = (riotDir, "Riot");

            var exe = dir is not null && game.Exe is not null ? Path.Combine(dir, game.Exe) : null;
            if (exe is not null && !File.Exists(exe)) exe = null;
            var configs = game.Config is null ? [] : FindConfigFiles(game.Config);

            if (dir is not null || configs.Count > 0)
                result.Add(new DetectedGame(game, source, dir, exe, configs));
        }
        return result;
    }

    /// <summary>"*" içeren yolu tek seviyelik klasör eşlemesiyle çözer; yalnızca izin verilen kök altında arar.</summary>
    public static IReadOnlyList<string> FindConfigFiles(GameConfigLocation location)
    {
        var root = location.Base switch
        {
            "steam-userdata" => SteamPath() is { } steam ? Path.Combine(steam, "userdata") : null,
            "localappdata" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            _ => null,
        };
        if (root is null || !Directory.Exists(root)) return [];

        IEnumerable<string> current = [root];
        var segments = location.Path.Split('/', '\\');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var last = i == segments.Length - 1;
            current = current.SelectMany(dir =>
            {
                if (segment == "*") return SafeDirectories(dir);
                var next = Path.Combine(dir, segment);
                return last ? (File.Exists(next) ? [next] : []) : (Directory.Exists(next) ? [next] : Array.Empty<string>());
            }).ToList();
        }
        return current.Where(File.Exists).ToList();
    }

    private static IEnumerable<string> SafeDirectories(string dir)
    {
        try { return Directory.GetDirectories(dir); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return []; }
    }

    private static string? SteamPath() =>
        (Reg.Read(RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath") as string)?.Replace('/', '\\');

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex VdfPath();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"")]
    private static partial Regex VdfInstallDir();

    private static Dictionary<int, string> SteamInstallDirs()
    {
        var result = new Dictionary<int, string>();
        if (SteamPath() is not { } steam) return result;
        try
        {
            var libraries = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                libraries.AddRange(VdfPath().Matches(File.ReadAllText(vdf)).Select(m => m.Groups[1].Value.Replace(@"\\", @"\")));

            foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var apps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(apps)) continue;
                foreach (var manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                {
                    var name = Path.GetFileNameWithoutExtension(manifest)["appmanifest_".Length..];
                    if (!int.TryParse(name, out var appId) || result.ContainsKey(appId)) continue;
                    var match = VdfInstallDir().Match(File.ReadAllText(manifest));
                    if (match.Success) result[appId] = Path.Combine(apps, "common", match.Groups[1].Value);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Steam kütüphanesi okunamadı", ex);
        }
        return result;
    }

    private static Dictionary<string, string> EpicInstallDirs()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");
        if (!Directory.Exists(manifests)) return result;

        foreach (var item in Directory.EnumerateFiles(manifests, "*.item"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(item));
                var root = doc.RootElement;
                if (root.TryGetProperty("AppName", out var app) && root.TryGetProperty("InstallLocation", out var location))
                    result[app.GetString() ?? ""] = location.GetString() ?? "";
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                Log.Error($"Epic kaydı okunamadı: {item}", ex);
            }
        }
        return result;
    }

    private static List<string> RiotInstallDirs()
    {
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Riot Games\RiotClientInstalls.json");
        try
        {
            if (!File.Exists(file)) return [];
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.TryGetProperty("associated_client", out var clients)) return [];
            return clients.EnumerateObject().Select(p => p.Name.Replace('/', '\\')).ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Log.Error("Riot kurulumları okunamadı", ex);
            return [];
        }
    }
}
