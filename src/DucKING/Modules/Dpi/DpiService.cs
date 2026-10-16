using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using DucKING.Core;
using DucKING.Modules.Dns;

namespace DucKING.Modules.Dpi;

public enum DpiRegion
{
    None,
    Turkey,
    Russia,
}

/// <param name="Arguments">{dir}, kurulumda aracın klasörüyle değiştirilir (paketteki dosyalar için).</param>
/// <param name="Source">Presetin alındığı yerin adresi (yoksa null).</param>
public sealed record DpiPreset(string Name, string Arguments, DpiRegion Region = DpiRegion.None, string? Source = null)
{
    /// <summary>Preset listesindeki grup başlığı.</summary>
    public string RegionName => Region switch
    {
        DpiRegion.Turkey => Loc.T("Türkiye", "Turkey", "Турция"),
        DpiRegion.Russia => Loc.T("Rusya", "Russia", "Россия"),
        _ => "",
    };

    // ComboBox'ın seçili öğe kutusu bu metni gösterir.
    public override string ToString() => Name;
}

public sealed class DpiTool
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ServiceName { get; init; }
    public required string Folder { get; init; }
    public required string Executable { get; init; }
    public required string Description { get; init; }

    /// <summary>Profiles/dpi-presets.json'dan (ya da GitHub'daki güncel sürümden), bkz. <see cref="DpiPresets"/>.</summary>
    public IReadOnlyList<DpiPreset> Presets => DpiPresets.For(Id);

    /// <summary>Kullanıcının düzenleyebileceği liste dosyası (yoksa null).</summary>
    public string? ListFile { get; init; }

    /// <summary>Kurulumdaki tek seçeneğin metni (liste kullanımı ya da tarayıcıları dahil etme).</summary>
    public string? OptionTitle { get; init; }

    public string InstallDir => Path.Combine(AppPaths.InstalledToolsDir, Folder);
    public string ExePath => Path.Combine(InstallDir, Executable);
}

/// <param name="HelperRunning">ByeDPI için ProxiFyre'ın durumu; diğer araçlarda null.</param>
public sealed record DpiStatus(DpiTool? Tool, bool Running, string? Arguments, bool? HelperRunning = null);

/// <summary>
/// GoodbyeDPI, zapret ve ByeDPI'ı Windows servisi olarak kurar. GoodbyeDPI ile zapret aynı
/// WinDivert sürücüsünü kullandığından aynı anda yalnızca bir araç kurulu tutulur.
/// </summary>
public static class DpiService
{
    public static DpiTool GoodbyeDpi { get; } = new()
    {
        Id = "goodbyedpi",
        Name = "GoodbyeDPI",
        ServiceName = "GoodbyeDPI",
        Folder = "goodbyedpi",
        Executable = "goodbyedpi.exe",
        Description = Loc.T("En hafif seçenek. Discord, Roblox gibi engelli sitelere erişim sağlar; oyun trafiğine dokunmaz.",
            "The lightest option. Unblocks sites like Discord and Roblox; doesn't touch game traffic.",
            "Самый лёгкий вариант. Открывает заблокированные сайты вроде Discord и Roblox; игровой трафик не затрагивает."),
        ListFile = "blacklist.txt",
        OptionTitle = Loc.T("Yalnızca listedeki sitelere uygula (önerilen)", "Apply only to sites on the list (recommended)",
            "Применять только к сайтам из списка (рекомендуется)"),
    };

    public static DpiTool Zapret { get; } = new()
    {
        Id = "zapret",
        Name = "zapret",
        ServiceName = "zapret",
        Folder = "zapret",
        Executable = "winws.exe",
        Description = Loc.T("En güçlü seçenek. Discord sesli sohbet (UDP) dahil çalışır. İnternet sağlayıcınıza uygun preseti seçin.",
            "The most powerful option. Works for Discord voice chat (UDP) too. Pick the preset for your ISP.",
            "Самый мощный вариант. Работает и для голосового чата Discord (UDP). Выберите пресет своего провайдера."),
        ListFile = "exclude.txt",
        OptionTitle = Loc.T("Oyun sitelerini hariç tut (Steam, Epic, Riot…)", "Exclude game sites (Steam, Epic, Riot…)",
            "Исключить игровые сайты (Steam, Epic, Riot…)"),
    };

    public static DpiTool ByeDpi { get; } = new()
    {
        Id = "byedpi",
        Name = "ByeDPI",
        ServiceName = "ByeDPI",
        Folder = "byedpi",
        Executable = "ciadpi.exe",
        Description = Loc.T("Discord ve Roblox trafiğini ProxiFyre ile ByeDPI'a yönlendirir. WinDivert kullanmaz.",
            "Routes Discord and Roblox traffic to ByeDPI via ProxiFyre. Doesn't use WinDivert.",
            "Направляет трафик Discord и Roblox в ByeDPI через ProxiFyre. Не использует WinDivert."),
        OptionTitle = Loc.T("Tarayıcıları da ByeDPI üzerinden geçir", "Route browsers through ByeDPI too", "Пропускать через ByeDPI и браузеры"),
    };

    public static IReadOnlyList<DpiTool> Tools { get; } = [GoodbyeDpi, Zapret, ByeDpi];

    private const int ByeDpiPort = 1080;

    /// <summary>Başka DPI araçlarının kurduğu ve WinDivert'i kullanan yaygın servis adları.</summary>
    private static readonly string[] LegacyServices = ["zapret2", "winws1", "winws2"];

    /// <summary>Kurulu aracı ve çalışma argümanlarını servis kaydından okur.</summary>
    public static DpiStatus GetStatus()
    {
        foreach (var tool in Tools)
        {
            var image = NativeServices.GetImagePath(tool.ServiceName);
            if (image is null) continue;
            bool? helper = tool == ByeDpi ? ProxiFyre.IsRunning : null;
            return new DpiStatus(tool, NativeServices.IsRunning(tool.ServiceName), ExtractArguments(image), helper);
        }
        return new DpiStatus(null, false, null);
    }

    /// <param name="option">GoodbyeDPI/zapret: liste dosyasını kullan. ByeDPI: tarayıcıları da yönlendir.</param>
    public static void Install(DpiTool tool, DpiPreset preset, bool option)
    {
        RemoveAll();
        CopyTool(tool);

        var args = BuildArguments(tool, preset, option);
        NativeServices.CreateAndStart(
            tool.ServiceName,
            $"DucKING - {tool.Name}",
            Loc.T($"DucKING tarafından kuruldu ({preset.Name}). DPI engellerini aşmak için.",
                $"Installed by DucKING ({preset.Name}). Bypasses DPI blocking.",
                $"Установлено DucKING ({preset.Name}). Для обхода DPI-блокировок."),
            $"\"{tool.ExePath}\" {args}");

        Log.Info($"{tool.Name} kuruldu: {preset.Name} -> {args}");

        // Önceki araçtan ya da modem DNS'inden kalmış sahte (engel sayfası) adresler önbellekte kalmasın.
        DnsService.FlushCache();

        if (tool == ByeDpi)
            ProxiFyre.Install(ByeDpiPort, includeBrowsers: option);
    }

    /// <summary>Servisin çalıştıracağı argümanlar; PresetFinder denemelerde de aynısını kullanır.</summary>
    internal static string BuildArguments(DpiTool tool, DpiPreset preset, bool option, int byeDpiPort = ByeDpiPort)
    {
        var args = ResolvePaths(tool, preset);
        // Yalnızca bu bilgisayardan erişilebilsin; varsayılan 0.0.0.0 proxy'yi yerel ağa açar.
        if (tool == ByeDpi) return $"--ip 127.0.0.1 --port {byeDpiPort} {args}";
        if (!option || tool.ListFile is null) return args;

        // zapret'te her profilin (--new ile ayrılan) kendi listesi olduğundan hariç tutma listesi hepsine eklenir.
        return tool == Zapret
            ? args.Replace(" --new ", $"{ZapretExclude} --new ", StringComparison.Ordinal) + ZapretExclude
            : args + $" --blacklist \"{Path.Combine(tool.InstallDir, tool.ListFile)}\"";
    }

    /// <summary>Kurulu presetin adını bulur (ek parametreler kurulumda eklendiğinden içerme ile bakılır).</summary>
    public static DpiPreset? FindPreset(DpiTool tool, string? arguments)
    {
        if (arguments is null) return null;
        // Profillerin arasına eklenen hariç tutma listesi karşılaştırmayı bozmasın.
        // Eski port listesiyle kurulmuş Türkiye presetleri de tanınsın (yeniden kurulana kadar eski listeyle çalışırlar).
        var bare = tool == Zapret
            ? arguments.Replace(ZapretExclude, "", StringComparison.Ordinal).Replace(OldTurkeyUdp, TurkeyUdp, StringComparison.Ordinal)
            : arguments;
        // En uzun eşleşme: "--set-ttl 3", "--set-ttl 3 --dns-addr …" presetinin içinde de geçer.
        return tool.Presets.Where(p => bare.Contains(ResolvePaths(tool, p), StringComparison.Ordinal))
            .MaxBy(p => p.Arguments.Length);
    }

    // Eski Türkiye listesi Discord ses portlarından yalnızca 50000 ve 50100'ü yakalıyordu (virgül aralık değil, tek port).
    // Yeni aralıklar Flowseal/zapret-discord-youtube general.bat'tan.
    private const string OldTurkeyUdp = "--wf-udp=443,50000,50100 ";
    private const string TurkeyUdp = "--wf-udp=443,19294-19344,50000-50100 ";

    private static string ResolvePaths(DpiTool tool, DpiPreset preset) => preset.Arguments.Replace("{dir}", tool.InstallDir, StringComparison.Ordinal);

    private static string ZapretExclude => $" --hostlist-exclude=\"{Path.Combine(Zapret.InstallDir, Zapret.ListFile!)}\"";

    /// <summary>Tüm DPI servislerini, ProxiFyre'ı ve WinDivert sürücüsünü kaldırır.</summary>
    public static void RemoveAll()
    {
        ProxiFyre.Remove();
        foreach (var name in ServiceNames)
            NativeServices.StopAndDelete(name);
        KillManualCopies();
        UnloadDriver();
    }

    /// <summary>DPI araçlarının ve WinDivert kullanan başka programların servisleri.</summary>
    private static IEnumerable<string> ServiceNames => Tools.Select(t => t.ServiceName).Concat(LegacyServices);

    /// <summary>
    /// Çalışan DPI servislerini silmeden durdurur (PresetFinder denemeleri için) ve durdurulanları döner.
    /// Elle başlatılmış kopyalar kapatılır, sürücü de yeniden yüklenebilsin diye kaldırılır.
    /// </summary>
    internal static List<string> PauseAll()
    {
        var stopped = new List<string>();
        // ProxiFyre ByeDPI'a bağlı olduğundan önce durdurulur, en son başlatılır.
        foreach (var name in ServiceNames.Prepend(ProxiFyre.ServiceName))
        {
            if (!NativeServices.IsRunning(name)) continue;
            try
            {
                using var controller = new ServiceController(name);
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                stopped.Add(name);
            }
            catch (Exception ex)
            {
                Log.Error($"Servis durdurulamadı: {name}", ex);
            }
        }
        KillManualCopies();
        UnloadDriver();
        return stopped;
    }

    /// <summary><see cref="PauseAll"/> ile durdurulan servisleri ters sırayla yeniden başlatır.</summary>
    internal static void ResumeAll(IReadOnlyList<string> stopped)
    {
        KillManualCopies();
        UnloadDriver(); // Denenen aracın sürücüsü, eski aracın kendi sürümünü yükleyebilmesi için kaldırılır.
        foreach (var name in stopped.Reverse())
        {
            try
            {
                using var controller = new ServiceController(name);
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            }
            catch (Exception ex)
            {
                Log.Error($"Servis yeniden başlatılamadı: {name}", ex);
            }
        }
    }

    /// <summary>Servis dışında elle başlatılmış kopyalar WinDivert'i kilitli tutar.</summary>
    private static void KillManualCopies()
    {
        foreach (var name in new[] { "goodbyedpi", "winws", "ciadpi" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(); p.WaitForExit(5000); }
                catch (Exception ex) { Log.Error($"İşlem kapatılamadı: {name}", ex); }
                finally { p.Dispose(); }
            }
        }
    }

    /// <summary>Sürücü servisi; bir sonraki araç kendi WinDivert sürümünü yükleyebilsin diye silinir.</summary>
    private static void UnloadDriver()
    {
        foreach (var driver in new[] { "WinDivert", "WinDivert14" })
        {
            try { NativeServices.StopAndDelete(driver); }
            catch (Exception ex) { Log.Error($"{driver} sürücüsü kaldırılamadı", ex); }
        }
    }

    public static string? GetListFilePath(DpiTool tool)
    {
        if (tool.ListFile is null) return null;
        var installed = Path.Combine(tool.InstallDir, tool.ListFile);
        if (!File.Exists(installed))
        {
            Directory.CreateDirectory(tool.InstallDir);
            File.Copy(Path.Combine(AppPaths.BundledToolsDir, tool.Folder, tool.ListFile), installed);
        }
        return installed;
    }

    /// <summary>
    /// Araç dosyalarını ProgramData'ya kopyalar: servis, program klasörü taşınsa da çalışmaya devam eder.
    /// Kullanıcının düzenlediği liste dosyası korunur.
    /// </summary>
    internal static void CopyTool(DpiTool tool)
    {
        var source = Path.Combine(AppPaths.BundledToolsDir, tool.Folder);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(Loc.T($"{tool.Name} dosyaları bulunamadı: {source}",
                $"{tool.Name} files not found: {source}", $"Файлы {tool.Name} не найдены: {source}"));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(tool.InstallDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            var isUserList = tool.ListFile is not null && relative.Equals(tool.ListFile, StringComparison.OrdinalIgnoreCase);
            if (isUserList && File.Exists(target)) continue;

            File.Copy(file, target, overwrite: true);
        }
    }

    private static string ExtractArguments(string imagePath)
    {
        var trimmed = imagePath.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            return end < 0 ? "" : trimmed[(end + 1)..].Trim();
        }
        var space = trimmed.IndexOf(' ');
        return space < 0 ? "" : trimmed[(space + 1)..].Trim();
    }
}
