using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DucKING.Core;

/// <param name="UsagePercent">En yoğun motorun (3D, Compute, Video…) kullanım oranı; Görev Yöneticisi'ndeki değer.</param>
public sealed record GpuSample(
    string Name,
    double? UsagePercent,
    double? TemperatureC,
    double? VramUsedGb,
    double VramTotalGb,
    int? FanRpm,
    double? PowerPercent);

/// <summary>
/// Ek sürücü kurmadan ekran kartı verisi: sıcaklık/fan/güç Windows'un D3DKMT arayüzünden (Görev
/// Yöneticisi'nin kaynağı, WDDM 2.4+), kullanım ve VRAM "GPU Engine" / "GPU Adapter Memory" sayaçlarından.
/// En çok ayrılmış belleği olan adaptör (harici ekran kartı) izlenir.
/// </summary>
public sealed partial class GpuSensor : IDisposable
{
    private const int QueryGetSegmentSize = 3;
    private const int QueryAdapterRegistryInfo = 8;
    private const int QueryAdapterPerfData = 62;

    private readonly uint _adapter;
    private readonly string _luid;
    private readonly Dictionary<string, PerformanceCounter> _engines = new();
    private PerformanceCounter? _vram;
    private DateTime _instancesReadAt;

    public string Name { get; }
    public double VramTotalGb { get; }

    private GpuSensor(uint adapter, Luid luid, string name, ulong vramBytes)
    {
        _adapter = adapter;
        _luid = $"luid_0x{luid.High:X8}_0x{luid.Low:X8}";
        Name = name;
        VramTotalGb = vramBytes / (1024d * 1024 * 1024);
    }

    /// <summary>Harici ekran kartını bulur; bulunamazsa null.</summary>
    public static GpuSensor? TryCreate()
    {
        try
        {
            var enumInfo = new EnumAdapters2();
            if (D3DKMTEnumAdapters2(ref enumInfo) != 0 || enumInfo.NumAdapters == 0) return null;

            var adapters = new AdapterInfo[enumInfo.NumAdapters];
            var handle = GCHandle.Alloc(adapters, GCHandleType.Pinned);
            try
            {
                enumInfo.Adapters = handle.AddrOfPinnedObject();
                if (D3DKMTEnumAdapters2(ref enumInfo) != 0) return null;
            }
            finally
            {
                handle.Free();
            }

            AdapterInfo? best = null;
            ulong bestVram = 0;
            foreach (var info in adapters.Take((int)enumInfo.NumAdapters))
            {
                var size = Query<SegmentSizeInfo>(info.Adapter, QueryGetSegmentSize);
                if (size is { } s && s.DedicatedVideoMemorySize > bestVram)
                {
                    bestVram = s.DedicatedVideoMemorySize;
                    best = info;
                }
            }

            foreach (var info in adapters.Take((int)enumInfo.NumAdapters).Where(a => best is null || a.Adapter != best.Value.Adapter))
                Close(info.Adapter);
            if (best is not { } chosen) return null;

            var registry = Query<AdapterRegistryInfo>(chosen.Adapter, QueryAdapterRegistryInfo);
            var name = registry?.AdapterString?.Trim() is { Length: > 0 } n ? n : UiText.Get("home.gpu");
            return new GpuSensor(chosen.Adapter, chosen.Luid, name, bestVram);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Error("Ekran kartı sensörü kullanılamıyor", ex);
            return null;
        }
    }

    public GpuSample Read()
    {
        var perf = Query<AdapterPerfData>(_adapter, QueryAdapterPerfData);
        double? temperature = perf is { Temperature: > 0 } p ? p.Temperature / 10.0 : null;
        int? fan = perf is { FanRpm: > 0 } f ? (int)f.FanRpm : null;
        double? power = perf is { Power: > 0 } w ? w.Power / 10.0 : null;

        return new GpuSample(Name, ReadUsage(), temperature, ReadVram(), VramTotalGb, fan, power);
    }

    /// <summary>Yalnızca sıcaklık (performans sayaçlarına dokunmaz; üst menü rozeti için ucuz okuma).</summary>
    public double? ReadTemperature() =>
        Query<AdapterPerfData>(_adapter, QueryAdapterPerfData) is { Temperature: > 0 } p ? p.Temperature / 10.0 : null;

    private double? ReadUsage()
    {
        try
        {
            // Oyunlar açılıp kapandıkça sayaç örnekleri değişir; listeyi birkaç saniyede bir yenile.
            if (DateTime.UtcNow - _instancesReadAt > TimeSpan.FromSeconds(3))
            {
                _instancesReadAt = DateTime.UtcNow;
                var current = new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                    .Where(n => n.Contains(_luid, StringComparison.OrdinalIgnoreCase))
                    .ToHashSet();
                foreach (var gone in _engines.Keys.Where(k => !current.Contains(k)).ToList())
                {
                    _engines[gone].Dispose();
                    _engines.Remove(gone);
                }
                foreach (var name in current.Where(n => !_engines.ContainsKey(n)))
                    _engines[name] = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, readOnly: true);
            }

            var byEngine = new Dictionary<string, double>();
            foreach (var (name, counter) in _engines)
            {
                var type = name[(name.LastIndexOf("engtype_", StringComparison.Ordinal) + 8)..];
                byEngine[type] = byEngine.GetValueOrDefault(type) + counter.NextValue();
            }
            return byEngine.Count == 0 ? 0 : Math.Clamp(byEngine.Values.Max(), 0, 100);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private double? ReadVram()
    {
        try
        {
            _vram ??= new PerformanceCounterCategory("GPU Adapter Memory").GetInstanceNames()
                .Where(n => n.Contains(_luid, StringComparison.OrdinalIgnoreCase))
                .Select(n => new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", n, readOnly: true))
                .FirstOrDefault();
            return _vram is null ? null : _vram.NextValue() / (1024d * 1024 * 1024);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var counter in _engines.Values) counter.Dispose();
        _vram?.Dispose();
        Close(_adapter);
    }

    // ---- D3DKMT (gdi32) ----

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint Low;
        public int High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public uint Adapter;
        public Luid Luid;
        public uint NumOfSources;
        public int PrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EnumAdapters2
    {
        public uint NumAdapters;
        public IntPtr Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public IntPtr PrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter
    {
        public uint Adapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SegmentSizeInfo
    {
        public ulong DedicatedVideoMemorySize;
        public ulong DedicatedSystemMemorySize;
        public ulong SharedSystemMemorySize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterRegistryInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string AdapterString;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string BiosString;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DacType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ChipType;
    }

    /// <summary>D3DKMT_ADAPTER_PERFDATA: sıcaklık ve güç onda bir birimle (35,2 °C → 352) gelir.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOC;
        public ulong MemoryBandwidth;
        public ulong PcieBandwidth;
        public uint FanRpm;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTEnumAdapters2(ref EnumAdapters2 info);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo info);

    [DllImport("gdi32.dll")]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter info);

    private static T? Query<T>(uint adapter, int type) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            // Bazı sorgular (ör. performans verisi) giriş olarak fiziksel adaptör numarası bekler; sıfırla.
            for (var i = 0; i < size; i++) Marshal.WriteByte(buffer, i, 0);
            var query = new QueryAdapterInfo { Adapter = adapter, Type = type, PrivateDriverData = buffer, PrivateDriverDataSize = (uint)size };
            return D3DKMTQueryAdapterInfo(ref query) == 0 ? Marshal.PtrToStructure<T>(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void Close(uint adapter)
    {
        var close = new CloseAdapter { Adapter = adapter };
        D3DKMTCloseAdapter(ref close);
    }
}
