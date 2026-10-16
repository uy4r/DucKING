using System.Globalization;
using System.Windows.Markup;
using Microsoft.Win32;

namespace DucKING.Core;

public enum AppLanguage
{
    Turkish,
    English,
    Russian,
}

/// <summary>
/// Arayüz dili. Kurulum programı seçilen dili HKLM\SOFTWARE\DucKING\Language değerine yazar (tr / en / ru);
/// program içinden değiştirilince de aynı yere yazılır. Değer yoksa Windows'un görüntü dili, o da desteklenmiyorsa
/// İngilizce kullanılır. Dil açılışta bir kez okunur; değiştirmek programı yeniden başlatır.
/// Kodda metinler <see cref="T"/> ile üç dilde yan yana yazılır; XAML'deki metinler <see cref="UiText"/> tablosundadır.
/// Günlük dosyasına yazılan iletiler (Log.Info / Log.Error) çevrilmez.
/// </summary>
public static class Loc
{
    private const string KeyPath = @"SOFTWARE\DucKING";
    private const string ValueName = "Language";

    public static IReadOnlyList<AppLanguage> All { get; } = [AppLanguage.Turkish, AppLanguage.English, AppLanguage.Russian];

    public static AppLanguage Current { get; } = Load();

    public static string T(string tr, string en, string ru) => Current switch
    {
        AppLanguage.English => en,
        AppLanguage.Russian => ru,
        _ => tr,
    };

    // Birimler ve sayılar: Rusçada "мс", "ГБ"; yüzde işareti Türkçede sayıdan önce gelir.
    public static string Ms => T("ms", "ms", "мс");
    public static string Gb => T("GB", "GB", "ГБ");
    public static string Percent(double value) => T($"%{value:0}", $"{value:0}%", $"{value:0}%");

    /// <summary>İngilizce tekil/çoğul seçimi.</summary>
    public static string Plural(long n, string one, string many) => n == 1 ? one : many;

    /// <summary>Rusça sayıya göre ad biçimi: 1 игра, 2 игры, 5 игр (11-14 her zaman "many").</summary>
    public static string RuPlural(long n, string one, string few, string many)
    {
        var last2 = Math.Abs(n) % 100;
        var last = last2 % 10;
        if (last2 is >= 11 and <= 14) return many;
        return last switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
    }

    public static string Code(AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Russian => "ru",
        _ => "tr",
    };

    /// <summary>Dilin kendi dilindeki adı (seçicide her zaman böyle gösterilir).</summary>
    public static string NativeName(AppLanguage language) => language switch
    {
        AppLanguage.English => "English",
        AppLanguage.Russian => "Русский",
        _ => "Türkçe",
    };

    public static AppLanguage? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "tr" => AppLanguage.Turkish,
        "en" => AppLanguage.English,
        "ru" => AppLanguage.Russian,
        _ => null,
    };

    /// <summary>Seçimi kaydeder; yeni dil bir sonraki açılışta geçerli olur.</summary>
    public static void Save(AppLanguage language)
    {
        using var key = Registry.LocalMachine.CreateSubKey(KeyPath);
        key.SetValue(ValueName, Code(language), RegistryValueKind.String);
        Log.Info($"Arayüz dili kaydedildi: {Code(language)}");
    }

    private static AppLanguage Load()
    {
        // Çevirileri denemek için yalnızca bu oturumda geçerli dil: DucKING.exe --lang en
        var args = Environment.GetCommandLineArgs();
        var index = Array.FindIndex(args, a => a.Equals("--lang", StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length && Parse(args[index + 1]) is { } requested) return requested;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            if (Parse(key?.GetValue(ValueName) as string) is { } saved) return saved;
        }
        catch (Exception ex)
        {
            Log.Error("Dil ayarı okunamadı", ex);
        }
        return Parse(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? AppLanguage.English;
    }
}

/// <summary>XAML'de çevrilmiş metin: <c>Text="{core:Loc home.title}"</c>. Anahtarlar <see cref="UiText"/>'te.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(Key);
}
