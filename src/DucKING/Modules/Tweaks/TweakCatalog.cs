using DucKING.Core;

namespace DucKING.Modules.Tweaks;

/// <summary>Sayfaların ve ana sayfadaki "tek tık" düğmesinin aynı nesneleri paylaşması için tek örnek.</summary>
public static class TweakCatalog
{
    private static IReadOnlyList<Tweak>? _privacy;
    private static IReadOnlyList<Tweak>? _performance;

    public static IReadOnlyList<Tweak> Privacy => _privacy ??= PrivacyTweaks.Create().Where(t => t.IsAvailable).ToList();
    public static IReadOnlyList<Tweak> Performance => _performance ??= PerformanceTweaks.Create().Where(t => t.IsAvailable).ToList();

    public static IEnumerable<Tweak> All => Privacy.Concat(Performance);

    public static Task RefreshAllAsync() => Task.Run(() =>
    {
        foreach (var tweak in All) tweak.Refresh();
    });

    /// <summary>Önerilen ve henüz uygulanmamış ayarları sırayla uygular.</summary>
    public static async Task<int> ApplyRecommendedAsync(IEnumerable<Tweak> tweaks)
    {
        var count = 0;
        foreach (var tweak in tweaks.Where(t => t.Recommended && !t.IsApplied))
        {
            await tweak.ApplyAsync();
            if (tweak.IsApplied) count++;
        }
        return count;
    }

    public static async Task<int> RevertAllAsync(IEnumerable<Tweak> tweaks)
    {
        var count = 0;
        foreach (var tweak in tweaks.Where(t => t.IsApplied))
        {
            await tweak.RevertAsync();
            if (!tweak.IsApplied) count++;
        }
        return count;
    }
}
