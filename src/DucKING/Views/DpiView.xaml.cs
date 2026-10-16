using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DucKING.Core;
using DucKING.Modules.Discord;
using DucKING.Modules.Dns;
using DucKING.Modules.Dpi;

namespace DucKING.Views;

public partial class DpiView : UserControl, IRefreshable
{
    private readonly Dictionary<DpiTool, RadioButton> _toolButtons = new();
    private DpiTool _selected = DpiService.Zapret;
    private bool _presetsChecked;

    public DpiView()
    {
        InitializeComponent();

        foreach (var tool in DpiService.Tools)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = tool.Name, Style = (Style)FindResource("SectionTitle") });
            content.Children.Add(new TextBlock { Text = tool.Description, Style = (Style)FindResource("Dim"), Margin = new Thickness(0, 6, 0, 0) });

            var button = new RadioButton
            {
                Style = (Style)FindResource("ChoiceCard"),
                Content = content,
                GroupName = "DpiTool",
                Margin = new Thickness(0, 0, 10, 0),
                Tag = tool,
            };
            button.Checked += (_, _) => SelectTool(tool);
            _toolButtons[tool] = button;
            ToolGrid.Children.Add(button);
        }

        _toolButtons[_selected].IsChecked = true;

        FindButton.Content = Loc.T("En iyi ayarı bul", "Find the best preset", "Подобрать лучший пресет");
        FindButton.ToolTip = Loc.T("Ayarları sırayla dener ve Discord, YouTube, Roblox'u en iyi açanı kurar (1-2 dakika sürebilir).",
            "Tries the presets one by one and installs the one that best opens Discord, YouTube and Roblox (may take 1-2 minutes).",
            "Перебирает пресеты и устанавливает тот, что лучше всего открывает Discord, YouTube и Roblox (может занять 1–2 минуты).");
    }

    public async Task RefreshAsync()
    {
        // GitHub'da daha yeni presetler varsa oturumda bir kez alınır, liste yenilenir.
        if (!_presetsChecked)
        {
            _presetsChecked = true;
            if (await DpiPresets.TryUpdateAsync()) SelectTool(_selected);
        }

        var dnsBlocked = DnsService.IsDiscordBlockedByDnsAsync();
        var status = await Task.Run(DpiService.GetStatus);
        DnsWarning.Visibility = await dnsBlocked ? Visibility.Visible : Visibility.Collapsed;

        if (status.Tool is null)
        {
            StatusTitle.Text = Loc.T("Kurulu değil", "Not installed", "Не установлен");
            StatusDetail.Text = Loc.T("Henüz bir DPI aracı kurulmadı.", "No DPI tool has been installed yet.", "Инструмент обхода DPI ещё не установлен.");
            StatusDot.Fill = (System.Windows.Media.Brush)FindResource("TextFaint");
            RemoveButton.Visibility = Visibility.Collapsed;
            return;
        }

        var healthy = status.Running && status.HelperRunning != false;
        var name = status.Tool.Name;
        StatusTitle.Text = status.HelperRunning switch
        {
            null when status.Running => Loc.T($"{name} çalışıyor", $"{name} is running", $"{name} работает"),
            null => Loc.T($"{name} kurulu ama çalışmıyor", $"{name} is installed but not running", $"{name} установлен, но не работает"),
            true when status.Running => Loc.T($"{name} + ProxiFyre çalışıyor", $"{name} + ProxiFyre are running", $"{name} + ProxiFyre работают"),
            _ => $"{name} {State(status.Running)}, ProxiFyre {State(status.HelperRunning == true)} — "
                 + Loc.T("yeniden kurun", "reinstall", "переустановите"),
        };
        StatusDetail.Text = status.Arguments ?? "";
        StatusDot.Fill = (System.Windows.Media.Brush)FindResource(healthy ? "Success" : "Danger");
        RemoveButton.Visibility = Visibility.Visible;

        // Kurulu araç ve preseti seçili göster.
        _toolButtons[status.Tool].IsChecked = true;
        if (DpiService.FindPreset(status.Tool, status.Arguments) is { } preset) PresetBox.SelectedItem = preset;
    }

    private static string State(bool running) =>
        running ? Loc.T("çalışıyor", "running", "работает") : Loc.T("çalışmıyor", "not running", "не работает");

    private void SelectTool(DpiTool tool)
    {
        _selected = tool;

        // Bölgeli presetler gruplanır; arayüz Rusçaysa Rusya grubu üstte ve ilk preset seçili olur.
        var preferred = Loc.Current == AppLanguage.Russian ? DpiRegion.Russia : DpiRegion.Turkey;
        var view = new ListCollectionView(tool.Presets.OrderBy(p => p.Region == preferred ? 0 : 1).ToList());
        if (tool.Presets.Any(p => p.Region != DpiRegion.None))
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DpiPreset.RegionName)));
        PresetBox.ItemsSource = view;
        PresetBox.SelectedIndex = 0;
        PresetHint.Text = tool == DpiService.Zapret
            ? Loc.T("İnternet sağlayıcınızı seçin. Çalışmazsa \"Alternatif\" ayarı deneyin.",
                "Choose your ISP. If it doesn't work, try its \"alternative\" preset.",
                "Выберите своего провайдера. Если не помогает, попробуйте его «альтернативный» пресет.")
            : Loc.T("Önce \"Standart\" ile deneyin; çalışmazsa sıradaki alternatife geçin.",
                "Try \"Standard\" first; if it doesn't work, move on to the next alternative.",
                "Сначала попробуйте «Стандартный»; если не помогает, переходите к следующему альтернативному.");

        ListPanel.Visibility = tool.OptionTitle is null ? Visibility.Collapsed : Visibility.Visible;
        EditListButton.Visibility = tool.ListFile is null ? Visibility.Collapsed : Visibility.Visible;
        ListCheck.Content = tool.OptionTitle;
    }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        var preset = PresetBox.SelectedItem as DpiPreset;
        PresetArgs.Text = preset?.Arguments ?? "";
        RussiaNote.Visibility = preset?.Region == DpiRegion.Russia ? Visibility.Visible : Visibility.Collapsed;
        SourceLink.Visibility = preset?.Source is null ? Visibility.Collapsed : Visibility.Visible;
        if (preset?.Source is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Kısa gösterim (github.com/bol-van/zapret); tam adres ipucunda.
            SourceLink.Content = Loc.T("Kaynak: ", "Source: ", "Источник: ") + (uri.Host + string.Concat(uri.Segments.Take(3))).TrimEnd('/');
            SourceLink.ToolTip = url;
        }
    }

    private void SourceLink_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is DpiPreset { Source: { } url })
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedItem is not DpiPreset preset) return;

        var tool = _selected;
        var option = ListCheck.IsChecked == true;
        var fixDns = DnsFixCheck.IsChecked == true;
        await RunBusy(async () =>
        {
            await Task.Run(() => DpiService.Install(tool, preset, option));
            var message = tool == DpiService.ByeDpi
                ? Loc.T($"ByeDPI ve ProxiFyre çalışıyor ({preset.Name}).", $"ByeDPI and ProxiFyre are running ({preset.Name}).",
                    $"ByeDPI и ProxiFyre работают ({preset.Name}).")
                : Loc.T($"{tool.Name} kuruldu ve çalışıyor ({preset.Name}).", $"{tool.Name} is installed and running ({preset.Name}).",
                    $"{tool.Name} установлен и работает ({preset.Name}).");

            if (fixDns && await DnsService.IsDiscordBlockedByDnsAsync())
                message += Loc.T(" DNS engeli bulundu: ", " DNS block found: ", " Обнаружена блокировка DNS: ") + await SwitchToSecureDnsAsync();
            return message + ReopenDiscord;
        });
    }

    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        var tool = _selected;
        var option = ListCheck.IsChecked == true;
        var fixDns = DnsFixCheck.IsChecked == true;
        // Listedeki sırayla (arayüz diline göre önce o bölgenin presetleri) denenir.
        var presets = PresetBox.Items.Cast<DpiPreset>().ToList();
        await RunBusy(async () =>
        {
            // DNS sahte adres veriyorsa hiçbir preset işe yaramaz; denemelerden önce düzeltilir.
            var dnsNote = "";
            if (await DnsService.IsDiscordBlockedByDnsAsync())
            {
                if (!fixDns)
                    return Loc.T("DNS'iniz Discord'u engelliyor; ayar aramadan önce güvenli DNS'e geçin.",
                        "Your DNS is blocking Discord; switch to a secure DNS before searching for a preset.",
                        "Ваш DNS блокирует Discord; перед подбором пресета перейдите на защищённый DNS.");
                dnsNote = Loc.T("DNS engeli bulundu: ", "DNS block found: ", "Обнаружена блокировка DNS: ") + await SwitchToSecureDnsAsync() + " ";
            }

            var progress = new Progress<string>(text => BusyText.Text = text);
            var result = await PresetFinder.RunAsync(tool, presets, option, progress);
            return dnsNote + DescribeResult(tool, result);
        });
    }

    private static string DescribeResult(DpiTool tool, FinderResult r)
    {
        var failed = string.Join(", ", r.Failed);
        return r.Outcome switch
        {
            FinderOutcome.NotBlocked => Loc.T("Engel bulunamadı: Discord, YouTube ve Roblox araç olmadan da açılıyor. Önceki ayarınız korundu.",
                "No blocking found: Discord, YouTube and Roblox open without a tool. Your previous setup was kept.",
                "Блокировок не найдено: Discord, YouTube и Roblox открываются без инструмента. Прежняя настройка сохранена."),
            FinderOutcome.Installed when r.Failed.Count == 0 =>
                Loc.T($"\"{r.Preset!.Name}\" tüm siteleri açtı ve kuruldu.", $"\"{r.Preset!.Name}\" opened every site and was installed.",
                    $"«{r.Preset!.Name}» открыл все сайты и установлен.") + VoiceNote + ReopenDiscord,
            FinderOutcome.Installed =>
                Loc.T($"\"{r.Preset!.Name}\" en iyi sonucu verdi ({r.Passed}/{r.Total} site) ve kuruldu. Açılmayan: {failed}.",
                    $"\"{r.Preset!.Name}\" gave the best result ({r.Passed}/{r.Total} sites) and was installed. Not opening: {failed}.",
                    $"«{r.Preset!.Name}» показал лучший результат ({r.Passed}/{r.Total} сайтов) и установлен. Не открываются: {failed}.") + VoiceNote + ReopenDiscord,
            _ => Loc.T($"{tool.Name} ile engeli aşan bir ayar bulunamadı (açılmayan: {failed}). Başka bir araç deneyin; önceki ayarınız geri yüklendi.",
                $"No {tool.Name} preset got past the block (not opening: {failed}). Try another tool; your previous setup was restored.",
                $"Пресет {tool.Name}, обходящий блокировку, не найден (не открываются: {failed}). Попробуйте другой инструмент; прежняя настройка восстановлена."),
        };
    }

    private static string VoiceNote => Loc.T(" Discord sesli sohbet bu testte denenmez.", " Discord voice chat isn't covered by this test.",
        " Голосовой чат Discord в этой проверке не проверяется.");

    private async void FixDns_Click(object sender, RoutedEventArgs e) =>
        await RunBusy(async () => await SwitchToSecureDnsAsync() + ReopenDiscord);

    private static string ReopenDiscord => Loc.T(" Discord'u tamamen kapatıp yeniden açın.", " Fully close Discord and open it again.",
        " Полностью закройте Discord и откройте его снова.");

    /// <summary>Bu bağlantıda en hızlı DoH destekli sağlayıcıya geçer; kullanıcıya gösterilecek metni döner.</summary>
    private static async Task<string> SwitchToSecureDnsAsync()
    {
        var provider = await DnsService.PickFastestSecureAsync();
        await Task.Run(() => DnsService.Apply(provider, enableDoh: true));
        var encrypted = DnsService.SupportsDoh ? Loc.T(" (şifreli)", " (encrypted)", " (с шифрованием)") : "";
        return Loc.T($"DNS {provider.Name}{encrypted} olarak ayarlandı.", $"DNS set to {provider.Name}{encrypted}.",
            $"DNS изменён на {provider.Name}{encrypted}.");
    }

    private async void Remove_Click(object sender, RoutedEventArgs e) =>
        await RunBusy(async () =>
        {
            await Task.Run(DpiService.RemoveAll);
            return Loc.T("DPI aracı kaldırıldı.", "DPI tool removed.", "Инструмент обхода DPI удалён.");
        });

    private void EditList_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DpiService.GetListFilePath(_selected);
            if (path is null) return;
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            var install = UiText.Get("dpi.install");
            Log.Notify(Loc.T($"Listeyi kaydettikten sonra \"{install}\" ile aracı yeniden başlatın.",
                $"After saving the list, restart the tool with \"{install}\".",
                $"После сохранения списка перезапустите инструмент кнопкой «{install}»."));
        }
        catch (Exception ex)
        {
            Log.Notify(Loc.T($"Liste açılamadı: {ex.Message}", $"Couldn't open the list: {ex.Message}", $"Не удалось открыть список: {ex.Message}"), isError: true);
        }
    }

    /// <param name="action">İşi yapar ve başarı mesajını döner.</param>
    private async Task RunBusy(Func<Task<string>> action)
    {
        IsEnabled = false;
        BusyText.Text = UiText.Get("dpi.installing"); // Ayar bulucu ilerleme metniyle değiştirir.
        BusyText.Visibility = Visibility.Visible;
        try
        {
            // Discord açıksa kapatılır, işlem bitince yeniden açılır (ayar açıksa).
            Log.Notify(await DiscordGuard.RunAsync(action));
        }
        catch (RebootRequiredException ex)
        {
            Log.Notify(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error("DPI işlemi başarısız", ex);
            Log.Notify(Loc.T($"İşlem başarısız: {ex.Message}", $"Operation failed: {ex.Message}", $"Операция не удалась: {ex.Message}"), isError: true);
        }
        finally
        {
            BusyText.Visibility = Visibility.Collapsed;
            IsEnabled = true;
            await RefreshAsync();
        }
    }
}
