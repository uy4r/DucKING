using System.Text.Json;
using DucKING.Core;

namespace DucKING.Modules.Apps;

public sealed class BloatApp : ObservableObject
{
    private bool _isSelected;

    public required string Package { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool Recommended { get; init; } = true;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

/// <summary>
/// Windows ile önceden yüklü gelen gereksiz Mağaza uygulamaları. Xbox uygulamaları
/// oyuncular için gerekli olduğundan bilerek listeye alınmadı.
/// </summary>
public static class BloatwareService
{
    private static readonly BloatApp[] Catalog =
    [
        new()
        {
            Package = "Microsoft.BingNews",
            Name = Loc.T("Haberler", "News", "Новости"),
            Description = Loc.T("Arka planda haber indirir.", "Downloads news in the background.", "Загружает новости в фоне."),
        },
        new()
        {
            Package = "Microsoft.BingWeather",
            Name = Loc.T("Hava Durumu", "Weather", "Погода"),
            Description = Loc.T("Arka planda hava durumu indirir.", "Downloads weather in the background.", "Загружает погоду в фоне."),
        },
        new()
        {
            Package = "Microsoft.BingSearch",
            Name = Loc.T("Bing Arama", "Bing Search", "Поиск Bing"),
            Description = Loc.T("Windows araması için Bing bileşeni.", "Bing component for Windows search.", "Компонент Bing для поиска Windows."),
        },
        new()
        {
            Package = "MicrosoftWindows.Client.WebExperience",
            Name = Loc.T("Widget'lar", "Widgets", "Мини-приложения"),
            Description = Loc.T("Görev çubuğundaki haber ve hava durumu paneli; arka planda sürekli internete bağlanır.",
                "The news and weather panel on the taskbar; keeps connecting to the internet in the background.",
                "Панель новостей и погоды на панели задач; постоянно подключается к интернету в фоне."),
        },
        new()
        {
            Package = "Clipchamp.Clipchamp",
            Name = "Clipchamp",
            Description = Loc.T("Video düzenleyici.", "Video editor.", "Видеоредактор."),
        },
        new()
        {
            Package = "Microsoft.GetHelp",
            Name = Loc.T("Yardım Alın", "Get Help", "Техническая поддержка"),
            Description = Loc.T("Microsoft destek uygulaması.", "Microsoft support app.", "Приложение поддержки Microsoft."),
        },
        new()
        {
            Package = "Microsoft.Getstarted",
            Name = Loc.T("İpuçları", "Tips", "Советы"),
            Description = Loc.T("Windows tanıtım uygulaması.", "Windows introduction app.", "Приложение-знакомство с Windows."),
        },
        new()
        {
            Package = "Microsoft.MicrosoftSolitaireCollection",
            Name = "Solitaire Collection",
            Description = Loc.T("Reklamlı kart oyunları.", "Card games with ads.", "Карточные игры с рекламой."),
        },
        new()
        {
            Package = "Microsoft.People",
            Name = Loc.T("Kişiler", "People", "Люди"),
            Description = Loc.T("Eski kişiler uygulaması.", "Old contacts app.", "Старое приложение контактов."),
        },
        new()
        {
            Package = "Microsoft.PowerAutomateDesktop",
            Name = "Power Automate",
            Description = Loc.T("Otomasyon aracı.", "Automation tool.", "Средство автоматизации."),
        },
        new()
        {
            Package = "Microsoft.Todos",
            Name = "Microsoft To Do",
            Description = Loc.T("Yapılacaklar listesi.", "To-do list.", "Список дел."),
        },
        new()
        {
            Package = "Microsoft.WindowsFeedbackHub",
            Name = Loc.T("Geri Bildirim Merkezi", "Feedback Hub", "Центр отзывов"),
            Description = Loc.T("Microsoft'a geri bildirim gönderir.", "Sends feedback to Microsoft.", "Отправляет отзывы в Microsoft."),
        },
        new()
        {
            Package = "Microsoft.WindowsMaps",
            Name = Loc.T("Haritalar", "Maps", "Карты"),
            Description = Loc.T("Çevrimdışı haritalar.", "Offline maps.", "Офлайн-карты."),
        },
        new()
        {
            Package = "Microsoft.MicrosoftOfficeHub",
            Name = "Microsoft 365 (Office)",
            Description = Loc.T("Office reklam/başlatıcı uygulaması.", "Office promo/launcher app.", "Рекламное приложение-лаунчер Office."),
        },
        new()
        {
            Package = "MicrosoftTeams",
            Name = Loc.T("Teams (kişisel, eski)", "Teams (personal, legacy)", "Teams (личный, старый)"),
            Description = Loc.T("Açılışta otomatik başlar.", "Starts automatically at boot.", "Запускается автоматически при включении."),
        },
        new()
        {
            Package = "MSTeams",
            Name = Loc.T("Teams (kişisel)", "Teams (personal)", "Teams (личный)"),
            Description = Loc.T("Açılışta otomatik başlar.", "Starts automatically at boot.", "Запускается автоматически при включении."),
        },
        new()
        {
            Package = "Microsoft.549981C3F5F10",
            Name = "Cortana",
            Description = Loc.T("Sesli asistan.", "Voice assistant.", "Голосовой помощник."),
        },
        new()
        {
            Package = "Microsoft.Copilot",
            Name = "Copilot",
            Description = Loc.T("Yapay zekâ asistanı.", "AI assistant.", "ИИ-помощник."),
        },
        new()
        {
            Package = "Microsoft.ZuneVideo",
            Name = Loc.T("Filmler ve TV", "Movies & TV", "Кино и ТВ"),
            Description = Loc.T("Eski video oynatıcı.", "Old video player.", "Старый видеоплеер."),
        },
        new()
        {
            Package = "Microsoft.MixedReality.Portal",
            Name = Loc.T("Karma Gerçeklik Portalı", "Mixed Reality Portal", "Портал смешанной реальности"),
            Description = Loc.T("VR başlığı yoksa gereksiz.", "Not needed without a VR headset.", "Не нужен без VR-гарнитуры."),
        },
        new()
        {
            Package = "Microsoft.Microsoft3DViewer",
            Name = Loc.T("3D Görüntüleyici", "3D Viewer", "Средство 3D-просмотра"),
            Description = Loc.T("3D model görüntüleyici.", "3D model viewer.", "Просмотр 3D-моделей."),
        },
        new()
        {
            Package = "Microsoft.SkypeApp",
            Name = "Skype",
            Description = Loc.T("Kapatılan Skype uygulaması.", "The discontinued Skype app.", "Закрытое приложение Skype."),
        },
        new()
        {
            Package = "Microsoft.Windows.DevHome",
            Name = "Dev Home",
            Description = Loc.T("Geliştirici paneli.", "Developer dashboard.", "Панель разработчика."),
        },
        new()
        {
            Package = "Microsoft.YourPhone",
            Name = Loc.T("Telefon Bağlantısı", "Phone Link", "Связь с телефоном"),
            Description = Loc.T("Telefonu bilgisayara bağlar. Kullanıyorsanız kaldırmayın.",
                "Connects your phone to your PC. Keep it if you use it.",
                "Связывает телефон с компьютером. Не удаляйте, если пользуетесь."),
            Recommended = false,
        },
        new()
        {
            Package = "Microsoft.OutlookForWindows",
            Name = Loc.T("Yeni Outlook", "New Outlook", "Новый Outlook"),
            Description = Loc.T("E-posta uygulaması. Kullanıyorsanız kaldırmayın.",
                "Email app. Keep it if you use it.",
                "Почтовое приложение. Не удаляйте, если пользуетесь."),
            Recommended = false,
        },
    ];

    /// <summary>Katalogdan bu bilgisayarda yüklü olanları döner.</summary>
    public static IReadOnlyList<BloatApp> GetInstalled()
    {
        var result = ProcessRunner.PowerShell("Get-AppxPackage | Select-Object -ExpandProperty Name | ConvertTo-Json -Compress");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return [];

        var installed = result.Output.TrimStart().StartsWith('[')
            ? JsonSerializer.Deserialize<string[]>(result.Output) ?? []
            : [JsonSerializer.Deserialize<string>(result.Output) ?? ""];
        var set = installed.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Catalog
            .Where(app => set.Contains(app.Package))
            .Select(app => new BloatApp
            {
                Package = app.Package,
                Name = app.Name,
                Description = app.Description,
                Recommended = app.Recommended,
                IsSelected = app.Recommended,
            })
            .ToList();
    }

    /// <summary>Uygulamayı kullanıcıdan kaldırır ve yeni kullanıcılara yüklenmemesi için hazır paketten de siler.</summary>
    public static void Remove(BloatApp app)
    {
        var name = ProcessRunner.PsQuote(app.Package);
        var result = ProcessRunner.PowerShell($$"""
            Get-AppxPackage -Name {{name}} | Remove-AppxPackage
            Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq {{name}} | Remove-AppxProvisionedPackage -Online | Out-Null
            """, timeoutMs: 180_000);
        if (!result.Success)
            throw new InvalidOperationException(result.Output.Split('\n').FirstOrDefault()?.Trim());
        Log.Info($"Uygulama kaldırıldı: {app.Package}");
    }
}
