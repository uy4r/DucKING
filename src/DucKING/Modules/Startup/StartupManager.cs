using System.IO;
using DucKING.Core;
using Microsoft.Win32;

namespace DucKING.Modules.Startup;

/// <summary>
/// Görev Yöneticisi'ndeki "Başlangıç uygulamaları" ile aynı mekanizma: girdiyi silmez,
/// yalnızca StartupApproved altında etkin/devre dışı işaretler. Böylece tamamen geri alınabilir.
/// </summary>
public sealed class StartupItem : ObservableObject
{
    private bool _isEnabled;

    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Source { get; init; }
    public required RegistryHive ApprovedHive { get; init; }
    public required string ApprovedPath { get; init; }

    /// <summary>Kapatılırsa güvenlik, ses ya da anti-cheat (Vanguard, FACEIT…) bozulabilecek girdiler.</summary>
    public bool IsImportant => StartupManager.ImportantPatterns.Any(p =>
        Name.Contains(p, StringComparison.OrdinalIgnoreCase) || Command.Contains(p, StringComparison.OrdinalIgnoreCase));

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            try
            {
                StartupManager.SetEnabled(this, value);
                _isEnabled = value;
                Log.Info($"Başlangıç {(value ? "açıldı" : "kapatıldı")}: {Name}");
            }
            catch (Exception ex)
            {
                Log.Notify(Loc.T($"\"{Name}\" değiştirilemedi: {ex.Message}", $"Couldn't change \"{Name}\": {ex.Message}",
                    $"Не удалось изменить «{Name}»: {ex.Message}"), isError: true);
            }
            OnPropertyChanged();
        }
    }

    internal void Load(bool enabled) => _isEnabled = enabled;
}

public static class StartupManager
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    internal static readonly string[] ImportantPatterns =
    [
        "SecurityHealth", "Windows Defender", "Vanguard", "vgtray", "FACEIT", "EasyAntiCheat", "BattlEye",
        "RtkAud", "Realtek", "Synaptics", "ELAN", "igfx",
    ];

    public static IReadOnlyList<StartupItem> GetItems()
    {
        var items = new List<StartupItem>();
        AddRegistry(items, RegistryHive.CurrentUser, Run, Approved + "Run", Loc.T("Kullanıcı", "User", "Пользователь"));
        AddRegistry(items, RegistryHive.LocalMachine, Run, Approved + "Run", Loc.T("Tüm kullanıcılar", "All users", "Все пользователи"));
        AddRegistry(items, RegistryHive.LocalMachine, Run32, Approved + "Run32",
            Loc.T("Tüm kullanıcılar (32 bit)", "All users (32-bit)", "Все пользователи (32-бит)"));
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser,
            Loc.T("Başlangıç klasörü", "Startup folder", "Папка автозагрузки"));
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine,
            Loc.T("Ortak başlangıç klasörü", "Common startup folder", "Общая папка автозагрузки"));
        return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    internal static void SetEnabled(StartupItem item, bool enabled)
    {
        // 12 bayt: ilk bayt 0x02 = etkin, 0x03 = devre dışı; devamı devre dışı bırakılma zamanı (FILETIME).
        var data = new byte[12];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
            BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
        Reg.Write(item.ApprovedHive, item.ApprovedPath, item.Name, data, RegistryValueKind.Binary);
    }

    private static void AddRegistry(List<StartupItem> items, RegistryHive hive, string runPath, string approvedPath, string source)
    {
        using var key = Reg.Base(hive).OpenSubKey(runPath);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            var item = new StartupItem
            {
                Name = name,
                Command = key.GetValue(name)?.ToString() ?? "",
                Source = source,
                ApprovedHive = hive,
                ApprovedPath = approvedPath,
            };
            item.Load(IsApproved(hive, approvedPath, name));
            items.Add(item);
        }
    }

    private static void AddFolder(List<StartupItem> items, string folder, RegistryHive hive, string source)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

            var item = new StartupItem
            {
                Name = fileName,
                Command = file,
                Source = source,
                ApprovedHive = hive,
                ApprovedPath = Approved + "StartupFolder",
            };
            item.Load(IsApproved(hive, Approved + "StartupFolder", fileName));
            items.Add(item);
        }
    }

    private static bool IsApproved(RegistryHive hive, string path, string name) =>
        Reg.Read(hive, path, name) is not byte[] { Length: > 0 } data || data[0] % 2 == 0;
}
