using System.Windows;
using System.Windows.Controls;
using DucKING.Core;
using DucKING.Modules.Discord;
using DucKING.Modules.Dns;

namespace DucKING.Views;

public partial class DnsView : UserControl, IRefreshable
{
    private DnsProvider? _fastest;

    public DnsView()
    {
        InitializeComponent();
        Providers.ItemsSource = DnsProvider.All;

        if (!DnsService.SupportsDoh)
        {
            DohCheck.IsChecked = false;
            DohCheck.IsEnabled = false;
            DohCheck.Content = Loc.T("Şifreli DNS (DoH) — Windows 11 gerekir", "Encrypted DNS (DoH) — requires Windows 11",
                "Зашифрованный DNS (DoH) — нужна Windows 11");
        }
    }

    public async Task RefreshAsync()
    {
        var adapters = await Task.Run(DnsService.GetActiveAdapters);
        var active = DnsService.DetectActiveProvider(adapters);

        foreach (var p in DnsProvider.All) p.IsActive = p == active;

        if (adapters.Count == 0)
        {
            CurrentProvider.Text = Loc.T("Bağlantı yok", "No connection", "Нет подключения");
            AdapterText.Text = Loc.T("İnternete bağlı Ethernet veya Wi-Fi adaptörü bulunamadı.",
                "No Ethernet or Wi-Fi adapter connected to the internet was found.",
                "Не найден Ethernet- или Wi-Fi-адаптер с подключением к интернету.");
            return;
        }

        var servers = adapters.SelectMany(a => a.DnsServers).Distinct().ToList();
        CurrentProvider.Text = DnsService.DescribeCurrent(adapters);
        AdapterText.Text = $"{string.Join(", ", adapters.Select(a => a.Name))}  ·  {string.Join(", ", servers.Take(4))}";
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        BestBanner.Visibility = Visibility.Collapsed;

        foreach (var p in DnsProvider.All)
        {
            p.IsFastest = false;
            p.TestFailed = false;
            p.LatencyMs = null;
            p.IsTesting = true;
        }

        // Her sağlayıcının iki sunucusu da ölçülür; en iyi sonuç sağlayıcının sonucu sayılır.
        await Task.WhenAll(DnsProvider.All.Select(async p =>
        {
            var results = await Task.WhenAll(p.Ipv4.Select(ip => DnsService.MeasureLatencyAsync(ip)));
            var best = results.Where(r => r.HasValue).Select(r => r!.Value).DefaultIfEmpty(double.NaN).Min();
            p.IsTesting = false;
            if (double.IsNaN(best)) p.TestFailed = true;
            else p.LatencyMs = best;
        }));

        _fastest = DnsProvider.All.Where(p => p.LatencyMs.HasValue).MinBy(p => p.LatencyMs);
        if (_fastest is not null)
        {
            _fastest.IsFastest = true;
            BestText.Text = Loc.T("Bağlantınız için en hızlı DNS", "Fastest DNS for your connection", "Самый быстрый DNS для вашего подключения")
                            + $": {_fastest.Name} ({_fastest.LatencyMs:0} {Loc.Ms})";
            BestBanner.Visibility = Visibility.Visible;
        }
        else
        {
            Log.Notify(Loc.T("Hiçbir DNS sunucusu yanıt vermedi. İnternet bağlantınızı kontrol edin.",
                "No DNS server responded. Check your internet connection.",
                "Ни один DNS-сервер не ответил. Проверьте подключение к интернету."), isError: true);
        }

        TestButton.IsEnabled = true;
    }

    private async void ApplyBest_Click(object sender, RoutedEventArgs e)
    {
        if (_fastest is not null) await ApplyAsync(_fastest);
    }

    private async void ApplyProvider_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DnsProvider provider)
            await ApplyAsync(provider);
    }

    private async Task ApplyAsync(DnsProvider provider)
    {
        var doh = DohCheck.IsChecked == true;
        var encrypted = doh && DnsService.SupportsDoh ? Loc.T(" (şifreli)", " (encrypted)", " (с шифрованием)") : "";
        await RunBusy(() => DnsService.Apply(provider, doh),
            Loc.T($"DNS {provider.Name}{encrypted} olarak ayarlandı.", $"DNS set to {provider.Name}{encrypted}.",
                $"DNS изменён на {provider.Name}{encrypted}."));
    }

    private async void Restore_Click(object sender, RoutedEventArgs e) =>
        await RunBusy(DnsService.Restore, Loc.T("DNS ayarları önceki haline döndürüldü.", "DNS settings restored.",
            "Настройки DNS восстановлены."));

    private async Task RunBusy(Action action, string successMessage)
    {
        IsEnabled = false;
        try
        {
            // Discord açıksa kapatılır, işlem bitince yeniden açılır (ayar açıksa).
            await DiscordGuard.RunAsync(() => Task.Run(action));
            Log.Notify(successMessage);
        }
        catch (Exception ex)
        {
            Log.Error("DNS işlemi başarısız", ex);
            Log.Notify(Loc.T($"DNS ayarlanamadı: {ex.Message}", $"Couldn't set DNS: {ex.Message}", $"Не удалось настроить DNS: {ex.Message}"), isError: true);
        }
        finally
        {
            IsEnabled = true;
            await RefreshAsync();
        }
    }
}
