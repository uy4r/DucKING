using System.IO;
using System.Text.Json;

namespace DucKING.Core;

public enum WidgetSize
{
    Small,  // 1×1
    Wide,   // 2×1
    Large,  // 2×2
}

public sealed class WidgetPlacement
{
    public string Id { get; set; } = "";
    public WidgetSize Size { get; set; }
}

/// <summary>Kullanıcı tercihleri; %ProgramData%\DucKING\settings.json içinde saklanır.</summary>
public sealed class AppSettings : ObservableObject
{
    private static string FilePath => Path.Combine(AppPaths.DataDir, "settings.json");

    private bool _restartDiscord = true;

    public static AppSettings Current { get; } = Load();

    /// <summary>DNS/DPI işlemlerinde açık Discord'u kapatıp işlem bitince yeniden başlat.</summary>
    public bool RestartDiscord
    {
        get => _restartDiscord;
        set
        {
            if (Set(ref _restartDiscord, value)) Save();
        }
    }

    /// <summary>Ana sayfa widget düzeni (sıra + boyut). Null ise varsayılan düzen kullanılır.</summary>
    public List<WidgetPlacement>? HomeLayout { get; set; }

    public void SaveLayout(List<WidgetPlacement>? layout)
    {
        HomeLayout = layout;
        Save();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Error("Ayarlar okunamadı, varsayılanlar kullanılıyor", ex);
        }
        return new AppSettings();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Error("Ayarlar kaydedilemedi", ex);
        }
    }
}
