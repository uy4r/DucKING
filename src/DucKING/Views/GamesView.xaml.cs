using System.Windows;
using System.Windows.Controls;
using DucKING.Core;
using DucKING.Modules.Games;

namespace DucKING.Views;

public partial class GamesView : UserControl, IRefreshable
{
    private bool _updateChecked;

    public GamesView()
    {
        InitializeComponent();
        UpdateButton.Visibility = string.IsNullOrWhiteSpace(GameProfiles.UpdateUrl) ? Visibility.Collapsed : Visibility.Visible;
    }

    public async Task RefreshAsync()
    {
        // GitHub'daki profil dosyası uygulama açıldıktan sonra bir kez kontrol edilir.
        if (!_updateChecked)
        {
            _updateChecked = true;
            await GameProfiles.TryUpdateAsync();
        }
        ProfileSource.Text = Loc.T($"Profiller: {GameProfiles.Source} · sürüm {GameProfiles.Current.Version}",
                                 $"Profiles: {GameProfiles.Source} · version {GameProfiles.Current.Version}",
                                 $"Профили: {GameProfiles.Source} · версия {GameProfiles.Current.Version}") +
                             (string.IsNullOrWhiteSpace(GameProfiles.UpdateUrl)
                                 ? Loc.T(" · çevrim içi güncelleme kapalı", " · online updates off", " · онлайн-обновление выключено")
                                 : "");

        var entries = await Task.Run(() =>
        {
            var list = GameLibrary.Detect(GameProfiles.Current.Games)
                .Select(g => new GameEntry(g))
                .OrderByDescending(e => e.HasProfiles)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            foreach (var entry in list) entry.Refresh();
            return list;
        });

        GameList.ItemsSource = entries;
        LoadingText.Visibility = Visibility.Collapsed;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Karta tıklanınca ayar tablosu açılır/kapanır. İçindeki düğme ve anahtarlar tıklamayı kendileri karşılar.</summary>
    private void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is GameEntry entry) entry.IsExpanded = !entry.IsExpanded;
    }

    private async void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ProfileOption option) return;
        var entry = option.Owner;
        await RunAsync(entry, () =>
        {
            var changed = entry.Apply(option.Profile);
            return changed == 0
                ? Loc.T($"{entry.Name}: '{option.Name}' zaten uygulanmış.", $"{entry.Name}: \"{option.Name}\" is already applied.",
                    $"{entry.Name}: профиль «{option.Name}» уже применён.")
                : Loc.T($"{entry.Name}: '{option.Name}' uygulandı ({changed} ayar). Oyunu açınca geçerli olur.",
                    $"{entry.Name}: \"{option.Name}\" applied ({changed} {Loc.Plural(changed, "setting", "settings")}). Takes effect when you start the game.",
                    $"{entry.Name}: профиль «{option.Name}» применён (настроек: {changed}). Вступит в силу при запуске игры.");
        });
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not GameEntry entry) return;
        await RunAsync(entry, () =>
        {
            entry.Revert();
            return Loc.T($"{entry.Name}: oyunun kendi ayarlarına dönüldü.", $"{entry.Name}: restored the game's own settings.",
                $"{entry.Name}: восстановлены собственные настройки игры.");
        });
    }

    private static async Task RunAsync(GameEntry entry, Func<string> action)
    {
        entry.IsBusy = true;
        try
        {
            Log.Notify(await Task.Run(action));
        }
        catch (Exception ex)
        {
            Log.Error($"{entry.Name} profili uygulanamadı", ex);
            Log.Notify(ex.Message, isError: true);
        }
        finally
        {
            await Task.Run(entry.Refresh);
            entry.IsBusy = false;
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        var updated = await GameProfiles.TryUpdateAsync();
        Log.Notify(updated
            ? Loc.T("Oyun profilleri güncellendi.", "Game profiles updated.", "Игровые профили обновлены.")
            : Loc.T("Profiller zaten güncel.", "Profiles are already up to date.", "Профили уже актуальны."));
        UpdateButton.IsEnabled = true;
        await RefreshAsync();
    }
}
