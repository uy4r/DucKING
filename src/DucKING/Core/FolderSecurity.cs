using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DucKING.Core;

/// <summary>
/// SYSTEM olarak çalışan servislerin dosyaları, yönetici olmayan kullanıcıların değiştiremeyeceği
/// klasörlerde durmalı; aksi halde araya DLL koyularak yetki yükseltilebilir. ProxiFyre de bunu
/// klasörün kendisi ve tüm üst klasörleri için denetler.
/// </summary>
public static class FolderSecurity
{
    private const string SystemSid = "*S-1-5-18";
    private const string AdminsSid = "*S-1-5-32-544";
    private const string UsersSid = "*S-1-5-32-545";

    /// <summary>Sahibi Yöneticiler olan ve mirası kapatılmış (korumalı) bir klasör mü?</summary>
    public static bool IsProtected(string dir)
    {
        var security = new DirectoryInfo(dir).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        return owner is not null && owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) && security.AreAccessRulesProtected;
    }

    /// <summary>
    /// Ağacın sahibini Yöneticiler yapar ve klasöre yalnızca SYSTEM + Yöneticiler (isteğe bağlı olarak
    /// Kullanıcılar: okuma) izni bırakır. Alt öğeler miras yoluyla aynı izinleri alır; kendi mirası
    /// kapalı olan alt klasörlere (ör. ProxiFyre) dokunulmaz.
    /// </summary>
    public static void Protect(string dir, bool allowUsersRead)
    {
        ProcessRunner.Run("icacls.exe", $"\"{dir}\" /setowner {AdminsSid} /T /C /Q");

        var grants = $"\"{SystemSid}:(OI)(CI)F\" \"{AdminsSid}:(OI)(CI)F\"";
        if (allowUsersRead) grants += $" \"{UsersSid}:(OI)(CI)RX\"";

        var result = ProcessRunner.Run("icacls.exe", $"\"{dir}\" /inheritance:r /grant:r {grants} /Q");
        if (!result.Success)
            throw new InvalidOperationException(Loc.T($"Klasör izinleri ayarlanamadı: {dir}", $"Couldn't set folder permissions: {dir}",
                $"Не удалось настроить права доступа к папке: {dir}"));
    }

    /// <summary>
    /// Alt ağacın tamamını önce mirasa döndürür, sonra yalnızca klasörün kendi ACL'sini daraltır.
    /// (OI)(CI) bayraklarını /T ile doğrudan dosyalara uygulamak dosya ACL'lerini bozar; bu yüzden
    /// sıra bilinçli olarak böyledir.
    /// </summary>
    public static void ProtectExclusively(string dir)
    {
        ProcessRunner.Run("icacls.exe", $"\"{dir}\" /reset /T /C /Q");
        Protect(dir, allowUsersRead: false);
    }
}
