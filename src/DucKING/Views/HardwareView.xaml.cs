using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DucKING.Core;

namespace DucKING.Views;

public partial class HardwareView : UserControl
{
    private const int History = 60;

    private readonly List<double?> _gpuTemp = [];
    private readonly List<double?> _gpuUsage = [];
    private readonly List<double?> _cpuUsage = [];
    private IDisposable? _telemetry;

    public HardwareView()
    {
        InitializeComponent();
        CpuName.Text = Telemetry.CpuName;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _telemetry ??= Telemetry.Shared.Subscribe(sample => Dispatcher.BeginInvoke(() => Show(sample)));

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _telemetry?.Dispose();
        _telemetry = null;
    }

    private void Show(TelemetrySample sample)
    {
        var gpu = sample.Gpu;
        GpuName.Text = gpu?.Name ?? Loc.T("Ekran kartı bulunamadı", "No graphics card found", "Видеокарта не найдена");
        GpuTemp.Text = gpu?.TemperatureC is { } t ? $"{t:0}" : "—";
        GpuUsage.Text = gpu?.UsagePercent is { } u ? $"{u:0}" : "—";
        GpuVram.Text = gpu is null ? "—" : gpu.VramUsedGb is { } v ? $"{v:0.0} / {gpu.VramTotalGb:0} {Loc.Gb}" : $"{gpu.VramTotalGb:0} {Loc.Gb}";
        GpuFan.Text = gpu?.FanRpm is { } rpm ? $"{rpm} " + Loc.T("rpm", "rpm", "об/мин") : Loc.T("durdu / yok", "stopped / none", "стоит / нет");
        GpuPower.Text = gpu?.PowerPercent is { } p ? Loc.Percent(p) : "—";

        CpuUsage.Text = $"{sample.CpuPercent:0}";
        CpuClock.Text = sample.CpuMhz is { } mhz ? $"{mhz / 1000:0.00}" : "—";

        RamUsed.Text = $"{sample.RamUsedGb:0.0}";
        RamTotal.Text = $" / {sample.RamTotalGb:0} {Loc.Gb}";
        ((ScaleTransform)RamBar.RenderTransform).ScaleX = sample.RamTotalGb > 0 ? sample.RamUsedGb / sample.RamTotalGb : 0;

        Sparkline.Push(_gpuTemp, gpu?.TemperatureC, History);
        Sparkline.Push(_gpuUsage, gpu?.UsagePercent, History);
        Sparkline.Push(_cpuUsage, sample.CpuPercent, History);
        DrawCharts();
    }

    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawCharts();

    private void DrawCharts()
    {
        // Sıcaklık ve kullanım aynı 0–100 ölçeğinde: °C değerleri pratikte bu aralıkta kalır.
        GpuTempPath.Data = Sparkline.Build(_gpuTemp, History, GpuChart.ActualWidth, GpuChart.ActualHeight, 100);
        GpuUsagePath.Data = Sparkline.Build(_gpuUsage, History, GpuChart.ActualWidth, GpuChart.ActualHeight, 100);
        CpuPath.Data = Sparkline.Build(_cpuUsage, History, CpuChart.ActualWidth, CpuChart.ActualHeight, 100);
    }
}
