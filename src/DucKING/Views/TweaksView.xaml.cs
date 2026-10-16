using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DucKING.Core;
using DucKING.Modules.Tweaks;

namespace DucKING.Views;

public partial class TweaksView : UserControl, IRefreshable
{
    private readonly IReadOnlyList<Tweak> _tweaks;

    public TweaksView(string title, string subtitle, IReadOnlyList<Tweak> tweaks)
    {
        InitializeComponent();
        _tweaks = tweaks;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        List.ItemsSource = tweaks;

        foreach (var tweak in tweaks)
            tweak.PropertyChanged += OnTweakChanged;
    }

    public async Task RefreshAsync()
    {
        await Task.Run(() =>
        {
            foreach (var tweak in _tweaks) tweak.Refresh();
        });
        UpdateCount();
    }

    private void OnTweakChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Tweak.IsApplied))
            Dispatcher.BeginInvoke(UpdateCount);
    }

    private void UpdateCount() =>
        CountText.Text = Loc.T($"{_tweaks.Count(t => t.IsApplied)} / {_tweaks.Count} ayar uygulandı",
            $"{_tweaks.Count(t => t.IsApplied)} / {_tweaks.Count} tweaks applied",
            $"Применено настроек: {_tweaks.Count(t => t.IsApplied)} из {_tweaks.Count}");

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        SetButtons(false);
        var count = await TweakCatalog.ApplyRecommendedAsync(_tweaks);
        SetButtons(true);
        Log.Notify(count == 0
            ? Loc.T("Önerilen ayarların hepsi zaten uygulanmış.", "All recommended tweaks are already applied.", "Все рекомендуемые настройки уже применены.")
            : Loc.T($"{count} ayar uygulandı.", $"{count} {Loc.Plural(count, "tweak", "tweaks")} applied.", $"Применено настроек: {count}."));
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(Loc.T("Bu sayfadaki tüm ayarlar DucKING'den önceki haline döndürülsün mü?",
                "Restore every setting on this page to how it was before DucKING?",
                "Вернуть все настройки на этой странице к состоянию до DucKING?"),
            "DucKING", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        SetButtons(false);
        var count = await TweakCatalog.RevertAllAsync(_tweaks);
        SetButtons(true);
        Log.Notify(Loc.T($"{count} ayar geri alındı.", $"{count} {Loc.Plural(count, "tweak", "tweaks")} reverted.", $"Отменено настроек: {count}."));
    }

    private void SetButtons(bool enabled)
    {
        ApplyButton.IsEnabled = enabled;
        RevertButton.IsEnabled = enabled;
    }
}
