namespace DucKING.Core;

/// <summary>
/// XAML'deki sabit metinlerin üç dildeki karşılıkları (<c>{core:Loc anahtar}</c>). Koddaki metinler bu tabloya
/// girmez; yerinde <see cref="Loc.T"/> ile yazılır. Birden çok yerde geçen metinler kodda da
/// <see cref="Get"/> ile buradan alınabilir.
/// </summary>
public static class UiText
{
    public static string Get(string key)
    {
        if (Table.TryGetValue(key, out var text)) return Loc.T(text.Tr, text.En, text.Ru);
        Log.Error($"Çevirisi olmayan metin anahtarı: {key}");
        return key;
    }

    private static readonly Dictionary<string, (string Tr, string En, string Ru)> Table = new()
    {
        // ---- Üst çubuk ve menüler ----
        ["main.homeName"] = ("DucKING ana sayfa", "DucKING home", "Главная DucKING"),
        ["main.tagline"] = ("oyuncular için ücretsiz", "free for gamers", "бесплатно для геймеров"),
        ["main.admin"] = ("Yönetici", "Administrator", "Администратор"),
        ["main.about"] = ("Hakkında", "About", "О программе"),
        ["main.language"] = ("Dil", "Language", "Язык"),
        ["main.gpuTemp"] = ("Ekran kartı sıcaklığı", "GPU temperature", "Температура видеокарты"),
        ["main.systemBadge"] = ("Uygulanan performans ve gizlilik ayarları", "Applied performance and privacy tweaks",
            "Применённые настройки производительности и конфиденциальности"),

        ["nav.home"] = ("Ana sayfa", "Home", "Главная"),
        ["nav.games"] = ("Oyunlar", "Games", "Игры"),
        ["nav.hardware"] = ("Donanım", "Hardware", "Оборудование"),
        ["nav.network"] = ("Ağ", "Network", "Сеть"),
        ["nav.system"] = ("Sistem", "System", "Система"),
        ["nav.dns"] = ("Oyuncu DNS", "Gaming DNS", "Игровой DNS"),
        ["nav.dpi"] = ("DPI engel aşma", "DPI bypass", "Обход DPI"),
        ["nav.performance"] = ("Performans", "Performance", "Производительность"),
        ["nav.privacy"] = ("Reklam ve gizlilik", "Ads and privacy", "Реклама и конфиденциальность"),
        ["nav.startup"] = ("Başlangıç uygulamaları", "Startup apps", "Автозагрузка"),
        ["nav.apps"] = ("Gereksiz uygulamalar", "Bloatware", "Ненужные приложения"),

        ["unit.ms"] = ("ms", "ms", "мс"),

        // ---- Ana sayfa ----
        ["home.editHint"] = ("Kutuları sürükleyerek yer değiştir, köşedeki düğmelerle boyutunu seç ya da kaldır.",
            "Drag tiles to rearrange them; use the corner buttons to resize or remove them.",
            "Перетаскивайте плитки, чтобы поменять их местами; кнопки в углу меняют размер или убирают плитку."),
        ["home.addWidget"] = ("Widget ekle", "Add widget", "Добавить виджет"),
        ["home.resetLayout"] = ("Varsayılana dön", "Reset to default", "Сбросить"),
        ["home.editName"] = ("Ana sayfayı düzenle", "Edit home page", "Изменить главную"),
        ["home.edit"] = ("Düzenle", "Edit", "Изменить"),
        ["home.done"] = ("Bitti", "Done", "Готово"),
        ["home.applyFastest"] = ("En hızlıyı uygula", "Use the fastest", "Выбрать быстрейший"),
        ["home.dpiFooter"] = ("Discord ve engelli siteler →", "Discord and blocked sites →", "Discord и заблокированные сайты →"),
        ["home.oneClick"] = ("Tek tıkla oyuncu modu", "One-click gamer mode", "Игровой режим в один клик"),
        ["home.oneClickDetail"] = ("Önerilen tüm ayarları uygular", "Applies all recommended tweaks", "Применяет все рекомендуемые настройки"),
        ["home.gpu"] = ("Ekran kartı", "Graphics card", "Видеокарта"),
        ["home.hardwareFooter"] = ("Donanım ayrıntıları →", "Hardware details →", "Подробнее об оборудовании →"),
        ["home.gamesFooter"] = ("Grafik profilleri →", "Graphics profiles →", "Графические профили →"),
        ["home.perfDetail"] = ("FPS, gecikme ve güç ayarları", "FPS, latency and power settings", "FPS, задержка и электропитание"),
        ["home.privacyDetail"] = ("Bing araması, reklamlar, telemetri", "Bing search, ads, telemetry", "Поиск Bing, реклама, телеметрия"),
        ["home.ping"] = ("Ping", "Ping", "Пинг"),
        ["home.measuring"] = ("ölçülüyor…", "measuring…", "измерение…"),
        ["home.cpu"] = ("İşlemci", "CPU", "Процессор"),
        ["home.ram"] = ("Bellek", "Memory", "Память"),
        ["home.system"] = ("Sistem (işlemci, bellek)", "System (CPU, memory)", "Система (процессор, память)"),
        ["home.startup"] = ("Açılışta çalışan", "Runs at startup", "В автозагрузке"),
        ["home.startupFooter"] = ("Gereksizleri kapat →", "Turn off the unneeded →", "Отключить лишнее →"),
        ["home.appsFooter"] = ("Tara ve kaldır →", "Scan and remove →", "Найти и удалить →"),
        ["home.appsDetail"] = ("Haberler, Clipchamp, Teams ve diğer önceden yüklü uygulamalar",
            "News, Clipchamp, Teams and other preinstalled apps",
            "Новости, Clipchamp, Teams и другие предустановленные приложения"),

        // ---- Ayar kartı (Themes/Dark.xaml) ----
        ["tweak.caution"] = ("Dikkat", "Caution", "Осторожно"),
        ["tweak.recommended"] = ("Önerilen", "Recommended", "Рекомендуется"),
        ["tweak.restart"] = ("Yeniden başlatma gerekir", "Restart required", "Нужна перезагрузка"),

        // ---- Ortak ----
        ["common.apply"] = ("Uygula", "Apply", "Применить"),

        // ---- DNS ----
        ["dns.subtitle"] = ("İnternet sağlayıcınızın yavaş DNS'i yerine hızlı bir DNS kullanın. Hız testi, bağlantınıza en hızlı yanıt veren sunucuyu bulur. Şifreli DNS (DoH), sağlayıcınızın hangi sitelere girdiğinizi görmesini ve DNS engellerini önler.",
            "Use a fast DNS instead of your ISP's slow one. The speed test finds the server that answers fastest on your connection. Encrypted DNS (DoH) stops your ISP from seeing which sites you visit and gets around DNS blocks.",
            "Используйте быстрый DNS вместо медленного DNS провайдера. Тест скорости находит сервер, который быстрее всех отвечает на вашем подключении. Зашифрованный DNS (DoH) скрывает от провайдера, какие сайты вы посещаете, и обходит DNS-блокировки."),
        ["dns.current"] = ("Şu anki DNS", "Current DNS", "Текущий DNS"),
        ["dns.useDoh"] = ("Şifreli DNS (DoH) kullan", "Use encrypted DNS (DoH)", "Зашифрованный DNS (DoH)"),
        ["dns.restore"] = ("Otomatiğe döndür", "Reset to automatic", "Вернуть автоматический"),
        ["dns.test"] = ("Hız testi yap", "Run speed test", "Проверить скорость"),
        ["dns.providers"] = ("DNS sağlayıcıları", "DNS providers", "DNS-провайдеры"),
        ["dns.inUse"] = ("Kullanımda", "In use", "Используется"),
        ["dns.fastest"] = ("En hızlı", "Fastest", "Самый быстрый"),

        ["common.remove"] = ("Kaldır", "Remove", "Удалить"),

        // ---- DPI ----
        ["dpi.title"] = ("DPI Engel Aşma", "DPI Bypass", "Обход DPI"),
        ["dpi.subtitle"] = ("İnternet sağlayıcınızın Discord, Roblox gibi platformlara koyduğu DPI (derin paket inceleme) engellerini aşar. Seçtiğiniz araç Windows servisi olarak kurulur ve bilgisayar her açıldığında kendiliğinden çalışır.",
            "Gets around the DPI (deep packet inspection) blocks your ISP puts on platforms like Discord and Roblox. The tool you choose is installed as a Windows service and starts on its own every time your PC boots.",
            "Обходит DPI-блокировки (глубокая инспекция пакетов), которые провайдер ставит на Discord, Roblox и другие платформы. Выбранный инструмент устанавливается как служба Windows и запускается сам при каждом включении компьютера."),
        ["dpi.dnsWarnTitle"] = ("DNS'iniz Discord'u engelliyor", "Your DNS is blocking Discord", "Ваш DNS блокирует Discord"),
        ["dpi.dnsWarnText"] = ("İnternet sağlayıcınızın DNS'i Discord için sahte adres veriyor. DPI aracı bu durumda tek başına işe yaramaz; şifreli güvenli DNS'e geçin.",
            "Your ISP's DNS returns a fake address for Discord. A DPI tool alone won't help in this case; switch to a secure encrypted DNS.",
            "DNS вашего провайдера выдаёт для Discord поддельный адрес. В этом случае одного инструмента обхода DPI мало — перейдите на защищённый зашифрованный DNS."),
        ["dpi.fixDns"] = ("Güvenli DNS'e geç", "Switch to secure DNS", "Перейти на защищённый DNS"),
        ["dpi.step1"] = ("1. Araç seçin", "1. Choose a tool", "1. Выберите инструмент"),
        ["dpi.step2"] = ("2. Ayar seçin", "2. Choose a preset", "2. Выберите пресет"),
        ["dpi.editList"] = ("Listeyi düzenle", "Edit list", "Изменить список"),
        ["dpi.russiaNote"] = ("Rusya'da engelleme merkezi olarak yapılır ve sık değişir; bir ayar çalışmazsa diğerlerini deneyin. Sağlayıcınız listede yoksa \"Genel\" ile başlayın. Mobil internetteki beyaz liste kesintileri bu araçlarla aşılamaz.",
            "In Russia, blocking is centralized and changes often; if a preset doesn't work, try the others. If your ISP isn't listed, start with \"General\". Whitelist-only mobile internet can't be bypassed with these tools.",
            "В России блокировки централизованы и часто меняются: если пресет не помогает, попробуйте другие. Если вашего провайдера нет в списке, начните с «Общий». Мобильный интернет в режиме белых списков этими средствами не обойти."),
        ["dpi.dnsFixCheck"] = ("DNS engeli varsa şifreli güvenli DNS'e de geç (önerilen)",
            "If DNS is blocked, also switch to a secure encrypted DNS (recommended)",
            "При блокировке DNS также перейти на защищённый зашифрованный DNS (рекомендуется)"),
        ["dpi.install"] = ("Kur ve başlat", "Install and start", "Установить и запустить"),
        ["dpi.installing"] = ("Kuruluyor…", "Installing…", "Установка…"),
        ["dpi.notesTitle"] = ("Bilmeniz gerekenler", "Good to know", "Что нужно знать"),
        ["dpi.notes"] = (
            "• Aynı anda yalnızca bir araç çalışır; yeni araç kurulunca eskisi (başka programlarla kurulanlar dahil) kaldırılır.\n" +
            "• Bir ayar işe yaramazsa sıradaki alternatifi deneyin. zapret'te kendi internet sağlayıcınızı seçin.\n" +
            "• GoodbyeDPI ve zapret, WinDivert sürücüsünü kullanır. Bazı antivirüs programları bunu yanlışlıkla tehdit olarak algılayabilir.\n" +
            "• ByeDPI ilk kurulumda Windows Packet Filter sürücüsünü ve Visual C++ çalışma zamanını kurar; kurulumdan sonra Discord'u kapatıp yeniden açın.\n" +
            "• Bu araçlar oyun trafiğinizi yavaşlatmaz; zapret'te oyun siteleri varsayılan olarak hariç tutulur.",
            "• Only one tool runs at a time; installing a new one removes the old one (including ones installed by other programs).\n" +
            "• If a preset doesn't work, try the next alternative. For zapret, pick your own ISP.\n" +
            "• GoodbyeDPI and zapret use the WinDivert driver. Some antivirus programs may wrongly flag it as a threat.\n" +
            "• On first install, ByeDPI installs the Windows Packet Filter driver and the Visual C++ runtime; close and reopen Discord afterwards.\n" +
            "• These tools don't slow down your game traffic; zapret excludes game sites by default.",
            "• Одновременно работает только один инструмент; при установке нового старый удаляется (включая установленные другими программами).\n" +
            "• Если пресет не помогает, попробуйте следующий альтернативный. Для zapret выберите своего провайдера.\n" +
            "• GoodbyeDPI и zapret используют драйвер WinDivert. Некоторые антивирусы могут ошибочно считать его угрозой.\n" +
            "• При первой установке ByeDPI ставит драйвер Windows Packet Filter и среду выполнения Visual C++; после этого закройте и снова откройте Discord.\n" +
            "• Эти инструменты не замедляют игровой трафик; в zapret игровые сайты по умолчанию исключены."),

        // ---- Discord yeniden başlatma ----
        ["discord.title"] = ("Discord otomatik yeniden başlatılır", "Discord restarts automatically", "Discord перезапускается автоматически"),
        ["discord.text"] = ("DNS veya DPI ayarı uygulanırken, kaldırılırken ya da yeniden kurulurken Discord açıksa önce kapatılır, işlem bitince yeniden açılır. Böylece yeni ayar Discord'a hemen uygulanır.",
            "When a DNS or DPI setting is applied, removed or reinstalled, an open Discord is closed first and reopened when the operation finishes, so the new setting reaches Discord right away.",
            "Когда настройка DNS или DPI применяется, удаляется или переустанавливается, открытый Discord сначала закрывается, а по окончании операции открывается снова — так новая настройка сразу действует в Discord."),
        ["discord.toggle"] = ("Discord'u otomatik yeniden başlat", "Restart Discord automatically", "Автоматически перезапускать Discord"),

        ["common.refresh"] = ("Yenile", "Refresh", "Обновить"),

        // ---- Performans / gizlilik sayfaları ----
        ["tweaks.revertAll"] = ("Tümünü geri al", "Revert all", "Отменить все"),
        ["tweaks.applyRecommended"] = ("Önerilenleri uygula", "Apply recommended", "Применить рекомендуемые"),

        // ---- Başlangıç uygulamaları ----
        ["startup.title"] = ("Başlangıç Uygulamaları", "Startup Apps", "Автозагрузка"),
        ["startup.subtitle"] = ("Bilgisayar açılırken kendiliğinden başlayan programlar. Gereksiz olanları kapatmak açılışı hızlandırır ve oyun sırasında RAM/CPU'yu boşaltır. Programlar silinmez; istediğiniz zaman tekrar açabilirsiniz.",
            "Programs that start on their own when your PC boots. Turning off the unneeded ones speeds up startup and frees RAM/CPU while you play. Nothing is deleted; you can turn them back on any time.",
            "Программы, которые запускаются сами при включении компьютера. Отключение лишних ускоряет загрузку и освобождает память и процессор во время игры. Программы не удаляются — их можно включить обратно в любой момент."),
        ["startup.importantTip"] = ("Güvenlik, ses sürücüsü veya oyun anti-cheat bileşeni. Kapatırsanız bazı oyunlar ya da özellikler çalışmayabilir.",
            "Security, audio driver or game anti-cheat component. If you turn it off, some games or features may stop working.",
            "Компонент безопасности, аудиодрайвера или античита. Если его отключить, некоторые игры или функции могут перестать работать."),
        ["startup.important"] = ("Kapatmayın", "Keep on", "Не отключать"),

        // ---- Gereksiz uygulamalar ----
        ["apps.title"] = ("Gereksiz Uygulamalar", "Bloatware", "Ненужные приложения"),
        ["apps.subtitle"] = ("Windows ile birlikte gelen, arka planda internete bağlanıp kaynak harcayan uygulamalar. Xbox uygulamalarına dokunulmaz. Kaldırdığınız uygulamaları istediğiniz zaman Microsoft Store'dan tekrar yükleyebilirsiniz.",
            "Apps that ship with Windows and connect to the internet and use resources in the background. Xbox apps are left alone. You can reinstall anything you remove from the Microsoft Store at any time.",
            "Приложения, которые поставляются с Windows, подключаются к интернету и тратят ресурсы в фоне. Приложения Xbox не затрагиваются. Удалённое можно в любой момент снова установить из Microsoft Store."),
        ["apps.removeSelected"] = ("Seçilenleri kaldır", "Remove selected", "Удалить выбранные"),

        // ---- Hakkında ----
        ["about.subtitle"] = ("DucKING ücretsizdir ve öyle kalacak. Aşağıdaki açık kaynak projelerin üzerine kurulmuştur; emeği geçenlere teşekkürler.",
            "DucKING is free and always will be. It's built on the open-source projects below — thanks to everyone who made them.",
            "DucKING бесплатен и останется таким. Он построен на открытых проектах ниже — спасибо всем, кто над ними работал."),
        ["about.troubleshooting"] = ("Sorun giderme", "Troubleshooting", "Устранение неполадок"),
        ["about.openLog"] = ("Günlük dosyasını aç", "Open log file", "Открыть журнал"),
        ["about.openData"] = ("Veri klasörünü aç", "Open data folder", "Открыть папку данных"),
        ["about.licenses"] = ("Lisansları göster", "Show licenses", "Показать лицензии"),
        ["about.checkUpdates"] = ("Güncellemeleri denetle", "Check for updates", "Проверить обновления"),
        ["about.openRelease"] = ("Sürüm sayfasını aç", "Open release page", "Открыть страницу выпуска"),
        ["about.diagnostics"] = ("Tanılama raporu oluştur", "Create diagnostics report", "Создать отчёт диагностики"),
        ["about.diagnosticsNote"] = ("Tanılama raporu Masaüstüne zip olarak kaydedilir: DucKING günlüğü ve sistem bilgileri (DucKING ve Windows sürümü, arayüz dili, DPI ve DNS durumu). Kullanıcı adınız, bilgisayar adınız ve profil klasörünüz rapordan gizlenir; dosya hiçbir yere gönderilmez. Bir sorun bildirirken kendiniz ekleyebilirsiniz.",
            "The diagnostics report is saved to your Desktop as a zip: the DucKING log and system info (DucKING and Windows version, UI language, DPI and DNS status). Your user name, computer name and profile folder are masked in it; the file is never sent anywhere. Attach it yourself when you report a problem.",
            "Отчёт диагностики сохраняется на рабочий стол в виде zip-архива: журнал DucKING и сведения о системе (версии DucKING и Windows, язык интерфейса, состояние DPI и DNS). Имя пользователя, имя компьютера и папка профиля в нём скрыты; файл никуда не отправляется. Если сообщаете о проблеме, приложите его сами."),
        ["about.backupNote"] = (@"Değiştirilen her ayarın orijinal değeri C:\ProgramData\DucKING\backup.json dosyasında saklanır. Bir ayarı kapattığınızda bu değer geri yüklenir.",
            @"The original value of every changed setting is kept in C:\ProgramData\DucKING\backup.json. Turning a setting off restores that value.",
            @"Исходное значение каждой изменённой настройки хранится в C:\ProgramData\DucKING\backup.json. Когда вы выключаете настройку, это значение восстанавливается."),

        ["unit.ghz"] = ("GHz", "GHz", "ГГц"),

        // ---- Oyunlar ----
        ["games.subtitle"] = ("Bilgisayarındaki oyunlar için hazır grafik profilleri. Profil, oyunun kendi ayar dosyasına yazılır; ilk değişiklikten önce orijinal dosya yedeklenir ve tek tıkla geri alınabilir. Oyun açıkken profil uygulanamaz.",
            "Ready-made graphics profiles for the games on your PC. A profile is written to the game's own settings file; the original file is backed up before the first change and can be restored with one click. Profiles can't be applied while the game is running.",
            "Готовые графические профили для игр на вашем компьютере. Профиль записывается в собственный файл настроек игры; перед первым изменением исходный файл сохраняется, и его можно вернуть одним щелчком. Пока игра запущена, профиль применить нельзя."),
        ["games.update"] = ("Profilleri güncelle", "Update profiles", "Обновить профили"),
        ["games.empty"] = ("Desteklenen bir oyun bulunamadı. Steam, Epic ve Riot kütüphaneleri tarandı.",
            "No supported game found. Steam, Epic and Riot libraries were scanned.",
            "Поддерживаемые игры не найдены. Проверены библиотеки Steam, Epic и Riot."),
        ["games.loading"] = ("Oyunlar taranıyor…", "Scanning games…", "Поиск игр…"),
        ["games.cardTip"] = ("Ayrıntılar için tıklayın", "Click for details", "Нажмите, чтобы увидеть подробности"),
        ["games.revert"] = ("Oyunun kendi ayarlarına dön", "Back to the game's own settings", "Вернуть собственные настройки игры"),
        ["games.noSettings"] = ("Bu oyun için gösterilecek grafik ayarı yok. Ayar dosyası bulunduğunda burada listelenir.",
            "No graphics settings to show for this game yet. They'll be listed here once its settings file is found.",
            "Для этой игры пока нечего показать. Настройки появятся здесь, когда будет найден её файл настроек."),
        ["games.colSetting"] = ("Ayar", "Setting", "Настройка"),
        ["games.colNow"] = ("Şu an", "Now", "Сейчас"),
        ["games.legendRed"] = ("Kırmızı", "Red", "Красные"),
        ["games.legendRest"] = (" değerler profil uygulanınca değişir; soluk olanlar aynı kalır. Değerler oyunun kendi ayar dosyasından okunur.",
            " values change when the profile is applied; faded ones stay the same. Values are read from the game's own settings file.",
            " значения изменятся при применении профиля; бледные останутся прежними. Значения читаются из файла настроек игры."),

        // ---- Donanım ----
        ["hw.subtitle"] = ("Ekran kartı, işlemci ve bellek kullanımı canlı olarak gösterilir. Veriler Windows'un kendi kaynaklarından okunur; ek sürücü kurulmaz. Ölçüm yalnızca bu sayfa açıkken yapılır.",
            "Live graphics card, CPU and memory usage. Data is read from Windows' own sources; no extra driver is installed. Measuring only runs while this page is open.",
            "Загрузка видеокарты, процессора и памяти в реальном времени. Данные берутся из источников самой Windows; дополнительные драйверы не устанавливаются. Измерение идёт, только пока открыта эта страница."),
        ["hw.temp"] = ("Sıcaklık", "Temperature", "Температура"),
        ["hw.usage"] = ("Kullanım", "Usage", "Загрузка"),
        ["hw.legendTemp"] = ("sıcaklık", "temperature", "температура"),
        ["hw.legendUsage"] = ("kullanım", "usage", "загрузка"),
        ["hw.legendWindow"] = ("son 60 sn", "last 60 s", "последние 60 с"),
        ["hw.fan"] = ("Fan", "Fan", "Вентилятор"),
        ["hw.power"] = ("Güç", "Power", "Мощность"),
        ["hw.clock"] = ("Saat hızı", "Clock speed", "Частота"),
        ["hw.cpuTempTip"] = ("İşlemci sıcaklığı Windows üzerinden okunamıyor; bunun için ek bir sürücü gerekir.",
            "CPU temperature can't be read through Windows; that needs an extra driver.",
            "Температуру процессора нельзя прочитать средствами Windows; для этого нужен дополнительный драйвер."),
        ["hw.cpuTempNote"] = ("İşlemci sıcaklığı Windows'un kendi verilerinde bulunmuyor; gösterebilmek için ek bir sürücü gerekir.",
            "Windows' own data doesn't include CPU temperature; showing it would need an extra driver.",
            "В собственных данных Windows нет температуры процессора; чтобы её показать, нужен дополнительный драйвер."),
        ["hw.ram"] = ("Bellek (RAM)", "Memory (RAM)", "Память (RAM)"),
    };
}
