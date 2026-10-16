using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace DucKING.Core;

/// <param name="PingMs">Yanıt yoksa null.</param>
/// <param name="CpuMhz">İşlemcinin anlık (efektif) saat hızı.</param>
public sealed record TelemetrySample(double? PingMs, double CpuPercent, double? CpuMhz, double RamUsedGb, double RamTotalGb, GpuSample? Gpu);

/// <summary>
/// Saniyede bir ping, işlemci, bellek ve ekran kartı ölçer. Tek ortak örnek vardır; yalnızca en az bir
/// ekran (ana sayfa, Donanım) abone olduğunda çalışır, arka planda kaynak harcamaz.
/// </summary>
public sealed partial class Telemetry : IDisposable
{
    public const string PingHost = "discord.com";
    private const string FallbackHost = "1.1.1.1";

    public static Telemetry Shared { get; } = new();

    /// <summary>Kayıt defterindeki işlemci adı (ör. "AMD Ryzen 7 7800X3D 8-Core Processor").</summary>
    public static string CpuName { get; } =
        (Reg.Read(Microsoft.Win32.RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") as string)?.Trim()
        ?? UiText.Get("home.cpu");

    private static readonly int CpuBaseMhz =
        Reg.Read(Microsoft.Win32.RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz") is int mhz ? mhz : 0;

    private readonly Ping _ping = new();
    private readonly List<Action<TelemetrySample>> _subscribers = [];
    private readonly Lazy<GpuSensor?> _gpu = new(GpuSensor.TryCreate);
    private System.Diagnostics.PerformanceCounter? _cpuPerformance;
    private Timer? _timer;
    private IPAddress? _target;
    private DateTime _resolvedAt;
    private (ulong Idle, ulong Total)? _lastCpu;
    private int _busy;

    /// <summary>Ölçümlere abone olur; dönen nesne Dispose edilince abonelik biter.</summary>
    public IDisposable Subscribe(Action<TelemetrySample> handler)
    {
        lock (_subscribers)
        {
            _subscribers.Add(handler);
            _timer ??= new Timer(_ => _ = SampleAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        return new Subscription(() =>
        {
            lock (_subscribers)
            {
                _subscribers.Remove(handler);
                if (_subscribers.Count > 0) return;
                _timer?.Dispose();
                _timer = null;
            }
        });
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    private async Task SampleAsync()
    {
        // Önceki ölçüm (ör. yavaş ping) bitmeden yenisini başlatma.
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var ping = await PingAsync();
            var cpu = ReadCpu();
            var (used, total) = ReadMemory();
            var sample = new TelemetrySample(ping, cpu, ReadCpuMhz(), used, total, _gpu.Value?.Read());

            Action<TelemetrySample>[] handlers;
            lock (_subscribers) handlers = _subscribers.ToArray();
            foreach (var handler in handlers) handler(sample);
        }
        catch (Exception ex)
        {
            Log.Error("Telemetri ölçülemedi", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private async Task<double?> PingAsync()
    {
        // Adres dakikada bir yenilenir; DNS değişirse grafik yeni sunucuya geçer.
        if (_target is null || DateTime.UtcNow - _resolvedAt > TimeSpan.FromMinutes(1))
        {
            try
            {
                _target = (await Dns.GetHostAddressesAsync(PingHost))
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }
            catch (Exception)
            {
                _target = null;
            }
            _target ??= IPAddress.Parse(FallbackHost);
            _resolvedAt = DateTime.UtcNow;
        }

        try
        {
            var reply = await _ping.SendPingAsync(_target, 900);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    /// <summary>İki ölçüm arasındaki boşta kalma oranından toplam işlemci kullanımı.</summary>
    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var now = (Idle: idle.Value, Total: kernel.Value + user.Value); // kernel süresi boşta kalmayı da içerir
        var last = _lastCpu;
        _lastCpu = now;
        if (last is null || now.Total <= last.Value.Total) return 0;

        var total = now.Total - last.Value.Total;
        var idleDelta = now.Idle - last.Value.Idle;
        return Math.Clamp(100.0 * (total - idleDelta) / total, 0, 100);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static (double UsedGb, double TotalGb) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
        const double gb = 1024d * 1024 * 1024;
        return ((status.TotalPhys - status.AvailPhys) / gb, status.TotalPhys / gb);
    }

    /// <summary>"% Processor Performance" taban frekansa göre yüzdedir (boost'ta 100'ü aşar).</summary>
    private double? ReadCpuMhz()
    {
        if (CpuBaseMhz == 0) return null;
        try
        {
            _cpuPerformance ??= new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
            var percent = _cpuPerformance.NextValue();
            return percent > 0 ? CpuBaseMhz * percent / 100 : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cpuPerformance?.Dispose();
        if (_gpu.IsValueCreated) _gpu.Value?.Dispose();
        _ping.Dispose();
    }
}
