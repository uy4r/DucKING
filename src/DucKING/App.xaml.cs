using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DucKING.Core;

namespace DucKING;

public partial class App : Application
{
    /// <summary>Kurulum programı (Inno Setup AppMutex) da bu adla DucKING'in açık olup olmadığını anlar.</summary>
    private const string MutexName = "DucKING.SingleInstance";

    private Mutex? _instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppPaths.EnsureCreated();
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            var revert = e.Args.Contains("--revert", StringComparer.OrdinalIgnoreCase);
            Shutdown(await Uninstaller.RunAsync(revert));
            return;
        }

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew && !(e.Args.Contains(RestartArg, StringComparer.OrdinalIgnoreCase) && WaitForPreviousInstance(_instanceMutex)))
        {
            MessageBox.Show(Loc.T("DucKING zaten açık.", "DucKING is already running.", "DucKING уже запущен."),
                "DucKING", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Log.Info($"DucKING {typeof(App).Assembly.GetName().Version?.ToString(3)} başlatıldı. Windows {Environment.OSVersion.Version}, dil {Loc.Code(Loc.Current)}");
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();

        // Yeni sürüm varsa yalnızca bildirir; UpdateChecker.Repository boşken ağa hiç çıkmaz.
        _ = UpdateChecker.CheckAtStartupAsync();
    }

    private const string RestartArg = "--restarted";

    /// <summary>
    /// Yeni bir DucKING başlatıp bu örneği kapatır (dil değişikliği). Tek örnek kilidi önce bırakılır; yeni örnek
    /// yine de kilidi dolu bulursa (eski süreç henüz kapanmadıysa) birkaç saniye bekler.
    /// </summary>
    public void Restart()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Kilit bu iş parçacığına ait değilse kapatmak yeterli.
        }
        _instanceMutex?.Dispose();
        _instanceMutex = null;

        // Yönetici yetkisiyle çalışan süreçten başlatılan süreç de yönetici olur; yeni UAC onayı istenmez.
        Process.Start(new ProcessStartInfo(exe, RestartArg) { UseShellExecute = false });
        Shutdown();
    }

    private static bool WaitForPreviousInstance(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // Önceki örnek kilidi bırakmadan kapandı; kilit artık bizde.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Beklenmeyen hata", e.Exception);
        MessageBox.Show(Loc.T($"Beklenmeyen bir hata oluştu:\n\n{e.Exception.Message}\n\nAyrıntılar: {AppPaths.LogFile}",
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails: {AppPaths.LogFile}",
                $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПодробности: {AppPaths.LogFile}"),
            "DucKING", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
