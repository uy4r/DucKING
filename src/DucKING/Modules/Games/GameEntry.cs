using System.Diagnostics;
using System.IO;
using DucKING.Core;
using Microsoft.Win32;

namespace DucKING.Modules.Games;

/// <summary>Ayar tablosunda bir profilin hücresi; Changes = uygulanırsa bu ayar değişir.</summary>
public sealed record SettingCell(string Text, bool Changes);

/// <summary>Ayar tablosunun bir satırı: ayar adı, şu anki değer ve her profildeki değer.</summary>
public sealed record SettingRow(string Name, string Current, IReadOnlyList<SettingCell> Cells);

/// <summary>Oyun kartındaki bir profil düğmesi.</summary>
public sealed class ProfileOption(GameEntry owner, GameProfile profile) : ObservableObject
{
    private bool _isActive;
    private string _preview = "";

    public GameEntry Owner { get; } = owner;
    public GameProfile Profile { get; } = profile;
    public string Name => Profile.Name.Text;

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    /// <summary>Uygulanınca neyin değişeceği (araç ipucu).</summary>
    public string Preview
    {
        get => _preview;
        set => Set(ref _preview, value);
    }
}

/// <summary>Oyunlar sayfasındaki bir oyun kartı.</summary>
public sealed class GameEntry : ObservableObject
{
    private const string GpuPreferencesKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string CompatLayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    private string _status = "";
    private string _statusBrush = "TextDim";
    private string _activeProfileText = "";
    private bool _hasBackup;
    private bool _isBusy;

    public GameEntry(DetectedGame game)
    {
        Game = game;
        Profiles = game.Definition.Profiles.Select(p => new ProfileOption(this, p)).ToList();

        if (game.ExePath is { } exe)
        {
            GpuTweak = new RegistryTweak(RegValue.Hkcu(GpuPreferencesKey, exe, "GpuPreference=2;", kind: RegistryValueKind.String))
            {
                Id = $"game-gpu-{game.Definition.Id}",
                Title = Loc.T("Yüksek performanslı ekran kartı", "High-performance graphics card", "Мощная видеокарта"),
                Description = Loc.T("Dahili ve harici ekran kartı olan sistemlerde oyunun her zaman güçlü kartta çalışmasını sağlar.",
                    "On systems with both integrated and dedicated graphics, always runs the game on the more powerful card.",
                    "На системах со встроенной и дискретной видеокартой игра всегда запускается на более мощной."),
            };
            FsoTweak = new RegistryTweak(RegValue.Hkcu(CompatLayersKey, exe, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", kind: RegistryValueKind.String))
            {
                Id = $"game-fso-{game.Definition.Id}",
                Title = Loc.T("Tam ekran iyileştirmelerini kapat", "Turn off fullscreen optimizations", "Отключить оптимизацию во весь экран"),
                Description = Loc.T("Bazı DirectX 11 oyunlarında giriş gecikmesini azaltır. DirectX 12 oyunlarda etkisi yoktur.",
                    "Reduces input lag in some DirectX 11 games. Has no effect on DirectX 12 games.",
                    "Снижает задержку ввода в некоторых играх на DirectX 11. На игры с DirectX 12 не влияет."),
                Risk = Risk.Moderate,
            };
        }
    }

    public DetectedGame Game { get; }
    public string Name => Game.Definition.Name;
    public string SourceText => Game.Source ?? Loc.T("Ayar dosyası bulundu", "Settings file found", "Найден файл настроек");
    public IReadOnlyList<ProfileOption> Profiles { get; }
    public bool HasProfiles => Profiles.Count > 0;
    public RegistryTweak? GpuTweak { get; }
    public RegistryTweak? FsoTweak { get; }
    public bool HasWindowsSettings => GpuTweak is not null;

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }
    public string ActiveProfileText
    {
        get => _activeProfileText;
        private set
        {
            if (Set(ref _activeProfileText, value)) OnPropertyChanged(nameof(HasActiveProfileText));
        }
    }

    public bool HasActiveProfileText => ActiveProfileText.Length > 0;
    public bool HasBackup { get => _hasBackup; private set => Set(ref _hasBackup, value); }
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

    private bool _isExpanded;
    private IReadOnlyList<SettingRow> _settings = [];

    /// <summary>Kart açıldığında profillerin yapacağı ayarlar tablo olarak gösterilir.</summary>
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    public IReadOnlyList<SettingRow> Settings
    {
        get => _settings;
        private set
        {
            if (Set(ref _settings, value)) OnPropertyChanged(nameof(HasSettings));
        }
    }

    public bool HasSettings => Settings.Count > 0;
    public IReadOnlyList<string> ProfileNames => Profiles.Select(p => p.Name).ToList();

    /// <summary>
    /// Profillerdeki her anahtar için dosyadaki karşılığını bulur (Unreal'da ekli anahtarlar dahil) ve
    /// okunabilir ad/değerlerle satır üretir. Kenar yumuşatma ayarları en üste alınır.
    /// </summary>
    private void BuildSettings(Dictionary<string, string> current)
    {
        var definition = Game.Definition;
        var keys = Profiles.SelectMany(p => p.Profile.Values.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var rows = new List<(bool AntiAliasing, SettingRow Row)>();

        foreach (var profileKey in keys)
        {
            var fileKey = current.Keys.FirstOrDefault(k => string.Equals(k, profileKey, StringComparison.OrdinalIgnoreCase))
                          ?? (Format == "unreal-ini"
                              ? current.Keys.FirstOrDefault(k => k.StartsWith(profileKey + "_", StringComparison.OrdinalIgnoreCase))
                              : null);
            if (fileKey is null) continue; // dosyada yoksa profil de dokunmaz

            var now = current[fileKey];
            var (name, describe) = GameProfiles.DescribeSetting(definition, fileKey);
            var cells = Profiles.Select(p => p.Profile.Values.TryGetValue(profileKey, out var target)
                    ? new SettingCell(describe(target), !string.Equals(target, now, StringComparison.OrdinalIgnoreCase))
                    : new SettingCell(Loc.T("değişmez", "unchanged", "не меняется"), false))
                .ToList();

            var antiAliasing = profileKey.Contains("AntiAliasing", StringComparison.OrdinalIgnoreCase)
                               || profileKey.Contains("msaa", StringComparison.OrdinalIgnoreCase)
                               || profileKey.Contains("cmaa", StringComparison.OrdinalIgnoreCase);
            rows.Add((antiAliasing, new SettingRow(name, describe(now), cells)));
        }

        Settings = rows.OrderByDescending(r => r.AntiAliasing).Select(r => r.Row).ToList();
    }

    public bool IsRunning =>
        Game.Definition.Process is { } name && Process.GetProcessesByName(name).Any(p => { p.Dispose(); return true; });

    private string Format => Game.Definition.Config?.Format ?? "";
    private static string BackupKey(string id, string path) => $"game|{id}|{path}";

    /// <summary>Dosyaları yeniden okur: hangi profil uygulanmış, ne değişecek, yedek var mı.</summary>
    public void Refresh()
    {
        GpuTweak?.Refresh();
        FsoTweak?.Refresh();
        HasBackup = Game.ConfigFiles.Any(f => BackupStore.TryGet(BackupKey(Game.Definition.Id, f), out _));

        if (Game.ConfigFiles.Count == 0)
        {
            SetStatus(Loc.T("Ayar dosyası bulunamadı. Oyunu bir kez açıp ayarlardan çıkın, sonra tekrar deneyin.",
                "Settings file not found. Start the game once, open and close its settings, then try again.",
                "Файл настроек не найден. Запустите игру, откройте и закройте её настройки, затем попробуйте снова."), "Warning");
            ActiveProfileText = "";
            return;
        }

        try
        {
            var current = GameConfigWriter.Read(Format, Game.ConfigFiles[0]);
            BuildSettings(current);
            ProfileOption? active = null;
            foreach (var option in Profiles)
            {
                var diff = GameConfigWriter.Diff(Format, current, option.Profile.Values);
                option.IsActive = diff.Count == 0;
                option.Preview = (option.Profile.Description is { } d ? d.Text + "\n\n" : "") +
                                 (diff.Count == 0 ? Loc.T("Bu profil zaten uygulanmış.", "This profile is already applied.", "Этот профиль уже применён.") :
                                     Loc.T($"{diff.Count} ayar değişecek:", $"{diff.Count} {Loc.Plural(diff.Count, "setting", "settings")} will change:",
                                         $"Изменится настроек: {diff.Count}") + "\n" +
                                     string.Join("\n", diff.Take(14).Select(c => $"{c.Key}: {c.From} → {c.To}")) +
                                     (diff.Count > 14 ? "\n" + Loc.T($"… ve {diff.Count - 14} ayar daha", $"… and {diff.Count - 14} more", $"… и ещё {diff.Count - 14}") : ""));
                if (option.IsActive) active ??= option;
            }
            ActiveProfileText = Loc.T("Şu an: ", "Now: ", "Сейчас: ")
                                + (active?.Name ?? Loc.T("oyunun kendi ayarları", "the game's own settings", "собственные настройки игры"));
            var accounts = Game.ConfigFiles.Count - 1;
            SetStatus(IsRunning
                    ? Loc.T("Oyun açık — profil uygulamak için önce oyunu kapatın.", "The game is running — close it before applying a profile.",
                        "Игра запущена — закройте её, прежде чем применять профиль.")
                    : Loc.T("Ayar dosyası", "Settings file", "Файл настроек") + $": {Path.GetFileName(Game.ConfigFiles[0])}" +
                      (accounts > 0
                          ? Loc.T($" (+{accounts} hesap)", $" (+{accounts} {Loc.Plural(accounts, "account", "accounts")})",
                              $" (+{accounts} {Loc.RuPlural(accounts, "аккаунт", "аккаунта", "аккаунтов")})")
                          : ""),
                IsRunning ? "Danger" : "TextDim");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(Loc.T($"Ayar dosyası okunamadı: {ex.Message}", $"Couldn't read the settings file: {ex.Message}",
                $"Не удалось прочитать файл настроек: {ex.Message}"), "Danger");
        }
    }

    /// <summary>Profili tüm ayar dosyalarına (ör. her Valorant hesabı) uygular; ilk seferde orijinal dosyayı yedekler.</summary>
    public int Apply(GameProfile profile)
    {
        if (IsRunning)
            throw new InvalidOperationException(Loc.T($"{Name} açık. Ayarları yazmadan önce oyunu kapatın; yoksa oyun kapanırken eski ayarları geri yazar.",
                $"{Name} is running. Close the game before writing settings, or it will write its old settings back when it exits.",
                $"Игра {Name} запущена. Закройте её перед записью настроек, иначе при выходе она запишет старые настройки обратно."));

        var changed = 0;
        foreach (var file in Game.ConfigFiles)
        {
            BackupFile(file);
            changed += GameConfigWriter.Apply(Format, file, profile.Values);
        }
        Log.Info($"{Name}: '{profile.Name}' profili uygulandı ({changed} ayar).");
        return changed;
    }

    /// <summary>DucKING'den önceki ayar dosyalarını geri yükler.</summary>
    public void Revert()
    {
        if (IsRunning)
            throw new InvalidOperationException(Loc.T($"{Name} açık. Geri almadan önce oyunu kapatın.", $"{Name} is running. Close the game before reverting.",
                $"Игра {Name} запущена. Закройте её перед откатом."));

        foreach (var file in Game.ConfigFiles)
        {
            var key = BackupKey(Game.Definition.Id, file);
            if (!BackupStore.TryGet(key, out var entry) || entry.Data is not { } backup || !File.Exists(backup)) continue;

            var attributes = File.GetAttributes(file);
            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            File.Copy(backup, file, overwrite: true);
            File.SetAttributes(file, attributes);
            File.Delete(backup);
            BackupStore.Forget(key);
        }
        Log.Info($"{Name}: oyunun kendi ayarları geri yüklendi.");
    }

    private void BackupFile(string file)
    {
        var key = BackupKey(Game.Definition.Id, file);
        BackupStore.Remember(key, () =>
        {
            var dir = Path.Combine(AppPaths.DataDir, "game-backups", Game.Definition.Id);
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Math.Abs(file.GetHashCode()):x}-{Path.GetFileName(file)}");
            File.Copy(file, target, overwrite: true);
            return new BackupStore.Entry { Exists = true, Data = target };
        });
    }

    private void SetStatus(string text, string brush)
    {
        Status = text;
        StatusBrush = brush;
    }
}
