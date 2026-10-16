using System.Windows;
using System.Windows.Controls;
using DucKING.Core;
using DucKING.Modules.Apps;

namespace DucKING.Views;

public partial class AppsView : UserControl, IRefreshable
{
    private IReadOnlyList<BloatApp> _apps = [];
    private bool _loaded;

    public AppsView() => InitializeComponent();

    public async Task RefreshAsync()
    {
        // Uygulama listesini almak birkaç saniye sürer; sayfaya her girişte değil, yalnızca ilk girişte ve kaldırma sonrası yenilenir.
        if (_loaded) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        RemoveButton.IsEnabled = false;
        StatusText.Text = Loc.T("Yüklü uygulamalar taranıyor…", "Scanning installed apps…", "Поиск установленных приложений…");
        _apps = await Task.Run(BloatwareService.GetInstalled);
        List.ItemsSource = _apps;
        _loaded = true;

        StatusText.Text = _apps.Count == 0
            ? Loc.T("Listedeki gereksiz uygulamaların hiçbiri yüklü değil. 👍", "None of the listed bloatware is installed. 👍",
                "Ни одно из ненужных приложений из списка не установлено. 👍")
            : Loc.T($"{_apps.Count} gereksiz uygulama bulundu.", $"Found {_apps.Count} unneeded {Loc.Plural(_apps.Count, "app", "apps")}.",
                $"Найдено ненужных приложений: {_apps.Count}.");
        RemoveButton.IsEnabled = _apps.Count > 0;
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        var selected = _apps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0) return;

        var answer = MessageBox.Show(
            Loc.T($"{selected.Count} uygulama kaldırılacak:", $"{selected.Count} {Loc.Plural(selected.Count, "app", "apps")} will be removed:",
                $"Будут удалены приложения ({selected.Count}):")
            + $"\n\n{string.Join("\n", selected.Select(a => "• " + a.Name))}\n\n"
            + Loc.T("Devam edilsin mi?", "Continue?", "Продолжить?"),
            "DucKING", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        IsEnabled = false;
        var removed = 0;
        foreach (var app in selected)
        {
            StatusText.Text = Loc.T("Kaldırılıyor", "Removing", "Удаление") + $": {app.Name}…";
            try
            {
                await Task.Run(() => BloatwareService.Remove(app));
                removed++;
            }
            catch (Exception ex)
            {
                Log.Error($"{app.Package} kaldırılamadı", ex);
            }
        }
        IsEnabled = true;

        Log.Notify(removed == selected.Count
            ? Loc.T($"{removed} uygulama kaldırıldı.", $"{removed} {Loc.Plural(removed, "app", "apps")} removed.", $"Удалено приложений: {removed}.")
            : Loc.T($"{removed}/{selected.Count} uygulama kaldırıldı. Ayrıntılar günlük dosyasında.",
                $"{removed}/{selected.Count} apps removed. See the log file for details.",
                $"Удалено приложений: {removed} из {selected.Count}. Подробности — в файле журнала."), removed != selected.Count);
        await LoadAsync();
    }
}
