# DucKING

Oyuncular için ücretsiz Windows aracı. Tek programdan:

- **Ana sayfa** — Sürüklenip boyutlandırılabilen widget'lar (1×1, 2×1, 2×2): DNS hızları, DPI durumu, canlı ping, ekran kartı sıcaklığı, tek tıkla oyuncu modu…
- **Oyunlar** — Proje sahibinin profil yazdığı oyunları (şu an Counter-Strike 2) Steam, Epic ve Riot kütüphanelerinde bulur; "Maksimum FPS" / "Dengeli" grafik profillerini oyunun kendi ayar dosyasına yazar (önce yedekler, tek tıkla geri alır). Oyunun kartına tıklayınca kenar yumuşatma, gölge, doku gibi ayarların şu anki ve profildeki değerleri tablo olarak görünür. Oyun başına "yüksek performanslı ekran kartı" ve "tam ekran iyileştirmelerini kapat" ayarları.
- **Donanım** — Ekran kartı sıcaklığı/kullanımı/VRAM, işlemci kullanımı ve saat hızı, RAM; ek sürücü kurmadan Windows'un kendi verilerinden.
- **Oyuncu DNS** — 6 DNS sağlayıcısına gerçek DNS sorgusuyla hız testi yapar, en hızlıyı tek tıkla uygular, Windows 11'de şifreli DNS (DoH) açar.
- **DPI Engel Aşma** — GoodbyeDPI, zapret veya ByeDPI'ı Windows servisi olarak kurar (Türk Telekom, Superonline, Kablonet… presetleri; Rusya için Rostelecom, Beeline, MGTS ve genel preset). "En iyi ayarı bul" presetleri sırayla dener, Discord, YouTube ve Roblox'u en iyi açanı kurar.
- **Performans** — Nihai Performans güç planı, Oyun Modu, Game DVR, ağ kısıtlaması, Nagle, fare ivmesi, HAGS vb.
- **Reklam ve Gizlilik** — Başlat menüsü Bing araması, reklamlar, öneriler, Copilot, Recall, Widget'lar, telemetri.
- **Başlangıç Uygulamaları** — Açılışta çalışan programları Görev Yöneticisi ile aynı yöntemle açıp kapatır.
- **Gereksiz Uygulamalar** — Haberler, Clipchamp, Teams gibi önceden yüklü uygulamaları kaldırır (Xbox'a dokunmaz).
- **Güncelleme denetimi** — Hakkında sayfasından yeni sürüm olup olmadığına bakılabilir; DucKING açılışta da bir kez denetler. Yalnızca bildirir, hiçbir şey indirmez veya kurmaz. GitHub'a giden istekte DucKING'in sürümü dışında bilgi yoktur. Depo GitHub'a yüklenince `UpdateChecker.Repository` sabitine "kullanıcı/DucKING" yazılır; boşken kapalıdır.
- **Tanılama raporu** — Hakkında → Sorun giderme → "Tanılama raporu oluştur", Masaüstüne günlük dosyası ve sistem bilgilerinden (DucKING/Windows sürümü, dil, DPI ve DNS durumu) oluşan bir zip kaydeder. Kullanıcı adı, bilgisayar adı ve profil klasörü gizlenir; dosya hiçbir yere gönderilmez, sorun bildirirken kendiniz eklersiniz.

Her ayar ayrı ayrı geri alınabilir: değiştirilen değerin orijinali
`C:\ProgramData\DucKING\backup.json` dosyasında saklanır. Günlük:
`C:\ProgramData\DucKING\logs\ducking.log`.

## Oyun profilleri

Oyun listesi `src/DucKING/Profiles/games.json` dosyasıdır ve yalnızca proje sahibi tarafından
düzenlenir. Programda listede yalnızca profili yazılmış oyunlar görünür; kullanıcı arayüzünden oyun
eklenemez. Dosya exe'nin içine gömülür, kurulum klasöründe düzenlenebilir bir kopyası olmaz.

Yeni oyun eklemek: `games.json`'a oyunu (algılama bilgisi, ayar dosyası konumu, en az bir profil ve
ayar adları) ekleyin, `version` numarasını artırın. Desteklenen ayar dosyası biçimleri:
`cs2-video`, `unreal-ini` (Unreal Engine `GameUserSettings.ini`), `key-value`. Yalnızca dosyada
zaten bulunan anahtarlar değiştirilir.

Depo GitHub'a yüklendiğinde `GameProfiles.UpdateUrl` sabitine bu dosyanın ham adresi
(`https://raw.githubusercontent.com/<kullanıcı>/DucKING/main/src/DucKING/Profiles/games.json`)
yazılırsa program, gömülü listeden daha yeni `version` numaralı dosyayı açılışta indirip o oturum
boyunca kullanır (diske kaydedilmez). İndirilen dosya katı kurallarla doğrulanır (izinli klasörler,
`..` yok, yalnızca .ini/.txt/.cfg, her oyunda en az bir profil).

## DPI presetleri

Presetler `src/DucKING/Profiles/dpi-presets.json` dosyasındadır (exe'ye gömülür). Her presetin
`source` alanı nereden alındığını gösterir ve arayüzde "Kaynak" bağlantısı olarak görünür. Kurulu
preset adına göre değil argümanlarına göre tanınır; bir presetin argümanlarını değiştirmek, eski
hâliyle kurulmuş servislerin tanınmamasına yol açar. `{dir}` kurulumda aracın klasörüyle değiştirilir.

Depo GitHub'a yüklendiğinde `DpiPresets.UpdateUrl` sabitine bu dosyanın ham adresi yazılırsa daha
yeni `version` numaralı dosya DPI sayfası açılınca indirilir ve o oturumda kullanılır (diske
kaydedilmez). Argümanlar SYSTEM olarak çalışan bir servise verildiğinden doğrulama katıdır: her araç
için yalnızca izin verilen seçenekler kabul edilir. Dosya yazan ya da kod yükleyen seçenekler
(`--debug`, `--wf-save`, `--hostlist-auto`, `--lua-*`…) kabul edilmez. Dosya yolları yalnızca
paketteki `files\` klasörünü gösterebilir.

## Derleme

.NET 8 SDK ve [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`) gerekir.

```bash
powershell -File build.ps1
```

`artifacts` klasörüne şunlar üretilir:

- `DucKING-Setup-<sürüm>.exe` — kurulum dosyası. Program Files'a kurar, Başlat menüsü ve
  masaüstü kısayolu ekler, "Uygulamalar ve özellikler"den kaldırılabilir. Kaldırırken DPI
  servislerini siler ve isterse yapılan ayarları geri alır (`DucKING.exe --uninstall [--revert]`).
- `portable\DucKING.exe` — kurulum gerektirmeyen tek dosya.

Sürüm numarası `src/DucKING/DucKING.csproj` içindeki `<Version>` alanından alınır.
Program yönetici olarak çalışır (DNS, servis ve HKLM ayarları için).

Dosyalar imzasız olduğu için Windows SmartScreen ilk açılışta uyarı gösterebilir
("Ek bilgi → Yine de çalıştır").

## Proje yapısı

```
src/DucKING/
  Core/              Ayar motoru (Tweak, RegistryTweak, ServiceTweak), yedekleme, servis API'si
  Modules/Dns/       DNS sağlayıcıları, hız testi, DoH
  Modules/Dpi/       GoodbyeDPI / zapret / ByeDPI kurulumu ve presetler
  Modules/Tweaks/    Performans ve gizlilik ayar listeleri
  Modules/Startup/   Başlangıç uygulamaları
  Modules/Apps/      Gereksiz Mağaza uygulamaları
  Views/             Sayfalar
  Themes/Dark.xaml   Tema
  Tools/             Paketlenen DPI araçları
```

Yeni bir ayar eklemek için `PerformanceTweaks.cs` veya `PrivacyTweaks.cs`
listesine bir `RegistryTweak` eklemek yeterlidir; arayüz otomatik oluşur.

## Lisanslar

Kullanılan açık kaynak bileşenler ve lisansları: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
