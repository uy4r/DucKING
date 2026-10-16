using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using DucKING.Modules.Dns;
using DucKING.Modules.Dpi;
using Microsoft.Win32;

namespace DucKING.Core;

/// <summary>
/// Sorun bildirirken eklenecek tanılama raporu: günlük dosyası (ducking.log) ve kısa sistem bilgisi (info.txt) tek bir zip'te.
/// Rapor yalnızca diske yazılır, hiçbir yere gönderilmez. Zip'e giren her metinde kullanıcı profili klasörü, kullanıcı adı,
/// bilgisayar adı, Steam hesap numarası ve e-posta adresleri yer tutucuyla değiştirilir (bkz. <see cref="Mask(string)"/>).
/// DPI ve DNS durumu yalnızca okunur (DpiService.GetStatus, DnsService.GetActiveAdapters); hiçbir ayar değiştirilmez.
/// </summary>
public static partial class DiagnosticsReport
{
    /// <summary>Günlük bundan büyükse yalnızca son kısmı eklenir.</summary>
    private const int MaxLogBytes = 2 * 1024 * 1024;

    /// <summary>Raporu klasöre (varsayılan: Masaüstü) yazar. Döner: zip dosyasının yolu.</summary>
    public static string Create(string? directory = null)
    {
        directory ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var info = Mask(BuildInfo());
        var log = Mask(ReadLog());

        var path = UniquePath(directory, $"DucKING-rapor-{DateTime.Now:yyyyMMdd-HHmm}");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddText(zip, "info.txt", info);
            AddText(zip, "ducking.log", log);
        }
        Log.Info($"Tanılama raporu oluşturuldu: {Path.GetFileName(path)}");
        return path;
    }

    /// <summary>Metindeki kişisel bilgileri bu bilgisayarın kullanıcısına göre gizler.</summary>
    public static string Mask(string text) => Mask(text,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.UserName, Environment.MachineName, Environment.UserDomainName);

    public static string Mask(string text, string? userProfile, string? userName, string? machineName, string? domain)
    {
        // 1) Profil klasörü (C:\Users\ozan → %USERPROFILE%), ters ve düz eğik çizgiyle.
        var profile = userProfile?.TrimEnd('\\', '/');
        if (!string.IsNullOrEmpty(profile))
        {
            text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
                       .Replace(profile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        // 2) Başka profil yolları ve kısa adlar (C:\Users\KULLAN~1\…); Public ve Default gibi ortak klasörler kalır.
        text = UsersFolder().Replace(text, "${1}%USERNAME%");

        // 3) Steam hesap numarası (…\Steam\userdata\123456789\…) ve e-posta adresleri.
        text = SteamUserData().Replace(text, "${1}<steam-id>");
        text = Email().Replace(text, "<e-mail>");

        // 4) Kullanıcı adı, profil klasörünün adı, bilgisayar adı ve etki alanı: tam sözcük olarak, büyük/küçük harf
        //    fark etmeden. Tek harfli adlar kimlik belirtmez ve metni bozar; atlanır. Uzun adlar önce değiştirilir.
        (string? Word, string Placeholder)[] words =
        [
            (userName, "%USERNAME%"),
            (string.IsNullOrEmpty(profile) ? null : Path.GetFileName(profile), "%USERNAME%"),
            (machineName, "%COMPUTERNAME%"),
            (domain, "%USERDOMAIN%"),
        ];
        foreach (var (word, placeholder) in words
                     .Where(w => w.Word?.Trim() is { Length: >= 2 })
                     .Select(w => (Word: w.Word!.Trim(), w.Placeholder))
                     .DistinctBy(w => w.Word, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(w => w.Word.Length))
        {
            text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}_])", placeholder,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return text;
    }

    /// <summary>info.txt: sürümler, dil, DPI ve DNS durumu. Her bölüm ayrı okunur; biri hata verirse diğerleri yine yazılır.</summary>
    public static string BuildInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("DucKING diagnostics report");
        sb.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm}");

        Section(sb, "DucKING", () =>
        [
            $"Version: {UpdateChecker.CurrentVersion.ToString(3)}",
            $"UI language: {Loc.Code(Loc.Current)}",
            $"Elevated: {YesNo(AppPaths.IsElevated)}",
            $"Program folder: {AppContext.BaseDirectory}",
            $".NET: {RuntimeInformation.FrameworkDescription}",
        ]);

        Section(sb, "Windows", () =>
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            var build = Environment.OSVersion.Version.Build;
            // ProductName Windows 11'de de "Windows 10" yazar.
            var product = Reg.Read(RegistryHive.LocalMachine, key, "ProductName") as string ?? "Windows";
            if (build >= 22000) product = product.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
            var display = Reg.Read(RegistryHive.LocalMachine, key, "DisplayVersion") as string;
            var ubr = Reg.Read(RegistryHive.LocalMachine, key, "UBR") is int u ? $".{u}" : "";
            return
            [
                $"Edition: {product}" + (display is null ? "" : $" {display}"),
                $"Version: {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}.{build}{ubr}",
                $"Architecture: {RuntimeInformation.OSArchitecture}",
            ];
        });

        Section(sb, "DPI", () =>
        {
            var status = DpiService.GetStatus();
            if (status.Tool is not { } tool) return ["Tool: none installed"];
            var lines = new List<string>
            {
                $"Tool: {tool.Name} (service {tool.ServiceName})",
                $"Running: {YesNo(status.Running)}",
                $"Preset: {DpiService.FindPreset(tool, status.Arguments)?.Name ?? "unknown"}",
                $"Arguments: {status.Arguments ?? "-"}",
            };
            if (status.HelperRunning is { } helper) lines.Add($"ProxiFyre running: {YesNo(helper)}");
            return lines;
        });

        Section(sb, "DNS", () =>
        {
            var adapters = DnsService.GetActiveAdapters();
            var lines = new List<string>
            {
                $"Provider: {DnsService.DetectActiveProvider(adapters)?.Name ?? (adapters.Count == 0 ? "no connection" : "not a listed provider")}",
                $"Built-in DoH supported: {YesNo(DnsService.SupportsDoh)}",
            };
            lines.AddRange(adapters.Select(a =>
                $"Adapter \"{a.Name}\": {(a.DnsServers.Count == 0 ? "automatic / none" : string.Join(", ", a.DnsServers.Select(DescribeServer)))}"));
            return lines;
        });

        return sb.ToString();
    }

    /// <summary>
    /// Listedeki sağlayıcıların ve IPv4 adresleri olduğu gibi yazılır (modem ya da internet sağlayıcısının sunucusu).
    /// Diğer IPv6 adresleri gizlenir: modemin genel IPv6 adresi bağlantının ön ekini, yani kabaca kimliğini gösterir.
    /// </summary>
    private static string DescribeServer(string server)
    {
        if (DnsProvider.All.Any(p => p.Ipv4.Contains(server) || p.Ipv6.Contains(server, StringComparer.OrdinalIgnoreCase))) return server;
        if (!IPAddress.TryParse(server, out var ip) || ip.AddressFamily != AddressFamily.InterNetworkV6) return server;
        return ip.IsIPv6LinkLocal ? "<IPv6 link-local>" : "<IPv6>";
    }

    private static void Section(StringBuilder sb, string title, Func<IEnumerable<string>> read)
    {
        sb.AppendLine().AppendLine($"[{title}]");
        try
        {
            foreach (var line in read()) sb.AppendLine(line);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(could not be read: {ex.Message})");
        }
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>Günlüğü, yazılırken de okunabilecek biçimde açar; çok büyükse son <see cref="MaxLogBytes"/> baytı alır.</summary>
    private static string ReadLog()
    {
        if (!File.Exists(AppPaths.LogFile)) return "(no log file)";
        using var stream = new FileStream(AppPaths.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var skipped = Math.Max(0, stream.Length - MaxLogBytes);
        stream.Seek(skipped, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        if (skipped == 0) return text;

        // Yarım kalan ilk satır atılır.
        var newline = text.IndexOf('\n');
        return $"(first {skipped / 1024} KB of the log skipped)\n" + (newline >= 0 ? text[(newline + 1)..] : text);
    }

    private static void AddText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string UniquePath(string directory, string baseName)
    {
        var path = Path.Combine(directory, baseName + ".zip");
        for (var i = 2; File.Exists(path); i++) path = Path.Combine(directory, $"{baseName}-{i}.zip");
        return path;
    }

    [GeneratedRegex(@"(?i)([A-Z]:[\\/]Users[\\/])(?!(?:Public|Default|Default User|All Users|%USERNAME%)(?:[\\/]|$))[^\\/:*?""'<>|\r\n]+")]
    private static partial Regex UsersFolder();

    [GeneratedRegex(@"(?i)([\\/]userdata[\\/])\d+")]
    private static partial Regex SteamUserData();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex Email();
}
