using System.Text.RegularExpressions;
using DucKING.Core;
using Microsoft.Win32;
using static DucKING.Core.RegValue;

namespace DucKING.Modules.Tweaks;

/// <summary>Oyunlarda FPS, gecikme ve takılmaları etkileyen Windows ayarları.</summary>
public static partial class PerformanceTweaks
{
    private const string SystemProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string GameConfig = @"System\GameConfigStore";
    private const string TcpInterfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public static IReadOnlyList<Tweak> Create() =>
    [
        PowerPlan.Tweak,
        new RegistryTweak(
            Hkcu(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
            Hkcu(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1))
        {
            Id = "game-mode",
            Title = Loc.T("Oyun Modu'nu aç", "Turn on Game Mode", "Включить игровой режим"),
            Description = Loc.T("Oyun açıkken Windows Update kurulumlarını ve arka plan işlerini erteler, kaynakları oyuna ayırır.",
                "Postpones Windows Update installs and background jobs while a game is running and gives its resources to the game.",
                "Откладывает установку обновлений Windows и фоновые задачи во время игры и отдаёт ресурсы игре."),
        },
        new RegistryTweak(
            Hkcu(GameConfig, "GameDVR_Enabled", 0, 1),
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0))
        {
            Id = "game-dvr",
            Title = Loc.T("Arka plan oyun kaydını (Game DVR) kapat",
                "Turn off background game recording (Game DVR)",
                "Отключить фоновую запись игр (Game DVR)"),
            Description = Loc.T("Xbox Game Bar'ın sürekli arka planda video kaydı tutmasını durdurur; FPS düşüşü ve takılmaları azaltır. (Win+Alt+R ile kayıt çalışmaz.)",
                "Stops Xbox Game Bar from constantly recording video in the background; reduces FPS drops and stutter. (Win+Alt+R recording won't work.)",
                "Xbox Game Bar перестаёт постоянно записывать видео в фоне; меньше просадок FPS и подтормаживаний. (Запись по Win+Alt+R работать не будет.)"),
            Risk = Risk.Moderate,
        },
        new RegistryTweak(
            Hklm(SystemProfile, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), 10),
            Hklm(SystemProfile, "SystemResponsiveness", 10, 20))
        {
            Id = "network-throttling",
            Title = Loc.T("Ağ kısıtlamasını kaldır", "Remove network throttling", "Снять ограничение сети"),
            Description = Loc.T("Windows'un multimedya sırasında ağ paketlerini kısmasını kapatır; çevrim içi oyunlarda ping dalgalanmasını azaltır.",
                "Stops Windows from throttling network packets during multimedia playback; reduces ping spikes in online games.",
                "Windows перестаёт ограничивать сетевые пакеты при воспроизведении мультимедиа; меньше скачков пинга в онлайн-играх."),
        },
        new RegistryTweak(
            Hklm(SystemProfile + @"\Tasks\Games", "GPU Priority", 8, 8),
            Hklm(SystemProfile + @"\Tasks\Games", "Priority", 6, 2),
            Hklm(SystemProfile + @"\Tasks\Games", "Scheduling Category", "High", "Medium", RegistryValueKind.String),
            Hklm(SystemProfile + @"\Tasks\Games", "SFIO Priority", "High", "Normal", RegistryValueKind.String))
        {
            Id = "game-priority",
            Title = Loc.T("Oyunlara yüksek CPU/GPU önceliği ver", "Give games high CPU/GPU priority", "Высокий приоритет CPU/GPU для игр"),
            Description = Loc.T("Windows'un çoklu ortam zamanlayıcısında oyunları en yüksek önceliğe alır.",
                "Puts games at the highest priority in the Windows multimedia scheduler.",
                "Ставит игры на наивысший приоритет в мультимедийном планировщике Windows."),
        },
        new RegistryTweak(NagleValues)
        {
            Id = "nagle",
            Title = Loc.T("Nagle algoritmasını kapat (düşük ping)",
                "Turn off Nagle's algorithm (lower ping)",
                "Отключить алгоритм Нейгла (ниже пинг)"),
            Description = Loc.T("Küçük ağ paketlerinin birleştirilip bekletilmesini önler. Özellikle TCP kullanan oyunlarda gecikmeyi düşürür.",
                "Keeps small network packets from being batched and held back. Lowers latency, especially in games that use TCP.",
                "Не даёт объединять и задерживать мелкие сетевые пакеты. Снижает задержку, особенно в играх на TCP."),
        },
        new RegistryTweak(
            Hkcu(@"Control Panel\Mouse", "MouseSpeed", "0", "1", RegistryValueKind.String),
            Hkcu(@"Control Panel\Mouse", "MouseThreshold1", "0", "6", RegistryValueKind.String),
            Hkcu(@"Control Panel\Mouse", "MouseThreshold2", "0", "10", RegistryValueKind.String))
        {
            Id = "mouse-acceleration",
            Title = Loc.T("Fare ivmesini kapat", "Turn off mouse acceleration", "Отключить ускорение мыши"),
            Description = Loc.T("\"İşaretçi hassasiyetini artır\" seçeneğini kapatır; nişan alırken fare hareketi her zaman aynı mesafeye gider.",
                "Turns off \"Enhance pointer precision\"; the same mouse movement always covers the same distance when you aim.",
                "Отключает «Повышенную точность установки указателя»; одно и то же движение мыши при прицеливании всегда даёт одинаковое смещение."),
            RequiresRestart = true,
        },
        new RegistryTweak(
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0))
        {
            Id = "startup-delay",
            Title = Loc.T("Açılıştaki başlangıç gecikmesini kaldır", "Remove the startup delay", "Убрать задержку автозагрузки"),
            Description = Loc.T("Windows'un oturum açtıktan sonra başlangıç uygulamalarını bekletmesini kaldırır.",
                "Stops Windows from holding back startup apps after you sign in.",
                "Windows перестаёт задерживать запуск программ автозагрузки после входа в систему."),
        },
        new ServiceTweak(4, "MapsBroker", "RetailDemo", "Fax", "WMPNetworkSvc")
        {
            Id = "unused-services",
            Title = Loc.T("Kullanılmayan servisleri kapat", "Turn off unused services", "Отключить неиспользуемые службы"),
            Description = Loc.T("Çevrimdışı Haritalar, Mağaza demo modu, Faks ve Media Player ağ paylaşımı servislerini kapatır.",
                "Turns off the Offline Maps, Store demo mode, Fax and Media Player network sharing services.",
                "Отключает службы автономных карт, демонстрационного режима Магазина, факса и сетевого доступа Media Player."),
        },
        new RegistryTweak(
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0),
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, 1))
        {
            Id = "background-apps",
            Title = Loc.T("Mağaza uygulamalarının arka planda çalışmasını engelle",
                "Stop Store apps from running in the background",
                "Запретить приложениям Магазина работать в фоне"),
            Description = Loc.T("Kullanmadığınız Mağaza uygulamalarının arka planda RAM ve CPU harcamasını durdurur. Bazı uygulamaların bildirimleri gecikebilir.",
                "Stops Store apps you don't use from eating RAM and CPU in the background. Some apps' notifications may be delayed.",
                "Неиспользуемые приложения Магазина перестают тратить память и процессор в фоне. Уведомления некоторых приложений могут задерживаться."),
            Risk = Risk.Moderate,
        },
        new RegistryTweak(
            Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2))
        {
            Id = "hags",
            Title = Loc.T("Donanım hızlandırmalı GPU zamanlamasını (HAGS) aç",
                "Turn on hardware-accelerated GPU scheduling (HAGS)",
                "Включить аппаратное планирование GPU (HAGS)"),
            Description = Loc.T("GPU'nun kendi belleğini yönetmesine izin verir; yeni ekran kartlarında gecikmeyi düşürür ve DLSS Frame Generation için gerekir. Eski kartlarda sorun çıkarabilir.",
                "Lets the GPU manage its own memory; lowers latency on newer graphics cards and is required for DLSS Frame Generation. May cause problems on older cards.",
                "Позволяет GPU самому управлять своей памятью; снижает задержку на новых видеокартах и нужен для DLSS Frame Generation. На старых картах может вызывать проблемы."),
            Risk = Risk.Moderate,
            Recommended = false,
            RequiresRestart = true,
        },
        new RegistryTweak(
            Hkcu(GameConfig, "GameDVR_FSEBehaviorMode", 2),
            Hkcu(GameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1),
            Hkcu(GameConfig, "GameDVR_FSEBehavior", 2),
            Hkcu(GameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1))
        {
            Id = "fullscreen-optimizations",
            Title = Loc.T("Tam ekran iyileştirmelerini kapat", "Turn off fullscreen optimizations", "Отключить оптимизацию во весь экран"),
            Description = Loc.T("Bazı eski DirectX oyunlarında gerçek tam ekran moduna geçerek giriş gecikmesini azaltır. Alt+Tab daha yavaş olabilir.",
                "Switches some older DirectX games to true exclusive fullscreen, reducing input lag. Alt+Tab may be slower.",
                "Переводит некоторые старые DirectX-игры в настоящий полноэкранный режим, снижая задержку ввода. Alt+Tab может работать медленнее."),
            Risk = Risk.Moderate,
            Recommended = false,
        },
        new RegistryTweak(
            Hklm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1))
        {
            Id = "power-throttling",
            Title = Loc.T("Güç kısıtlamasını (Power Throttling) kapat", "Turn off Power Throttling", "Отключить Power Throttling"),
            Description = Loc.T("Windows'un arka plandaki işlemleri yavaşlatmasını engeller. Dizüstü bilgisayarlarda pil daha çabuk biter.",
                "Stops Windows from slowing down background processes. Laptop batteries drain faster.",
                "Windows перестаёт замедлять фоновые процессы. На ноутбуках батарея садится быстрее."),
            Risk = Risk.Moderate,
            Recommended = false,
        },
        new RegistryTweak(
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1),
            Hkcu(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1", RegistryValueKind.String),
            Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, 1))
        {
            Id = "visual-effects",
            Title = Loc.T("Saydamlık ve animasyonları kapat", "Turn off transparency and animations", "Отключить прозрачность и анимации"),
            Description = Loc.T("Pencere animasyonlarını ve saydamlık efektini kapatır. Zayıf bilgisayarlarda masaüstünü hızlandırır.",
                "Turns off window animations and transparency effects. Speeds up the desktop on weaker PCs.",
                "Отключает анимацию окон и эффекты прозрачности. Ускоряет рабочий стол на слабых компьютерах."),
            Recommended = false,
            RequiresRestart = true,
        },
    ];

    /// <summary>Her bağlı ağ arayüzü için TcpAckFrequency=1 ve TCPNoDelay=1.</summary>
    private static IEnumerable<RegValue> NagleValues()
    {
        using var root = Reg.Base(RegistryHive.LocalMachine).OpenSubKey(TcpInterfaces);
        if (root is null) yield break;

        foreach (var guid in root.GetSubKeyNames())
        {
            using var iface = root.OpenSubKey(guid);
            var dhcp = iface?.GetValue("DhcpIPAddress") as string;
            var staticIp = (iface?.GetValue("IPAddress") as string[])?.FirstOrDefault();
            var hasAddress = IsUsable(dhcp) || IsUsable(staticIp);
            if (!hasAddress) continue;

            yield return Hklm($@"{TcpInterfaces}\{guid}", "TcpAckFrequency", 1);
            yield return Hklm($@"{TcpInterfaces}\{guid}", "TCPNoDelay", 1);
        }

        static bool IsUsable(string? ip) => !string.IsNullOrWhiteSpace(ip) && ip != "0.0.0.0";
    }

    /// <summary>"Nihai Performans" planının bir kopyasını oluşturup etkinleştirir.</summary>
    private static partial class PowerPlan
    {
        private const string Ours = "d0c6c1f2-7a3e-4b52-9b8e-4475634b494e";
        private const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        private const string BackupKey = "power|active";

        [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
        private static partial Regex GuidRegex();

        public static Tweak Tweak { get; } = new ActionTweak
        {
            Id = "power-plan",
            Title = Loc.T("Nihai Performans güç planını etkinleştir",
                "Enable the Ultimate Performance power plan",
                "Включить схему «Максимальная производительность»"),
            Description = Loc.T("İşlemcinin hız düşürmesini ve uykuya geçmesini engeller; oyunlarda ani FPS düşüşlerini azaltır. Dizüstünde pil daha çabuk biter.",
                "Keeps the CPU from slowing down or sleeping; reduces sudden FPS drops in games. Laptop batteries drain faster.",
                "Не даёт процессору снижать частоту и засыпать; меньше внезапных просадок FPS в играх. На ноутбуке батарея садится быстрее."),
            Check = () => string.Equals(GetActive(), Ours, StringComparison.OrdinalIgnoreCase),
            Apply = Apply,
            Revert = Revert,
        };

        private static string? GetActive()
        {
            var result = ProcessRunner.Run("powercfg.exe", "/getactivescheme");
            var match = GuidRegex().Match(result.Output);
            return match.Success ? match.Value.ToLowerInvariant() : null;
        }

        private static void Apply()
        {
            var active = GetActive();
            if (active is not null && active != Ours)
                BackupStore.Remember(BackupKey, () => new BackupStore.Entry { Exists = true, Data = active });

            if (!ProcessRunner.Run("powercfg.exe", $"/query {Ours}", logFailure: false).Success)
            {
                var created = ProcessRunner.Run("powercfg.exe", $"/duplicatescheme {Ultimate} {Ours}").Success
                              || ProcessRunner.Run("powercfg.exe", $"/duplicatescheme {HighPerformance} {Ours}").Success;
                if (!created)
                    throw new InvalidOperationException(Loc.T("Bu cihaz yüksek performans güç planlarını desteklemiyor (Modern Standby).",
                        "This device doesn't support high-performance power plans (Modern Standby).",
                        "Это устройство не поддерживает схемы питания высокой производительности (Modern Standby)."));
                var planName = Loc.T("DucKING Oyun Performansı", "DucKING Game Performance", "DucKING: игровая производительность");
                ProcessRunner.Run("powercfg.exe", $"/changename {Ours} \"{planName}\"");
            }

            if (!ProcessRunner.Run("powercfg.exe", $"/setactive {Ours}").Success)
                throw new InvalidOperationException(Loc.T("Güç planı etkinleştirilemedi.", "Couldn't activate the power plan.",
                    "Не удалось включить схему питания."));
        }

        private static void Revert()
        {
            var previous = BackupStore.TryGet(BackupKey, out var entry) ? entry.Data! : Balanced;
            if (!ProcessRunner.Run("powercfg.exe", $"/setactive {previous}").Success)
                ProcessRunner.Run("powercfg.exe", $"/setactive {Balanced}");
            ProcessRunner.Run("powercfg.exe", $"/delete {Ours}");
            BackupStore.Forget(BackupKey);
        }
    }
}
