using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DucKING.Core;
using DucKING.Modules.Dns;
using DucKING.Modules.Dpi;
using DucKING.Modules.Games;
using DucKING.Modules.Tweaks;
using DucKING.Views;

namespace DucKING;

public enum AppPage
{
    Home,
    Games,
    Hardware,
    Dns,
    Dpi,
    Performance,
    Privacy,
    Startup,
    Apps,
    About,
}

/// <summary>
/// Üst çubukta hap biçimli sekmeler: Ana sayfa · Oyunlar · Donanım · Ağ ▾ (DNS, DPI) · Sistem ▾
/// (Performans, Gizlilik, Başlangıç, Uygulamalar).
/// </summary>
public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private readonly Dictionary<AppPage, Func<UserControl>> _factories;
    private readonly Dictionary<AppPage, UserControl> _pages = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    public MainWindow()
    {
        InitializeComponent();

        _factories = new()
        {
            [AppPage.Home] = () => new HomeView(this),
            [AppPage.Games] = () => new GamesView(),
            [AppPage.Hardware] = () => new HardwareView(),
            [AppPage.Dns] = () => new DnsView(),
            [AppPage.Dpi] = () => new DpiView(),
            [AppPage.Performance] = () => new TweaksView(UiText.Get("nav.performance"),
                Loc.T("Oyunlarda FPS'i, giriş gecikmesini ve ping'i etkileyen Windows ayarları. Her ayar ayrı ayrı geri alınabilir.",
                    "Windows settings that affect FPS, input latency and ping in games. Each one can be reverted on its own.",
                    "Настройки Windows, влияющие на FPS, задержку ввода и пинг в играх. Каждую можно отменить по отдельности."),
                TweakCatalog.Performance),
            [AppPage.Privacy] = () => new TweaksView(UiText.Get("nav.privacy"),
                Loc.T("Windows'un reklamlarını, internet aramalarını ve arka planda veri gönderen özelliklerini kapatır.",
                    "Turns off Windows ads, web search and features that send data in the background.",
                    "Отключает рекламу Windows, поиск в интернете и функции, отправляющие данные в фоне."),
                TweakCatalog.Privacy),
            [AppPage.Startup] = () => new StartupView(),
            [AppPage.Apps] = () => new AppsView(),
            [AppPage.About] = () => new AboutView(),
        };

        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}";
        if (!AppPaths.IsElevated)
        {
            ElevationText.Text = Loc.T("Kısıtlı (yönetici değil)", "Limited (not administrator)", "Ограничено (не администратор)");
            ElevationDot.Foreground = (System.Windows.Media.Brush)FindResource("Warning");
        }
        Log.Status += (message, isError) => Dispatcher.BeginInvoke(() => ShowToast(message, isError));
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast.Visibility = Visibility.Collapsed;
        };

        SourceInitialized += (_, _) =>
        {
            var on = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmUseImmersiveDarkMode, ref on, sizeof(int));
        };
        Loaded += (_, _) => Navigate(AppPage.Home);
        InitializeBadges();
        InitializeLanguageMenu();
    }

    // ---- Sekme rozetleri: oyun sayısı, ekran kartı sıcaklığı, ağ durumu, uygulanan ayarlar ----

    private readonly DispatcherTimer _tempTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly Lazy<GpuSensor?> _gpu = new(GpuSensor.TryCreate);
    private bool _badgesBusy;

    private void InitializeBadges()
    {
        // Ayar sayısı, ayarlar açılıp kapatıldıkça anında güncellenir.
        foreach (var tweak in TweakCatalog.All)
            tweak.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Tweak.IsApplied)) Dispatcher.BeginInvoke(UpdateSystemBadge);
            };

        _tempTimer.Tick += async (_, _) => await UpdateTemperatureAsync();
        _statusTimer.Tick += async (_, _) => await RefreshBadgesAsync();
        _tempTimer.Start();
        _statusTimer.Start();
        _ = UpdateTemperatureAsync();
    }

    private async Task UpdateTemperatureAsync()
    {
        var temperature = await Task.Run(() => _gpu.Value?.ReadTemperature());
        GpuTempBadge.Text = temperature is { } t ? $"{t:0}°C" : "";
        GpuTempBadge.Foreground = (System.Windows.Media.Brush)FindResource(temperature switch
        {
            >= 85 => "Danger",
            >= 70 => "Warning",
            _ => "TextDim",
        });
    }

    private void UpdateSystemBadge()
    {
        var tweaks = TweakCatalog.All.ToList();
        var applied = tweaks.Count(t => t.IsApplied);
        SystemBadgeText.Text = $"{applied}/{tweaks.Count}";
        SystemBadge.Visibility = applied > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Oyun sayısı ve ağ durumu (DPI + DNS engeli); sayfa değişince ve dakikada bir.</summary>
    private async Task RefreshBadgesAsync()
    {
        if (_badgesBusy) return;
        _badgesBusy = true;
        try
        {
            var dnsBlocked = DnsService.IsDiscordBlockedByDnsAsync();
            var (games, dpi) = await Task.Run(() => (GameLibrary.Detect(GameProfiles.Current.Games).Count, DpiService.GetStatus()));

            GamesBadgeText.Text = games.ToString();
            GamesBadge.Visibility = games > 0 ? Visibility.Visible : Visibility.Collapsed;

            var blocked = await dnsBlocked;
            var dpiHealthy = dpi.Tool is not null && dpi.Running && dpi.HelperRunning != false;
            NetDot.Foreground = (System.Windows.Media.Brush)FindResource(
                blocked || (dpi.Tool is not null && !dpiHealthy) ? "Danger" : dpiHealthy ? "Success" : "TextFaint");
            NetPill.ToolTip = blocked ? Loc.T("DNS engeli var — Discord açılmaz", "DNS block detected — Discord won't open", "Блокировка DNS — Discord не откроется")
                : dpi.Tool is null ? Loc.T("DPI aracı kurulu değil", "No DPI tool installed", "Инструмент обхода DPI не установлен")
                : dpiHealthy ? Loc.T($"{dpi.Tool.Name} çalışıyor", $"{dpi.Tool.Name} is running", $"{dpi.Tool.Name} работает")
                : Loc.T($"{dpi.Tool.Name} çalışmıyor", $"{dpi.Tool.Name} is not running", $"{dpi.Tool.Name} не работает");

            UpdateSystemBadge();
        }
        catch (Exception ex)
        {
            Log.Error("Sekme rozetleri güncellenemedi", ex);
        }
        finally
        {
            _badgesBusy = false;
        }
    }

    /// <summary>Önce sayfa kendi verisini yeniler (ayar durumları dahil), sonra rozetler.</summary>
    private async Task RefreshPageAndBadgesAsync(UserControl page)
    {
        if (page is IRefreshable refreshable) await refreshable.RefreshAsync();
        await RefreshBadgesAsync();
    }

    public AppPage Current { get; private set; }

    public void Navigate(AppPage target)
    {
        NetMenu.IsOpen = SystemMenu.IsOpen = LanguageMenu.IsOpen = false;
        if (!_pages.TryGetValue(target, out var page))
        {
            page = _factories[target]();
            _pages[target] = page;
        }
        PageHost.Content = page;
        Current = target;
        UpdateNavState();

        _ = RefreshPageAndBadgesAsync(page);
    }

    private void UpdateNavState()
    {
        SetActive(HomePill, Current == AppPage.Home);
        SetActive(GamesPill, Current == AppPage.Games);
        SetActive(HardwarePill, Current == AppPage.Hardware);
        SetActive(NetPill, Current is AppPage.Dns or AppPage.Dpi);
        SetActive(SystemPill, Current is AppPage.Performance or AppPage.Privacy or AppPage.Startup or AppPage.Apps);

        SetActive(DnsRow, Current == AppPage.Dns);
        SetActive(DpiRow, Current == AppPage.Dpi);
        SetActive(PerformanceRow, Current == AppPage.Performance);
        SetActive(PrivacyRow, Current == AppPage.Privacy);
        SetActive(StartupRow, Current == AppPage.Startup);
        SetActive(AppsRow, Current == AppPage.Apps);
    }

    private static void SetActive(Button button, bool active) => button.Tag = active ? "active" : null;

    private void Home_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Home);
    private void Games_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Games);
    private void Hardware_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Hardware);
    private void About_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.About);
    private void Dns_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Dns);
    private void Dpi_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Dpi);
    private void Performance_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Performance);
    private void Privacy_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Privacy);
    private void Startup_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Startup);
    private void Apps_Click(object sender, RoutedEventArgs e) => Navigate(AppPage.Apps);

    // StaysOpen=False menü, sekmeye basılınca (dışarı tıklama sayılır) kapanır; aynı tıklama
    // onu hemen yeniden açmasın diye kapanma anı tutulur.
    private DateTime _netClosedAt, _systemClosedAt, _languageClosedAt;

    private static bool JustClosed(DateTime closedAt) => DateTime.UtcNow - closedAt < TimeSpan.FromMilliseconds(250);

    private void NetMenu_Closed(object? sender, EventArgs e) => _netClosedAt = DateTime.UtcNow;
    private void SystemMenu_Closed(object? sender, EventArgs e) => _systemClosedAt = DateTime.UtcNow;
    private void LanguageMenu_Closed(object? sender, EventArgs e) => _languageClosedAt = DateTime.UtcNow;

    private async void Net_Click(object sender, RoutedEventArgs e)
    {
        SystemMenu.IsOpen = false;
        if (JustClosed(_netClosedAt)) return;
        NetMenu.IsOpen = true;

        // Menü açılınca anlık durum: kullanılan DNS ve DPI aracı.
        var (dns, dpi) = await Task.Run(() => (DnsService.DescribeCurrent(DnsService.GetActiveAdapters()), DpiService.GetStatus()));
        DnsMenuInfo.Text = dns.Length > 18 ? dns[..18] + "…" : dns;
        DpiMenuInfo.Text = dpi.Tool is null ? Loc.T("kurulu değil", "not installed", "не установлен") : $"{dpi.Tool.Name} ●";
        DpiMenuInfo.Foreground = (System.Windows.Media.Brush)FindResource(
            dpi.Tool is null ? "TextDim" : dpi.Running && dpi.HelperRunning != false ? "Success" : "Danger");
    }

    private void System_Click(object sender, RoutedEventArgs e)
    {
        NetMenu.IsOpen = false;
        if (JustClosed(_systemClosedAt)) return;
        SystemMenu.IsOpen = true;
    }

    // ---- Dil ----

    private void InitializeLanguageMenu()
    {
        LanguageCode.Text = Loc.Code(Loc.Current).ToUpperInvariant();
        foreach (var language in Loc.All)
        {
            var row = new Button
            {
                Style = (Style)FindResource("MenuRow"),
                Content = Loc.NativeName(language),
                Tag = language == Loc.Current ? "active" : null,
            };
            row.Click += (_, _) => ChangeLanguage(language);
            LanguageList.Children.Add(row);
        }
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        NetMenu.IsOpen = SystemMenu.IsOpen = false;
        if (JustClosed(_languageClosedAt)) return;
        LanguageMenu.IsOpen = true;
    }

    /// <summary>Dil açılışta bir kez okunduğu için seçim kaydedilir ve DucKING yeniden başlatılır.</summary>
    private void ChangeLanguage(AppLanguage language)
    {
        LanguageMenu.IsOpen = false;
        if (language == Loc.Current) return;

        // Soru, seçilen dilde sorulur: kullanıcı o dili okuyabildiği için seçmiştir.
        var question = language switch
        {
            AppLanguage.English => "DucKING will restart in English. Continue?",
            AppLanguage.Russian => "DucKING будет перезапущен на русском языке. Продолжить?",
            _ => "DucKING Türkçe olarak yeniden başlatılacak. Devam edilsin mi?",
        };
        if (MessageBox.Show(this, question, "DucKING", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            Loc.Save(language);
        }
        catch (Exception ex)
        {
            Log.Error("Dil kaydedilemedi", ex);
            Log.Notify(Loc.T($"Dil kaydedilemedi: {ex.Message}", $"Couldn't save the language: {ex.Message}",
                $"Не удалось сохранить язык: {ex.Message}"), isError: true);
            return;
        }
        ((App)Application.Current).Restart();
    }

    private void ShowToast(string message, bool isError)
    {
        ToastText.Text = message;
        ToastIcon.Text = isError ? "" : "";
        ToastIcon.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "Danger" : "Success");
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }
}
