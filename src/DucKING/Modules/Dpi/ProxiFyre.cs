using System.IO;
using System.Text.Json;
using DucKING.Core;
using Microsoft.Win32;

namespace DucKING.Modules.Dpi;

/// <summary>
/// ByeDPI yalnızca bir SOCKS5 proxy'dir; Discord gibi proxy ayarı olmayan uygulamaların trafiğini
/// ona yönlendirmek için ProxiFyre (Windows Packet Filter sürücüsü üzerinde çalışır) kullanılır.
/// </summary>
public static class ProxiFyre
{
    public const string ServiceName = "ProxiFyreService";
    private const string DriverService = "NDISRD";
    private const string FirewallRule = "DucKING ProxiFyre";

    private static string InstallDir => Path.Combine(AppPaths.InstalledToolsDir, "proxifyre");
    private static string ExePath => Path.Combine(InstallDir, "ProxiFyre.exe");
    private static string PrereqDir => Path.Combine(AppPaths.BundledToolsDir, "prereq");

    // Ters bölü içeren değerler tam yolda aranır: Discord klasöründeki Update.exe de yakalanır,
    // ama başka programların Update.exe'lerine dokunulmaz.
    private static readonly string[] DiscordApps =
    [
        "Discord", "DiscordPTB", "DiscordCanary",
        @"\Discord\", @"\DiscordPTB\", @"\DiscordCanary\",
        "RobloxPlayerBeta", "RobloxPlayerInstaller", @"\Roblox\",
    ];

    private static readonly string[] BrowserApps =
    [
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "zen", "librewolf", "browser", "chromium",
    ];

    public static bool IsRunning => NativeServices.IsRunning(ServiceName);

    /// <summary>
    /// Ön gereksinimleri kurar ve ProxiFyre servisini başlatır.
    /// </summary>
    /// <exception cref="RebootRequiredException">Sürücü kuruldu ama yeniden başlatma olmadan yüklenemedi.</exception>
    public static void Install(int socksPort, bool includeBrowsers)
    {
        EnsureVcRuntime();
        var rebootNeeded = EnsurePacketFilterDriver();

        Remove();
        CopyFiles();
        WriteConfig(socksPort, includeBrowsers);
        LockDownFolder();
        AddFirewallRules();

        try
        {
            NativeServices.CreateAndStart(ServiceName, "DucKING - ProxiFyre",
                Loc.T("Discord ve seçili uygulamaların trafiğini ByeDPI'a yönlendirir.",
                    "Routes the traffic of Discord and selected apps to ByeDPI.",
                    "Направляет трафик Discord и выбранных приложений в ByeDPI."),
                $"\"{ExePath}\"", dependencies: DriverService);
        }
        catch (Exception) when (rebootNeeded)
        {
            throw new RebootRequiredException();
        }
        catch (Exception ex)
        {
            var reason = LastLogLines(5);
            Log.Error($"ProxiFyre başlatılamadı. ProxiFyre günlüğü:\n{reason}", ex);
            var detail = reason.Split('\n').LastOrDefault(l => l.Length > 0) ?? ex.Message;
            throw new InvalidOperationException(Loc.T($"ProxiFyre başlatılamadı: {detail}", $"ProxiFyre couldn't start: {detail}",
                $"Не удалось запустить ProxiFyre: {detail}"), ex);
        }

        Log.Info($"ProxiFyre kuruldu (tarayıcılar: {includeBrowsers}).");
    }

    public static void Remove()
    {
        NativeServices.StopAndDelete(ServiceName);
        ProcessRunner.Run("netsh.exe", $"advfirewall firewall delete rule name=\"{FirewallRule}\"", logFailure: false);
    }

    /// <summary>socksify.dll, Visual C++ 2015-2022 çalışma zamanına (MSVCP140.dll) ihtiyaç duyar.</summary>
    private static void EnsureVcRuntime()
    {
        var installed = Reg.Read(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64", "Installed");
        if (installed is int and 1) return;

        Log.Info("Visual C++ çalışma zamanı kuruluyor…");
        var result = ProcessRunner.Run(Path.Combine(PrereqDir, "VC_redist.x64.exe"), "/install /quiet /norestart", timeoutMs: 300_000);
        // 3010: yeniden başlatma gerekli, 1638: daha yeni sürüm zaten kurulu.
        if (result.ExitCode is not (0 or 3010 or 1638))
            throw new InvalidOperationException(Loc.T($"Visual C++ çalışma zamanı kurulamadı (kod {result.ExitCode}).",
                $"Couldn't install the Visual C++ runtime (code {result.ExitCode}).",
                $"Не удалось установить среду выполнения Visual C++ (код {result.ExitCode})."));
    }

    /// <returns>Kurulum yeniden başlatma istediyse true.</returns>
    private static bool EnsurePacketFilterDriver()
    {
        if (NativeServices.Exists(DriverService)) return false;

        Log.Info("Windows Packet Filter sürücüsü kuruluyor…");
        var msi = Path.Combine(PrereqDir, "Windows.Packet.Filter.3.6.1.1.x64.msi");
        var result = ProcessRunner.Run("msiexec.exe", $"/i \"{msi}\" /quiet /norestart", timeoutMs: 300_000);
        if (result.ExitCode is not (0 or 3010))
            throw new InvalidOperationException(Loc.T($"Windows Packet Filter sürücüsü kurulamadı (kod {result.ExitCode}).",
                $"Couldn't install the Windows Packet Filter driver (code {result.ExitCode}).",
                $"Не удалось установить драйвер Windows Packet Filter (код {result.ExitCode})."));
        return result.ExitCode == 3010;
    }

    private static void CopyFiles()
    {
        var source = Path.Combine(AppPaths.BundledToolsDir, "proxifyre");
        Directory.CreateDirectory(InstallDir);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(InstallDir, Path.GetFileName(file)), overwrite: true);
    }

    private static void WriteConfig(int socksPort, bool includeBrowsers)
    {
        var config = new
        {
            logLevel = "Info",
            proxies = new[]
            {
                new
                {
                    appNames = includeBrowsers ? DiscordApps.Concat(BrowserApps).ToArray() : DiscordApps,
                    socks5ProxyEndpoint = $"127.0.0.1:{socksPort}",
                    supportedProtocols = new[] { "TCP" },
                },
            },
        };
        File.WriteAllText(Path.Combine(InstallDir, "app-config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// ProxiFyre 2.6.1 her başlangıçta denetler: klasör ve tüm üst klasörler SYSTEM/Yöneticiler/TrustedInstaller'a
    /// ait olmalı, klasörde (miras kalan CREATOR OWNER dahil) başka kimseye yazma/silme izni bulunmamalı.
    /// </summary>
    private static void LockDownFolder()
    {
        if (!FolderSecurity.IsProtected(AppPaths.DataDir))
            FolderSecurity.Protect(AppPaths.DataDir, allowUsersRead: true);
        FolderSecurity.ProtectExclusively(InstallDir);
    }

    /// <summary>ProxiFyre kendi günlüğünü (NLog) klasöründeki logs\ altına yazar.</summary>
    private static string LastLogLines(int count)
    {
        try
        {
            var latest = new DirectoryInfo(Path.Combine(InstallDir, "logs"))
                .EnumerateFiles("*.txt").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (latest is null) return Loc.T("(ProxiFyre günlüğü yok)", "(no ProxiFyre log)", "(нет журнала ProxiFyre)");
            using var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var lines = new StreamReader(stream).ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join('\n', lines.TakeLast(count).Select(l => l.TrimEnd()));
        }
        catch (Exception ex)
        {
            return Loc.T($"(günlük okunamadı: {ex.Message})", $"(couldn't read the log: {ex.Message})", $"(не удалось прочитать журнал: {ex.Message})");
        }
    }

    /// <summary>ProxiFyre yönlendirilen bağlantıları dinamik portlarda karşılar; gelen trafiğe izin verilmeli.</summary>
    private static void AddFirewallRules()
    {
        ProcessRunner.Run("netsh.exe", $"advfirewall firewall delete rule name=\"{FirewallRule}\"", logFailure: false);
        foreach (var dir in new[] { "in", "out" })
            ProcessRunner.Run("netsh.exe", $"advfirewall firewall add rule name=\"{FirewallRule}\" dir={dir} action=allow program=\"{ExePath}\" enable=yes");
    }
}

public sealed class RebootRequiredException()
    : Exception(Loc.T("Paket filtresi sürücüsü kuruldu. Bilgisayarı yeniden başlatın; ByeDPI açılışta otomatik çalışacak.",
        "The packet filter driver was installed. Restart your PC; ByeDPI will start automatically at boot.",
        "Драйвер фильтра пакетов установлен. Перезагрузите компьютер — ByeDPI запустится автоматически при включении."));
