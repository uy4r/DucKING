using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DucKING.Core;
using DucKING.Modules.Discord;
using DucKING.Modules.Dns;
using DucKING.Modules.Dpi;
using DucKING.Modules.Games;
using DucKING.Modules.Startup;
using DucKING.Modules.Tweaks;

namespace DucKING.Views;

/// <summary>
/// Bento ana sayfa: her kutu bir özelliğin özetini gösterir ve tıklanınca o sayfayı açar.
/// Kutuların sırası ve boyutu düzenlenebilir (bkz. HomeView.Layout.cs).
/// </summary>
public partial class HomeView : UserControl, IRefreshable
{
    private const int PingHistory = 40;

    private readonly MainWindow _window;
    private readonly List<double?> _pings = [];
    private IDisposable? _telemetry;
    private DnsProvider? _fastest;
    private DnsProvider? _currentDns;
    private bool _busy;

    public HomeView(MainWindow window)
    {
        InitializeComponent();
        _window = window;

        foreach (var bar in new[] { PerfBar, PrivacyBar, CpuBar, RamBar })
        {
            bar.RenderTransformOrigin = new Point(0, 0.5);
            bar.RenderTransform = new ScaleTransform(0, 1);
        }

        InitializeLayout();
    }

    // Ölçüm yalnızca ana sayfa ekrandayken çalışır.
    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _telemetry ??= Telemetry.Shared.Subscribe(sample => Dispatcher.BeginInvoke(() => Show(sample)));

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _telemetry?.Dispose();
        _telemetry = null;
    }

    public async Task RefreshAsync()
    {
        await TweakCatalog.RefreshAllAsync();
        ShowCount(TweakCatalog.Performance, PerfOn, PerfTotal, PerfBar);
        ShowCount(TweakCatalog.Privacy, PrivacyOn, PrivacyTotal, PrivacyBar);

        var dnsBlocked = DnsService.IsDiscordBlockedByDnsAsync();
        var (adapters, dpi, startup, games) = await Task.Run(() =>
            (DnsService.GetActiveAdapters(), DpiService.GetStatus(), StartupManager.GetItems(), GameLibrary.Detect(GameProfiles.Current.Games)));

        // DPI
        DpiName.Text = dpi.Tool?.Name ?? Loc.T("Kurulu değil", "Not installed", "Не установлен");
        var healthy = dpi.Running && dpi.HelperRunning != false;
        SetText(DpiDetail,
            dpi.Tool is null ? Loc.T("Discord'a erişmek için kur", "Install to reach Discord", "Установите для доступа к Discord")
            : healthy ? Loc.T("● Çalışıyor", "● Running", "● Работает")
            : Loc.T("● Çalışmıyor — yeniden kur", "● Not running — reinstall", "● Не работает — переустановите"),
            dpi.Tool is null ? "TextDim" : healthy ? "Success" : "Danger");

        // Başlangıç
        var enabled = startup.Count(i => i.IsEnabled);
        StartupCount.Text = enabled.ToString();
        StartupDetail.Text = Loc.T($"{startup.Count} programdan {enabled} tanesi Windows ile başlıyor",
            $"{enabled} of {startup.Count} {Loc.Plural(startup.Count, "program starts", "programs start")} with Windows",
            $"Запускаются вместе с Windows: {enabled} из {startup.Count}");

        // Oyunlar
        GamesCount.Text = games.Count.ToString();
        GamesDetail.Text = games.Count == 0
            ? Loc.T("Desteklenen oyun bulunamadı", "No supported games found", "Поддерживаемые игры не найдены")
            : Loc.T("oyun için hazır grafik profili var",
                Loc.Plural(games.Count, "game with a ready graphics profile", "games with ready graphics profiles"),
                $"{Loc.RuPlural(games.Count, "игра", "игры", "игр")} с готовым профилем графики");

        // DNS
        DnsName.Text = DnsService.DescribeCurrent(adapters);
        await EnsureDnsMeasuredAsync();
        ShowDns(adapters, await dnsBlocked);
    }

    private void ShowCount(IReadOnlyList<Tweak> tweaks, System.Windows.Documents.Run on, System.Windows.Documents.Run total, Border bar)
    {
        var applied = tweaks.Count(t => t.IsApplied);
        on.Text = applied.ToString();
        total.Text = $" / {tweaks.Count}";
        SetBar(bar, tweaks.Count == 0 ? 0 : (double)applied / tweaks.Count);
    }

    private static void SetBar(Border bar, double ratio) =>
        ((ScaleTransform)bar.RenderTransform).ScaleX = Math.Clamp(ratio, 0, 1);

    private void SetText(TextBlock target, string text, string brush)
    {
        target.Text = text;
        target.Foreground = (Brush)FindResource(brush);
    }

    // ---- DNS ----

    /// <summary>DNS sağlayıcılarının hızını uygulama açıldıktan sonra bir kez ölçer (DNS sayfasıyla paylaşılır).</summary>
    private async Task EnsureDnsMeasuredAsync()
    {
        if (DnsProvider.All.Any(p => p.LatencyMs.HasValue || p.TestFailed)) return;

        DnsTestState.Text = Loc.T("hız ölçülüyor…", "measuring speed…", "измерение скорости…");
        await Task.WhenAll(DnsProvider.All.Select(async p =>
        {
            var results = await Task.WhenAll(p.Ipv4.Select(ip => DnsService.MeasureLatencyAsync(ip, attempts: 2)));
            var best = results.Where(r => r.HasValue).Select(r => r!.Value).DefaultIfEmpty(double.NaN).Min();
            if (double.IsNaN(best)) p.TestFailed = true;
            else p.LatencyMs = best;
        }));
        DnsTestState.Text = "";
    }

    private void ShowDns(IReadOnlyList<NetAdapter> adapters, bool blocked)
    {
        var current = DnsService.DetectActiveProvider(adapters);
        _fastest = DnsProvider.All.Where(p => p.LatencyMs.HasValue).MinBy(p => p.LatencyMs);
        foreach (var p in DnsProvider.All) p.IsFastest = p == _fastest;

        var servers = string.Join(" · ", adapters.SelectMany(a => a.DnsServers).Distinct().OrderBy(s => s.Contains(':')).Take(2));
        if (blocked)
            SetText(DnsDetail, Loc.T("● DNS engeli var — Discord bu DNS ile açılmaz", "● DNS block — Discord won't open with this DNS",
                "● Блокировка DNS — с этим DNS Discord не откроется"), "Danger");
        else
        {
            DnsDetail.Text = current?.LatencyMs is { } ms ? $"{ms:0} {Loc.Ms} · {servers}" : servers;
            DnsDetail.Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0x8A, 0x8D));
        }

        if (_fastest is null)
        {
            DnsSuggestion.Text = Loc.T("DNS sunucularına ulaşılamadı.", "Couldn't reach the DNS servers.", "Не удалось связаться с DNS-серверами.");
            ApplyFastestButton.Visibility = Visibility.Collapsed;
        }
        else if (_fastest == current)
        {
            DnsSuggestion.Text = Loc.T("Bağlantın için en hızlı DNS'i kullanıyorsun.", "You're already using the fastest DNS for your connection.",
                "Вы уже используете самый быстрый DNS для своего подключения.");
            ApplyFastestButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            DnsSuggestion.Text = Loc.T("En hızlı", "Fastest", "Быстрейший") + $": {_fastest.Name} · {_fastest.LatencyMs:0} {Loc.Ms}";
            ApplyFastestButton.Visibility = Visibility.Visible;
        }

        BuildDnsBars(current);
    }

    private void DnsBars_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged) BuildDnsBars(_currentDns);
    }

    /// <summary>Her sağlayıcı için gecikme çubuğu: kısa çubuk = hızlı; en hızlısı kırmızı. Çubuklar kutunun yüksekliğini kullanır.</summary>
    private void BuildDnsBars(DnsProvider? current)
    {
        _currentDns = current;
        DnsBars.Children.Clear();
        DnsBars.ColumnDefinitions.Clear();
        var room = DnsBars.ActualHeight - 44; // etiketler için ~44 px
        if (room < 30) return; // küçük boyutta çubuklar gizlenir

        var max = DnsProvider.All.Where(p => p.LatencyMs.HasValue).Select(p => p.LatencyMs!.Value).DefaultIfEmpty(1).Max();
        for (var i = 0; i < DnsProvider.All.Count; i++)
        {
            var p = DnsProvider.All[i];
            DnsBars.ColumnDefinitions.Add(new ColumnDefinition());

            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            column.Children.Add(new TextBlock
            {
                Text = p.LatencyMs is { } v ? $"{v:0} {Loc.Ms}" : Loc.T("yok", "n/a", "нет"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)FindResource(p == _fastest ? "Text" : "TextDim"),
            });
            column.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(6, 4, 6, 6),
                Height = p.LatencyMs is { } ms ? Math.Max(10, 18 + (room - 18) * ms / max) : 10,
                Background = (Brush)FindResource(p == _fastest ? "Accent" : p == current ? "AccentLine" : "AccentDim"),
            });
            column.Children.Add(new TextBlock
            {
                Text = p == current ? $"{p.Name} •" : p.Name,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource(p == current ? "Text" : "TextDim"),
            });
            Grid.SetColumn(column, i);
            DnsBars.Children.Add(column);
        }
    }

    private async void ApplyFastest_Click(object sender, RoutedEventArgs e)
    {
        if (_fastest is not { } provider || _busy) return;
        _busy = true;
        ApplyFastestButton.IsEnabled = false;
        try
        {
            await DiscordGuard.RunAsync(() => Task.Run(() => DnsService.Apply(provider, enableDoh: true)));
            var encrypted = DnsService.SupportsDoh ? Loc.T(" (şifreli)", " (encrypted)", " (с шифрованием)") : "";
            Log.Notify(Loc.T($"DNS {provider.Name}{encrypted} olarak ayarlandı.", $"DNS set to {provider.Name}{encrypted}.",
                $"DNS изменён на {provider.Name}{encrypted}."));
        }
        catch (Exception ex)
        {
            Log.Error("DNS ayarlanamadı", ex);
            Log.Notify(Loc.T($"DNS ayarlanamadı: {ex.Message}", $"Couldn't set DNS: {ex.Message}", $"Не удалось настроить DNS: {ex.Message}"), isError: true);
        }
        finally
        {
            ApplyFastestButton.IsEnabled = true;
            _busy = false;
            await RefreshAsync();
        }
    }

    // ---- Canlı ölçümler ----

    private void Show(TelemetrySample sample)
    {
        Sparkline.Push(_pings, sample.PingMs, PingHistory);
        PingValue.Text = sample.PingMs is { } ms ? $"{ms:0}" : "—";
        PingValue.Foreground = (Brush)FindResource(sample.PingMs is null ? "Danger" : "Text");
        var ok = _pings.Where(p => p.HasValue).Select(p => p!.Value).ToList();
        var loss = 100.0 * _pings.Count(p => p is null) / _pings.Count;
        PingStats.Text = ok.Count == 0
            ? Loc.T("yanıt yok", "no response", "нет ответа")
            : Loc.T($"ort {ok.Average():0} ms · en yüksek {ok.Max():0} ms · kayıp %{loss:0}",
                $"avg {ok.Average():0} ms · max {ok.Max():0} ms · loss {loss:0}%",
                $"ср. {ok.Average():0} мс · макс. {ok.Max():0} мс · потери {loss:0}%");
        DrawPing();

        CpuValue.Text = Loc.Percent(sample.CpuPercent);
        SetBar(CpuBar, sample.CpuPercent / 100);
        RamValue.Text = $"{sample.RamUsedGb:0.0} / {sample.RamTotalGb:0} {Loc.Gb}";
        SetBar(RamBar, sample.RamTotalGb > 0 ? sample.RamUsedGb / sample.RamTotalGb : 0);

        var gpu = sample.Gpu;
        GpuTemp.Text = gpu?.TemperatureC is { } t ? $"{t:0}" : "—";
        GpuDetail.Text = gpu is null
            ? Loc.T("Ekran kartı bulunamadı", "No graphics card found", "Видеокарта не найдена")
            : Loc.T("Kullanım", "Usage", "Загрузка") + " " + Loc.Percent(gpu.UsagePercent ?? 0)
              + (gpu.VramUsedGb is { } v ? $" · VRAM {v:0.0}/{gpu.VramTotalGb:0} {Loc.Gb}" : "");
    }

    private void PingCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawPing();

    private void DrawPing()
    {
        var max = Math.Max(40, _pings.Where(p => p.HasValue).Select(p => p!.Value).DefaultIfEmpty(0).Max() * 1.3);
        PingPath.Data = Sparkline.Build(_pings, PingHistory, PingCanvas.ActualWidth, PingCanvas.ActualHeight, max);
    }

    // ---- Etkileşim ----

    private void Tile_Click(object sender, MouseButtonEventArgs e)
    {
        if (_editing) return;
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<AppPage>(tag, out var page))
            _window.Navigate(page);
    }

    private async void OneClick_Click(object sender, MouseButtonEventArgs e)
    {
        if (_busy || _editing) return;
        var pending = TweakCatalog.All.Count(t => t.Recommended && !t.IsApplied);
        if (pending == 0)
        {
            Log.Notify(Loc.T("Önerilen ayarların hepsi zaten uygulanmış.", "All recommended tweaks are already applied.",
                "Все рекомендуемые настройки уже применены."));
            return;
        }

        var answer = MessageBox.Show(
            Loc.T($"Önerilen {pending} ayar uygulanacak. Her biri sonradan tek tek geri alınabilir.\n\n" +
                  "Önce sistem geri yükleme noktası oluşturulsun mu? (Önerilir, 1-2 dakika sürer.)",
                $"{pending} recommended {Loc.Plural(pending, "tweak", "tweaks")} will be applied. Each can be reverted on its own later.\n\n" +
                "Create a system restore point first? (Recommended, takes 1-2 minutes.)",
                $"Будут применены рекомендуемые настройки: {pending}. Каждую из них можно потом отменить по отдельности.\n\n" +
                "Сначала создать точку восстановления системы? (Рекомендуется, занимает 1–2 минуты.)"),
            UiText.Get("home.oneClick"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return;

        _busy = true;
        try
        {
            if (answer == MessageBoxResult.Yes)
            {
                OneClickDetail.Text = Loc.T("Geri yükleme noktası oluşturuluyor…", "Creating a restore point…", "Создание точки восстановления…");
                try
                {
                    var name = Loc.T("DucKING öncesi", "Before DucKING", "До DucKING");
                    await Task.Run(() => SystemRestore.Create($"{name} {DateTime.Now:dd.MM.yyyy HH:mm}"));
                }
                catch (Exception ex)
                {
                    Log.Error("Geri yükleme noktası oluşturulamadı", ex);
                    var go = MessageBox.Show(Loc.T($"Geri yükleme noktası oluşturulamadı:\n{ex.Message}\n\nYine de devam edilsin mi?",
                            $"Couldn't create a restore point:\n{ex.Message}\n\nContinue anyway?",
                            $"Не удалось создать точку восстановления:\n{ex.Message}\n\nВсё равно продолжить?"),
                        "DucKING", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (go != MessageBoxResult.Yes) return;
                }
            }

            OneClickDetail.Text = Loc.T("Ayarlar uygulanıyor…", "Applying tweaks…", "Применение настроек…");
            var count = await TweakCatalog.ApplyRecommendedAsync(TweakCatalog.All);
            Log.Notify(Loc.T($"{count} ayar uygulandı. Bazı ayarlar için bilgisayarı yeniden başlatın.",
                $"{count} {Loc.Plural(count, "tweak", "tweaks")} applied. Restart your PC for some of them to take effect.",
                $"Применено настроек: {count}. Для некоторых из них нужно перезагрузить компьютер."));
        }
        finally
        {
            OneClickDetail.Text = UiText.Get("home.oneClickDetail");
            _busy = false;
            await RefreshAsync();
        }
    }
}
