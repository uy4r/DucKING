using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using DucKING.Core;
using Microsoft.Win32;

namespace DucKING.Modules.Dns;

/// <param name="IsConnected">İnternete bağlı mı; bağlı olmayan Wi-Fi/Ethernet de DNS ayarı için listelenir.</param>
public sealed record NetAdapter(string Name, string Guid, int Index, IReadOnlyList<string> DnsServers, bool IsConnected = true);

public static class DnsService
{
    private const string Tcpip4 = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\";
    private const string Tcpip6 = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\";
    private const string DohInterfaces = @"SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\";

    /// <summary>Windows 11 (build 22000+) yerleşik DNS-over-HTTPS destekler.</summary>
    public static bool SupportsDoh => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>İnternete bağlı fiziksel (Ethernet/Wi-Fi) adaptörler.</summary>
    public static IReadOnlyList<NetAdapter> GetActiveAdapters()
    {
        var list = new List<NetAdapter>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.GigabitEthernet)) continue;

            var props = nic.GetIPProperties();
            if (!props.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any))) continue;

            int index;
            try { index = props.GetIPv4Properties().Index; }
            catch (NetworkInformationException) { continue; }

            list.Add(new NetAdapter(
                nic.Name,
                nic.Id,
                index,
                props.DnsAddresses.Select(a => a.ToString()).ToList()));
        }
        return list;
    }

    // Sanal ve tünel adaptörleri: bağlı değilken DNS'leri değiştirilmez.
    private static readonly string[] VirtualAdapterWords =
    [
        "Virtual", "Hyper-V", "VMware", "VirtualBox", "Bluetooth", "TAP-", "Tunnel", "VPN", "WireGuard", "Wintun",
        "ZeroTier", "Npcap", "Loopback", "Miniport",
    ];

    /// <summary>
    /// DNS'i ayarlanacak adaptörler: bağlı olanlar ve şu an bağlı olmayan fiziksel Ethernet/Wi-Fi kartları.
    /// Bağlı olmayan Wi-Fi da sayılır: bilgisayar açılırken kendiliğinden bağlanıp modemin DNS'ini (Türk Telekom'da
    /// Discord için engel sayfası) devreye sokabilir; Windows o sırada DNS sorgularını iki adaptöre birden gönderir.
    /// </summary>
    public static IReadOnlyList<NetAdapter> GetConfigurableAdapters()
    {
        var list = GetActiveAdapters().ToList();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == OperationalStatus.Up || list.Any(a => a.Guid == nic.Id)) continue;
            if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.GigabitEthernet)) continue;
            if (VirtualAdapterWords.Any(w => nic.Description.Contains(w, StringComparison.OrdinalIgnoreCase))) continue;

            int index;
            try { index = nic.GetIPProperties().GetIPv4Properties().Index; }
            catch (NetworkInformationException) { continue; }

            list.Add(new NetAdapter(nic.Name, nic.Id, index, ConfiguredDnsServers(nic.Id), IsConnected: false));
        }
        return list;
    }

    /// <summary>Bağlı olmayan adaptörün DNS'i: elle girilmişse o, yoksa son DHCP kiralamasından gelen.</summary>
    private static IReadOnlyList<string> ConfiguredDnsServers(string guid)
    {
        var path = Tcpip4 + guid;
        var configured = Reg.Read(RegistryHive.LocalMachine, path, "NameServer") as string;
        if (string.IsNullOrWhiteSpace(configured)) configured = Reg.Read(RegistryHive.LocalMachine, path, "DhcpNameServer") as string;
        return (configured ?? "").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
    }

    public static DnsProvider? DetectActiveProvider(IReadOnlyList<NetAdapter> adapters)
    {
        var servers = adapters.SelectMany(a => a.DnsServers).ToHashSet();
        return DnsProvider.All.FirstOrDefault(p => p.Ipv4.Any(servers.Contains));
    }

    /// <summary>Kullanıcıya gösterilecek DNS adı (bilinen sağlayıcı, yerel proxy ya da modem).</summary>
    public static string DescribeCurrent(IReadOnlyList<NetAdapter> adapters)
    {
        if (adapters.Count == 0) return Loc.T("Bağlantı yok", "No connection", "Нет подключения");
        if (DetectActiveProvider(adapters) is { } provider) return provider.Name;

        var servers = adapters.SelectMany(a => a.DnsServers).ToList();
        if (servers.Count == 0) return Loc.T("Bilinmiyor", "Unknown", "Неизвестно");
        if (servers.Any(s => s is "127.0.0.1" or "::1"))
            return Loc.T("Yerel DNS proxy (dnscrypt-proxy vb.)", "Local DNS proxy (dnscrypt-proxy etc.)", "Локальный DNS-прокси (dnscrypt-proxy и т. п.)");
        return Loc.T("Modem / İnternet sağlayıcısı", "Router / ISP", "Роутер / провайдер");
    }

    /// <summary>Sunucuya gerçek bir DNS sorgusu atarak yanıt süresini ölçer (en iyi değeri döner).</summary>
    public static async Task<double?> MeasureLatencyAsync(string server, int attempts = 4)
    {
        var query = BuildQuery("www.google.com");
        double? best = null;

        for (var i = 0; i < attempts; i++)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            try
            {
                var sw = Stopwatch.StartNew();
                await udp.SendAsync(query, new IPEndPoint(IPAddress.Parse(server), 53), cts.Token);
                await udp.ReceiveAsync(cts.Token);
                sw.Stop();
                var ms = sw.Elapsed.TotalMilliseconds;
                if (best is null || ms < best) best = ms;
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException)
            {
                // Bu deneme zaman aşımına uğradı; diğerlerine bak.
            }
        }
        return best;
    }

    /// <summary>
    /// Bilgisayardaki DNS sunucularından biri Discord için sahte adres (ör. Türk Telekom'un engel sayfası
    /// 195.175.254.2) döndürüyor mu; Cloudflare ve Google'ın yanıtlarıyla karşılaştırarak anlar. Bağlı olmayan
    /// Wi-Fi'nin DNS'i de sorulur: açılışta kendiliğinden bağlanınca Discord'u engeller. DPI araçları doğru IP'ye
    /// bağlanılmadan işe yaramaz; bu yüzden DNS engeli ayrıca çözülmelidir.
    /// </summary>
    public static async Task<bool> IsDiscordBlockedByDnsAsync()
    {
        const string host = "discord.com";
        var publicAnswers = (await Task.WhenAll(QueryAsync("1.1.1.1", host), QueryAsync("8.8.8.8", host)))
            .SelectMany(a => a).ToList();
        if (publicAnswers.Count == 0) return false; // İnternet yok ya da 53. port kapalı; karar verilemez.

        var servers = GetConfigurableAdapters().SelectMany(a => a.DnsServers)
            .Select(s => IPAddress.TryParse(s, out var ip) ? ip : null)
            .OfType<IPAddress>()
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork || IPAddress.IsLoopback(ip))
            .Distinct().Take(6).ToList();
        if (servers.Count == 0) return false;

        // Discord Cloudflare anycast kullanır; sunucular farklı adres alt kümesi verebilir, /16 düzeyinde karşılaştırılır.
        // Yanıt vermeyen sunucu (ör. başka bir ağın Wi-Fi DNS'i) karar dışı kalır.
        static string Prefix(IPAddress ip) => string.Join('.', ip.GetAddressBytes().Take(2));
        var publicPrefixes = publicAnswers.Select(Prefix).ToHashSet();
        var answers = await Task.WhenAll(servers.Select(async s => (Server: s, Ips: await QueryAsync(s.ToString(), host))));
        var poisoned = answers.Where(a => a.Ips.Count > 0 && !a.Ips.Any(ip => publicPrefixes.Contains(Prefix(ip)))).ToList();
        foreach (var (server, ips) in poisoned)
            Log.Info($"DNS engeli: {server} {host} için [{string.Join(", ", ips)}] döndürdü, gerçek adresler [{string.Join(", ", publicAnswers.Take(3))}…]");
        return poisoned.Count > 0;
    }

    /// <summary>Verilen sunucudan UDP ile A kaydı sorar; yanıt yoksa boş liste döner.</summary>
    public static async Task<IReadOnlyList<IPAddress>> QueryAsync(string server, string host, int timeoutMs = 1500)
    {
        var serverIp = IPAddress.Parse(server);
        var query = BuildQuery(host);
        try
        {
            using var udp = new UdpClient(serverIp.AddressFamily);
            using var cts = new CancellationTokenSource(timeoutMs);
            await udp.SendAsync(query, new IPEndPoint(serverIp, 53), cts.Token);
            var response = (await udp.ReceiveAsync(cts.Token)).Buffer;
            if (response.Length < 12 || response[0] != query[0] || response[1] != query[1]) return [];
            return ParseARecords(response);
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException)
        {
            return [];
        }
    }

    private static List<IPAddress> ParseARecords(byte[] r)
    {
        var result = new List<IPAddress>();
        try
        {
            int questions = r[4] << 8 | r[5], answers = r[6] << 8 | r[7];
            var pos = 12;
            for (var i = 0; i < questions; i++) pos = SkipName(r, pos) + 4;
            for (var i = 0; i < answers; i++)
            {
                pos = SkipName(r, pos);
                int type = r[pos] << 8 | r[pos + 1], length = r[pos + 8] << 8 | r[pos + 9];
                pos += 10;
                if (type == 1 && length == 4) result.Add(new IPAddress(r.AsSpan(pos, 4)));
                pos += length;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Bozuk/kesik yanıt: okunabilen kayıtlarla devam.
        }
        return result;

        static int SkipName(byte[] r, int pos)
        {
            while (true)
            {
                var len = r[pos];
                if (len == 0) return pos + 1;
                if ((len & 0xC0) == 0xC0) return pos + 2; // sıkıştırılmış ad işaretçisi
                pos += len + 1;
            }
        }
    }

    /// <summary>Windows'un DoH şablonunu bildiği sağlayıcılar arasından bu bağlantıda en hızlısını seçer.</summary>
    public static async Task<DnsProvider> PickFastestSecureAsync()
    {
        var candidates = DnsProvider.All.Where(p => p.Name is "Cloudflare" or "Google" or "Quad9").ToList();
        var latencies = await Task.WhenAll(candidates.Select(p => MeasureLatencyAsync(p.Ipv4[0], attempts: 2)));
        return candidates.Zip(latencies)
            .Where(x => x.Second.HasValue)
            .OrderBy(x => x.Second)
            .Select(x => x.First)
            .FirstOrDefault() ?? candidates[0];
    }

    private static byte[] BuildQuery(string host)
    {
        var bytes = new List<byte>
        {
            (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), // ID
            0x01, 0x00, // standart sorgu, recursion desired
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // QD=1, AN=NS=AR=0
        };
        foreach (var label in host.Split('.'))
        {
            bytes.Add((byte)label.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(label));
        }
        bytes.AddRange([0x00, 0x00, 0x01, 0x00, 0x01]); // kök, TYPE A, CLASS IN
        return bytes.ToArray();
    }

    /// <summary>Tüm Ethernet/Wi-Fi adaptörlerinin (bağlı olmayanlar dahil) DNS'ini sağlayıcıya çevirir; istenirse DoH'u da açar.</summary>
    public static void Apply(DnsProvider provider, bool enableDoh)
    {
        var adapters = GetConfigurableAdapters();
        if (!adapters.Any(a => a.IsConnected))
            throw new InvalidOperationException(Loc.T("İnternete bağlı bir Ethernet/Wi-Fi adaptörü bulunamadı.",
                "No Ethernet/Wi-Fi adapter connected to the internet was found.",
                "Не найден Ethernet/Wi-Fi-адаптер с подключением к интернету."));

        var script = new StringBuilder();
        var doh = enableDoh && SupportsDoh;

        if (doh)
        {
            foreach (var ip in provider.Ipv4.Concat(provider.Ipv6))
            {
                var q = ProcessRunner.PsQuote(ip);
                var t = ProcessRunner.PsQuote(provider.DohTemplate);
                script.AppendLine($$"""
                    if (Get-DnsClientDohServerAddress -ServerAddress {{q}} -ErrorAction SilentlyContinue) {
                        Set-DnsClientDohServerAddress -ServerAddress {{q}} -DohTemplate {{t}} -AllowFallbackToUdp $true -AutoUpgrade $true | Out-Null
                    } else {
                        Add-DnsClientDohServerAddress -ServerAddress {{q}} -DohTemplate {{t}} -AllowFallbackToUdp $true -AutoUpgrade $true | Out-Null
                    }
                    """);
            }
        }

        foreach (var adapter in adapters)
        {
            BackupAdapter(adapter);
            var v4 = string.Join(",", provider.Ipv4.Select(ProcessRunner.PsQuote));
            var v6 = string.Join(",", provider.Ipv6.Select(ProcessRunner.PsQuote));
            // Bağlı olmayan adaptörde hata işlemi durdurmasın; asıl bağlantının ayarı yine yapılır.
            script.AppendLine(adapter.IsConnected
                ? $"Set-DnsClientServerAddress -InterfaceIndex {adapter.Index} -ServerAddresses @({v4})"
                : $"try {{ Set-DnsClientServerAddress -InterfaceIndex {adapter.Index} -ServerAddresses @({v4}) -ErrorAction Stop }} catch {{ }}");
            // IPv6 kapalı adaptörlerde hata verebilir; bu durumda yalnızca IPv4 yeterli.
            script.AppendLine($"try {{ Set-DnsClientServerAddress -InterfaceIndex {adapter.Index} -ServerAddresses @({v4},{v6}) }} catch {{ }}");
        }
        script.AppendLine("Clear-DnsClientCache");

        var result = ProcessRunner.PowerShell(script.ToString());
        if (!result.Success)
            throw new InvalidOperationException(FirstLine(result.Output));

        foreach (var adapter in adapters)
        {
            ClearInterfaceDoh(adapter.Guid);
            if (doh) SetInterfaceDoh(adapter.Guid, provider);
        }

        Log.Info($"DNS ayarlandı: {provider.Name} (DoH: {doh}) -> {string.Join(", ", adapters.Select(a => a.Name))}");
    }

    /// <summary>DNS'i DucKING'den önceki haline (çoğu zaman modemden otomatik) döndürür.</summary>
    public static void Restore()
    {
        var script = new StringBuilder();
        foreach (var adapter in GetConfigurableAdapters())
        {
            var key = $"dns|{adapter.Guid}";
            var backedUp = BackupStore.TryGet(key, out var entry);
            if (!backedUp && !adapter.IsConnected) continue; // DucKING'in hiç dokunmadığı bağlı olmayan adaptör

            string[] servers = [];
            if (backedUp && !string.IsNullOrWhiteSpace(entry.Data))
                servers = entry.Data.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);

            script.AppendLine(servers.Length == 0
                ? $"Set-DnsClientServerAddress -InterfaceIndex {adapter.Index} -ResetServerAddresses"
                : $"Set-DnsClientServerAddress -InterfaceIndex {adapter.Index} -ServerAddresses @({string.Join(",", servers.Select(ProcessRunner.PsQuote))})");

            ClearInterfaceDoh(adapter.Guid);
            BackupStore.Forget(key);
        }
        script.AppendLine("Clear-DnsClientCache");

        var result = ProcessRunner.PowerShell(script.ToString());
        if (!result.Success)
            throw new InvalidOperationException(FirstLine(result.Output));
        Log.Info("DNS ayarları geri alındı.");
    }

    public static void FlushCache() => ProcessRunner.Run("ipconfig.exe", "/flushdns");

    /// <summary>Adaptörün elle girilmiş DNS'ini saklar (boşsa DHCP'den otomatik alıyordur).</summary>
    private static void BackupAdapter(NetAdapter adapter)
    {
        BackupStore.Remember($"dns|{adapter.Guid}", () =>
        {
            var v4 = Reg.Read(RegistryHive.LocalMachine, Tcpip4 + adapter.Guid, "NameServer") as string ?? "";
            var v6 = Reg.Read(RegistryHive.LocalMachine, Tcpip6 + adapter.Guid, "NameServer") as string ?? "";
            var all = string.Join(",", new[] { v4, v6 }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new BackupStore.Entry { Exists = all.Length > 0, Data = all };
        });
    }

    private static void SetInterfaceDoh(string guid, DnsProvider provider)
    {
        // DohFlags=1: Windows'un kayıtlı DoH şablonunu otomatik kullan (Ayarlar > Ağ ekranındaki "Şifreli" seçeneği).
        foreach (var (family, ips) in new[] { ("Doh", provider.Ipv4), ("Doh6", provider.Ipv6) })
        {
            foreach (var ip in ips)
            {
                var path = $@"{DohInterfaces}{guid}\DohInterfaceSettings\{family}\{ip}";
                Reg.Write(RegistryHive.LocalMachine, path, "DohFlags", 1L, RegistryValueKind.QWord);
            }
        }
    }

    private static void ClearInterfaceDoh(string guid)
    {
        using var key = Reg.Base(RegistryHive.LocalMachine).OpenSubKey(DohInterfaces + guid, writable: true);
        key?.DeleteSubKeyTree("DohInterfaceSettings", throwOnMissingSubKey: false);
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? Loc.T("Bilinmeyen hata", "Unknown error", "Неизвестная ошибка");
}
