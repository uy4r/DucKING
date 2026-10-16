using System.IO;
using System.Text.Json;

namespace DucKING.Core;

/// <summary>
/// Değiştirilen her ayarın İLK (orijinal) değerini saklar. Aynı ayar tekrar uygulanırsa
/// yedek ezilmez; geri alındığında orijinal değer yerine konup kayıt silinir.
/// </summary>
public static class BackupStore
{
    public sealed class Entry
    {
        public bool Exists { get; set; }
        public string? Kind { get; set; }
        public string? Data { get; set; }
    }

    private static readonly object Sync = new();
    private static Dictionary<string, Entry>? _entries;

    private static Dictionary<string, Entry> Entries
    {
        get
        {
            if (_entries is not null) return _entries;
            try
            {
                _entries = File.Exists(AppPaths.BackupFile)
                    ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(AppPaths.BackupFile)) ?? new()
                    : new();
            }
            catch (Exception ex)
            {
                Log.Error("Yedek dosyası okunamadı, yeni dosya ile devam ediliyor", ex);
                _entries = new();
            }
            return _entries;
        }
    }

    /// <summary>Anahtar için yedek yoksa <paramref name="capture"/> ile alınan değeri saklar.</summary>
    /// <returns>Yeni bir yedek eklendiyse true.</returns>
    public static bool Remember(string key, Func<Entry> capture)
    {
        lock (Sync)
        {
            if (Entries.ContainsKey(key)) return false;
            Entries[key] = capture();
            Save();
            return true;
        }
    }

    /// <summary>
    /// Değeri yedekleyip <paramref name="write"/>'ı çalıştırır. Yazma başarısız olursa bu çağrının
    /// eklediği yedek silinir; böylece hiç değişmemiş bir ayar için sahipsiz yedek kalmaz.
    /// </summary>
    public static void RememberAndWrite(string key, Func<Entry> capture, Action write)
    {
        var added = Remember(key, capture);
        try
        {
            write();
        }
        catch
        {
            if (added) Forget(key);
            throw;
        }
    }

    public static bool TryGet(string key, out Entry entry)
    {
        lock (Sync)
        {
            return Entries.TryGetValue(key, out entry!);
        }
    }

    public static void Forget(string key)
    {
        lock (Sync)
        {
            if (Entries.Remove(key)) Save();
        }
    }

    public static IReadOnlyCollection<string> Keys
    {
        get { lock (Sync) return Entries.Keys.ToList(); }
    }

    private static void Save()
    {
        var tmp = AppPaths.BackupFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, AppPaths.BackupFile, overwrite: true);
    }
}
