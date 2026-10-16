using DucKING.Core;
using static DucKING.Core.RegValue;

namespace DucKING.Modules.Tweaks;

/// <summary>
/// Reklam, öneri, web araması ve telemetri ayarları.
/// Her ayar tek tek geri alınabilir; orijinal değerler BackupStore'da saklanır.
/// </summary>
public static class PrivacyTweaks
{
    private const string Cv = @"Software\Microsoft\Windows\CurrentVersion\";
    private const string Cdm = Cv + "ContentDeliveryManager";
    private const string Adv = Cv + @"Explorer\Advanced";

    private static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    public static IReadOnlyList<Tweak> Create() =>
    [
        new RegistryTweak(
            Hkcu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
            Hkcu(Cv + "Search", "BingSearchEnabled", 0),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0))
        {
            Id = "bing-search",
            Title = Loc.T("Başlat aramasında internet (Bing) sonuçlarını kapat",
                "Turn off web (Bing) results in Start search",
                "Отключить интернет-результаты (Bing) в поиске «Пуска»"),
            Description = Loc.T("Başlat menüsünde yazdığınız her harf internete gönderilmez; arama sadece bilgisayarınızda yapılır ve daha hızlı açılır.",
                "What you type in the Start menu is no longer sent to the internet; search stays on your PC and opens faster.",
                "Вводимый в меню «Пуск» текст больше не отправляется в интернет; поиск идёт только по компьютеру и открывается быстрее."),
        },
        new RegistryTweak(
            Hkcu(Adv, "Start_IrisRecommendations", 0, 1))
        {
            Id = "start-recommendations",
            Title = Loc.T("Başlat menüsündeki önerileri kapat", "Turn off Start menu recommendations", "Отключить рекомендации в меню «Пуск»"),
            Description = Loc.T("Başlat menüsündeki ipucu, kısayol ve uygulama önerilerini gizler.",
                "Hides tips, shortcuts and app suggestions in the Start menu.",
                "Скрывает советы, ярлыки и рекомендуемые приложения в меню «Пуск»."),
        },
        new RegistryTweak(
            Hkcu(Adv, "ShowSyncProviderNotifications", 0, 1))
        {
            Id = "explorer-ads",
            Title = Loc.T("Dosya Gezgini reklamlarını kapat", "Turn off File Explorer ads", "Отключить рекламу в Проводнике"),
            Description = Loc.T("Dosya Gezgini'nde çıkan OneDrive / Microsoft 365 bildirim ve reklamlarını gizler.",
                "Hides OneDrive / Microsoft 365 notifications and ads in File Explorer.",
                "Скрывает уведомления и рекламу OneDrive / Microsoft 365 в Проводнике."),
        },
        new RegistryTweak(
            Hkcu(Cdm, "RotatingLockScreenOverlayEnabled", 0, 1),
            Hkcu(Cdm, "SubscribedContent-338387Enabled", 0, 1))
        {
            Id = "lockscreen-tips",
            Title = Loc.T("Kilit ekranındaki ipuçlarını ve reklamları kapat",
                "Turn off lock screen tips and ads",
                "Отключить советы и рекламу на экране блокировки"),
            Description = Loc.T("Kilit ekranında çıkan \"eğlenceli bilgiler, ipuçları ve püf noktaları\" kutusunu kaldırır.",
                "Removes the \"fun facts, tips, tricks\" box from the lock screen.",
                "Убирает с экрана блокировки блок «интересные факты, советы и рекомендации»."),
        },
        new RegistryTweak(
            Hkcu(Cdm, "SubscribedContent-338393Enabled", 0, 1),
            Hkcu(Cdm, "SubscribedContent-353694Enabled", 0, 1),
            Hkcu(Cdm, "SubscribedContent-353696Enabled", 0, 1))
        {
            Id = "settings-suggestions",
            Title = Loc.T("Ayarlar uygulamasındaki önerileri kapat", "Turn off suggestions in Settings", "Отключить рекомендации в «Параметрах»"),
            Description = Loc.T("Ayarlar uygulamasında gösterilen önerilen içerikleri gizler.",
                "Hides the suggested content shown in the Settings app.",
                "Скрывает рекомендуемое содержимое в приложении «Параметры»."),
        },
        new RegistryTweak(
            Hkcu(Cdm, "SubscribedContent-338389Enabled", 0, 1))
        {
            Id = "windows-tips",
            Title = Loc.T("Windows ipucu ve öneri bildirimlerini kapat",
                "Turn off Windows tip and suggestion notifications",
                "Отключить уведомления с советами Windows"),
            Description = Loc.T("Windows kullanırken çıkan ipucu ve öneri bildirimlerini durdurur.",
                "Stops the tips and suggestions that pop up while you use Windows.",
                "Прекращает показ советов и предложений во время работы с Windows."),
        },
        new RegistryTweak(
            Hkcu(Cv + "UserProfileEngagement", "ScoobeSystemSettingEnabled", 0, 1),
            Hkcu(Cdm, "SubscribedContent-310093Enabled", 0, 1))
        {
            Id = "welcome-screens",
            Title = Loc.T("\"Kurulumu bitirin\" ve hoş geldiniz ekranlarını kapat",
                "Turn off \"Finish setup\" and welcome screens",
                "Отключить экраны приветствия и «Завершите настройку»"),
            Description = Loc.T("Güncellemelerden sonra açılan tam ekran tanıtım ve \"cihazınızı kurmayı bitirin\" sayfalarını engeller.",
                "Blocks the full-screen intros and \"let's finish setting up your device\" pages that open after updates.",
                "Блокирует полноэкранные презентации и страницы «Давайте завершим настройку устройства» после обновлений."),
        },
        new RegistryTweak(
            Hkcu(Cdm, "SilentInstalledAppsEnabled", 0, 1),
            Hkcu(Cdm, "SystemPaneSuggestionsEnabled", 0, 1),
            Hkcu(Cdm, "SoftLandingEnabled", 0, 1),
            Hkcu(Cdm, "SubscribedContent-338388Enabled", 0, 1),
            Hkcu(Cdm, "PreInstalledAppsEnabled", 0, 1),
            Hkcu(Cdm, "OemPreInstalledAppsEnabled", 0, 1),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1))
        {
            Id = "silent-app-installs",
            Title = Loc.T("İstenmeyen uygulamaların otomatik yüklenmesini engelle",
                "Block automatic installs of unwanted apps",
                "Запретить автоустановку ненужных приложений"),
            Description = Loc.T("Windows'un Candy Crush, TikTok gibi \"önerilen\" uygulamaları kendiliğinden yüklemesini durdurur.",
                "Stops Windows from installing \"suggested\" apps like Candy Crush and TikTok on its own.",
                "Не даёт Windows самостоятельно устанавливать «рекомендуемые» приложения вроде Candy Crush и TikTok."),
        },
        new RegistryTweak(
            Hkcu(Cv + "AdvertisingInfo", "Enabled", 0, 1))
        {
            Id = "advertising-id",
            Title = Loc.T("Reklam kimliğini kapat", "Turn off advertising ID", "Отключить рекламный идентификатор"),
            Description = Loc.T("Uygulamaların size özel reklam göstermek için kullandığı reklam kimliğini kapatır.",
                "Turns off the advertising ID that apps use to show you personalized ads.",
                "Отключает рекламный идентификатор, по которому приложения показывают персональную рекламу."),
        },
        new RegistryTweak(
            Hkcu(Cv + "Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1))
        {
            Id = "tailored-experiences",
            Title = Loc.T("Kişiye özel deneyimleri kapat", "Turn off tailored experiences", "Отключить персонализированные возможности"),
            Description = Loc.T("Microsoft'un tanılama verilerinizle size özel ipucu ve reklam göstermesini engeller.",
                "Stops Microsoft from using your diagnostic data to show you personalized tips and ads.",
                "Не даёт Microsoft использовать ваши диагностические данные для персональных советов и рекламы."),
        },
        new RegistryTweak(
            Hkcu(Cv + @"Notifications\Settings\Windows.ActionCenter.SmartOptOut", "Enabled", 0, 1))
        {
            Id = "smart-optout",
            Title = Loc.T("\"Bu uygulamanın bildirimlerini kapatalım mı?\" sorusunu kapat",
                "Turn off the \"Turn off notifications for this app?\" prompt",
                "Отключить вопрос «Отключить уведомления этого приложения?»"),
            Description = Loc.T("Windows'un bir süredir açmadığınız uygulamaların bildirimleri için sürekli sormasını engeller.",
                "Stops Windows from repeatedly asking about notifications from apps you haven't opened in a while.",
                "Windows перестанет постоянно спрашивать об уведомлениях приложений, которые вы давно не открывали."),
        },
        new RegistryTweak(
            Hkcu(@"Software\Policies\Microsoft\Edge", "WebWidgetAllowed", 0))
        {
            Id = "edge-widget",
            Title = Loc.T("Edge masaüstü arama çubuğunu kapat",
                "Turn off the Edge desktop search bar",
                "Отключить панель поиска Edge на рабочем столе"),
            Description = Loc.T("Masaüstünde açılabilen Edge arama/haber widget'ını devre dışı bırakır.",
                "Disables the Edge search/news widget that can appear on the desktop.",
                "Отключает виджет поиска и новостей Edge, который может появляться на рабочем столе."),
        },
        // Windows 11'de Widget ilkesi UCPD sürücüsüyle korunuyor (yönetici bile yazamıyor); orada
        // Widget'lar "Gereksiz Uygulamalar" sayfasından Web Experience paketi kaldırılarak kapatılır.
        .. (IsWindows11 ? Array.Empty<Tweak>() : new Tweak[]
        {
            new RegistryTweak(
                Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0))
            {
                Id = "news-feed",
                Title = Loc.T("Görev çubuğundaki Haberler ve İlgi Alanları'nı kapat",
                    "Turn off News and Interests on the taskbar",
                    "Отключить «Новости и интересы» на панели задач"),
                Description = Loc.T("Arka planda sürekli internetten haber/hava durumu çeken paneli kapatır. RAM ve internet kullanımını azaltır.",
                    "Turns off the panel that keeps pulling news and weather from the internet in the background. Reduces RAM and data use.",
                    "Отключает панель, которая постоянно загружает новости и погоду в фоне. Снижает расход памяти и трафика."),
            },
        }),
        new RegistryTweak(
            Hkcu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
            Hkcu(Adv, "ShowCopilotButton", 0))
        {
            Id = "copilot",
            Title = Loc.T("Copilot'u kapat", "Turn off Copilot", "Отключить Copilot"),
            Description = Loc.T("Görev çubuğundaki Copilot düğmesini ve Windows Copilot'u devre dışı bırakır.",
                "Disables the Copilot taskbar button and Windows Copilot.",
                "Отключает кнопку Copilot на панели задач и Windows Copilot."),
        },
        new RegistryTweak(
            Hkcu(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1))
        {
            Id = "recall",
            Title = Loc.T("Recall (ekran görüntüsü kaydı) özelliğini kapat",
                "Turn off Recall (screen snapshots)",
                "Отключить Recall (запись снимков экрана)"),
            Description = Loc.T("Copilot+ bilgisayarlarda ekranınızın sürekli görüntüsünü alan Recall'u kapatır.",
                "Turns off Recall, which keeps taking snapshots of your screen on Copilot+ PCs.",
                "Отключает Recall, который на компьютерах Copilot+ постоянно делает снимки экрана."),
        },
        new RegistryTweak(
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
            Hkcu(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0))
        {
            Id = "telemetry-policy",
            Title = Loc.T("Telemetriyi en aza indir", "Minimize telemetry", "Свести телеметрию к минимуму"),
            Description = Loc.T("Microsoft'a gönderilen tanılama verisini en düşük seviyeye çeker ve geri bildirim isteklerini kapatır.",
                "Lowers the diagnostic data sent to Microsoft to the minimum and turns off feedback requests.",
                "Снижает объём диагностических данных для Microsoft до минимума и отключает запросы отзывов."),
        },
        new ServiceTweak(4, "DiagTrack", "dmwappushservice")
        {
            Id = "telemetry-services",
            Title = Loc.T("Telemetri servislerini kapat", "Turn off telemetry services", "Отключить службы телеметрии"),
            Description = Loc.T("Arka planda sürekli veri toplayıp gönderen \"Bağlı Kullanıcı Deneyimleri ve Telemetri\" servislerini durdurur.",
                "Stops the \"Connected User Experiences and Telemetry\" services that keep collecting and sending data in the background.",
                "Останавливает службы «Функциональные возможности для подключённых пользователей и телеметрия», которые постоянно собирают и отправляют данные."),
        },
        new RegistryTweak(
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0))
        {
            Id = "activity-history",
            Title = Loc.T("Etkinlik geçmişini kapat", "Turn off activity history", "Отключить журнал действий"),
            Description = Loc.T("Açtığınız uygulama ve dosyaların geçmişinin toplanmasını durdurur.",
                "Stops collecting a history of the apps and files you open.",
                "Прекращает сбор истории открытых приложений и файлов."),
        },
        new RegistryTweak(
            Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0))
        {
            Id = "cortana",
            Title = Loc.T("Cortana'yı kapat", "Turn off Cortana", "Отключить Cortana"),
            Description = Loc.T("Cortana'nın arka planda çalışmasını engeller.",
                "Prevents Cortana from running in the background.",
                "Не даёт Cortana работать в фоне."),
        },
    ];
}
