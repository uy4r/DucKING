using System.IO;

namespace DucKING.Core;

public static class AppPaths
{
    /// <summary>%ProgramData%\DucKING — servisler LocalSystem olarak çalıştığı için kullanıcı profili yerine burası kullanılır.</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DucKING");

    public static string BackupFile => Path.Combine(DataDir, "backup.json");
    public static string LogFile => Path.Combine(DataDir, "logs", "ducking.log");
    public static string InstalledToolsDir => Path.Combine(DataDir, "tools");
    public static string BundledToolsDir => Path.Combine(AppContext.BaseDirectory, "Tools");

    public static bool IsElevated { get; } =
        new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);

        // Servis dosyaları ve yedekler burada; yalnızca yöneticiler değiştirebilmeli.
        // (Klasör yönetici olmayan bir işlemce oluşturulduysa sahibi o kullanıcı olur.)
        // Yönetici değilken denenirse sahiplik alınamaz ama izinler daraltılır; işlem kendini kilitler.
        if (!IsElevated) return;
        try
        {
            if (!FolderSecurity.IsProtected(DataDir))
            {
                FolderSecurity.Protect(DataDir, allowUsersRead: true);
                Log.Info("Veri klasörünün izinleri sıkılaştırıldı.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Veri klasörünün izinleri ayarlanamadı", ex);
        }
    }
}
