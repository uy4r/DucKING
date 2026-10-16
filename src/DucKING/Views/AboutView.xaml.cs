using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DucKING.Core;

namespace DucKING.Views;

public partial class AboutView : UserControl, IRefreshable
{
    private sealed record Credit(string Name, string Usage, string License, string Url);

    public AboutView()
    {
        InitializeComponent();
        Credits.ItemsSource = new Credit[]
        {
            new("GoodbyeDPI", Loc.T("DPI engel aşma aracı", "DPI bypass tool", "Инструмент обхода DPI"), "Apache 2.0",
                "https://github.com/ValdikSS/GoodbyeDPI"),
            new("zapret", Loc.T("DPI engel aşma aracı (winws)", "DPI bypass tool (winws)", "Инструмент обхода DPI (winws)"), "MIT",
                "https://github.com/bol-van/zapret"),
            new("ByeDPI", Loc.T("SOCKS5 proxy ile DPI engel aşma", "DPI bypass via a SOCKS5 proxy", "Обход DPI через SOCKS5-прокси"), "MIT",
                "https://github.com/hufrea/byedpi"),
            new("WinDivert", Loc.T("Paket yakalama sürücüsü", "Packet capture driver", "Драйвер перехвата пакетов"), "LGPL v3",
                "https://github.com/basil00/WinDivert"),
            new("ProxiFyre", Loc.T("Discord trafiğini ByeDPI'a yönlendirir", "Routes Discord traffic to ByeDPI", "Направляет трафик Discord в ByeDPI"),
                "AGPL-3.0", "https://github.com/wiresock/proxifyre"),
            new("Windows Packet Filter", Loc.T("ProxiFyre'ın kullandığı ağ sürücüsü", "Network driver used by ProxiFyre",
                "Сетевой драйвер, который использует ProxiFyre"), "MIT", "https://github.com/wiresock/ndisapi"),
        };
        UpdatePanel.Visibility = UpdateChecker.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Açılıştaki denetim yeni sürüm bulduysa sayfaya girilince gösterilir.</summary>
    public Task RefreshAsync()
    {
        ShowAvailableUpdate();
        return Task.CompletedTask;
    }

    private void ShowAvailableUpdate()
    {
        if (UpdateChecker.Available is not { } update) return;
        UpdateStatus.Text = Loc.T($"Yeni sürüm: {update.Tag}", $"New version: {update.Tag}", $"Новая версия: {update.Tag}");
        ReleaseButton.Visibility = Visibility.Visible;
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatus.Text = Loc.T("Denetleniyor…", "Checking…", "Проверка…");
        try
        {
            await UpdateChecker.CheckAsync();
            var current = UpdateChecker.CurrentVersion.ToString(3);
            if (UpdateChecker.Available is null)
                UpdateStatus.Text = Loc.T($"DucKING güncel (v{current}).", $"DucKING is up to date (v{current}).",
                    $"Установлена последняя версия DucKING (v{current}).");
        }
        catch (Exception ex)
        {
            // Hata ayrıntısı yalnızca günlüğe yazılır.
            Log.Error("Güncelleme denetlenemedi", ex);
            UpdateStatus.Text = Loc.T("Şu an denetlenemedi, daha sonra tekrar deneyin.", "Couldn't check right now, try again later.",
                "Сейчас проверить не удалось, попробуйте позже.");
        }
        ShowAvailableUpdate();
        CheckUpdateButton.IsEnabled = true;
    }

    private void Release_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateChecker.Available is { } update) Open(update.PageUrl);
    }

    private async void Report_Click(object sender, RoutedEventArgs e)
    {
        ReportButton.IsEnabled = false;
        string path;
        try
        {
            path = await Task.Run(() => DiagnosticsReport.Create());
        }
        catch (Exception ex)
        {
            Log.Notify(Loc.T($"Tanılama raporu oluşturulamadı: {ex.Message}", $"Couldn't create the diagnostics report: {ex.Message}",
                $"Не удалось создать отчёт диагностики: {ex.Message}"), isError: true);
            return;
        }
        finally
        {
            ReportButton.IsEnabled = true;
        }

        Log.Notify(Loc.T($"Tanılama raporu Masaüstüne kaydedildi: {Path.GetFileName(path)}",
            $"Diagnostics report saved to your Desktop: {Path.GetFileName(path)}",
            $"Отчёт диагностики сохранён на рабочий стол: {Path.GetFileName(path)}"));
        try
        {
            // Dosya Gezgini'nde seçili olarak gösterilir.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error("Rapor Dosya Gezgini'nde gösterilemedi", ex);
        }
    }

    private void Link_Click(object sender, RoutedEventArgs e) => Open((string)((FrameworkElement)sender).Tag);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(AppPaths.LogFile)) Open(AppPaths.LogFile);
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) => Open(AppPaths.DataDir);

    private void OpenLicenses_Click(object sender, RoutedEventArgs e) =>
        Open(Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md"));

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Notify(Loc.T($"Açılamadı: {ex.Message}", $"Couldn't open: {ex.Message}", $"Не удалось открыть: {ex.Message}"), isError: true);
        }
    }
}
