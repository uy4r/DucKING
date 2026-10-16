using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace DucKING.Core;

/// <summary>
/// Windows servislerini doğrudan SCM API'si ile oluşturur/siler.
/// sc.exe'ye argüman göndermek yerine bu yol seçildi: yol ve parametrelerdeki tırnaklar bozulmaz.
/// </summary>
public static partial class NativeServices
{
    private const uint ScManagerAllAccess = 0xF003F;
    private const uint ServiceAllAccess = 0xF01FF;
    private const uint ServiceWin32OwnProcess = 0x10;
    private const uint ServiceAutoStart = 0x2;
    private const uint ServiceErrorNormal = 0x1;
    private const uint ServiceConfigDescription = 1;
    private const uint ServiceConfigFailureActions = 2;
    private const uint ServiceConfigFailureActionsFlag = 4;
    private const int ScActionRestart = 1;
    private const int ErrorServiceMarkedForDelete = 1072;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceDescription
    {
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScAction
    {
        public int Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceFailureActions
    {
        public uint ResetPeriod;
        public string? RebootMessage;
        public string? Command;
        public uint ActionCount;
        public IntPtr Actions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceFailureActionsFlag
    {
        public int OnNonCrashFailures;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenSCManager(string? machine, string? database, uint access);

    [LibraryImport("advapi32.dll", EntryPoint = "CreateServiceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateService(IntPtr scm, string name, string displayName, uint access, uint type,
        uint start, uint errorControl, string binaryPath, string? group, IntPtr tagId, string? dependencies,
        string? account, string? password);

    [LibraryImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenService(IntPtr scm, string name, uint access);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, ref ServiceDescription info);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, ref ServiceFailureActions info);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    private static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, ref ServiceFailureActionsFlag info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(IntPtr handle);

    public static bool Exists(string name) =>
        Reg.Read(RegistryHive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{name}", "Type") is not null;

    public static string? GetImagePath(string name) =>
        Reg.Read(RegistryHive.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{name}", "ImagePath") as string;

    public static bool IsRunning(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <param name="dependencies">Önce başlaması gereken servis (ör. bir sürücü).</param>
    public static void CreateAndStart(string name, string displayName, string description, string binaryPathWithArgs,
        string? dependencies = null)
    {
        var scm = OpenSCManager(null, null, ScManagerAllAccess);
        if (scm == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            // lpDependencies çift null ile biten bir liste; sondaki ikinci null'u marshaller ekler.
            var deps = dependencies is null ? null : dependencies + "\0";
            var svc = CreateService(scm, name, displayName, ServiceAllAccess, ServiceWin32OwnProcess,
                ServiceAutoStart, ServiceErrorNormal, binaryPathWithArgs, null, IntPtr.Zero, deps, null, null);
            if (svc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            var desc = new ServiceDescription { Description = description };
            ChangeServiceConfig2(svc, ServiceConfigDescription, ref desc);
            SetRestartOnFailure(svc, name);
            CloseServiceHandle(svc);
        }
        finally
        {
            CloseServiceHandle(scm);
        }

        using var controller = new ServiceController(name);
        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// Servis çökerse (ör. ByeDPI) Windows onu 2 sn, 5 sn ve 30 sn sonra yeniden başlatır; sayaç bir günde sıfırlanır.
    /// Bu olmadan çöken ByeDPI, yeniden kurulana kadar Discord'u bağlantısız bırakıyordu.
    /// </summary>
    private static void SetRestartOnFailure(IntPtr service, string name)
    {
        var actions = new[]
        {
            new ScAction { Type = ScActionRestart, Delay = 2_000 },
            new ScAction { Type = ScActionRestart, Delay = 5_000 },
            new ScAction { Type = ScActionRestart, Delay = 30_000 },
        };
        var handle = GCHandle.Alloc(actions, GCHandleType.Pinned);
        try
        {
            var info = new ServiceFailureActions
            {
                ResetPeriod = 24 * 60 * 60,
                ActionCount = (uint)actions.Length,
                Actions = handle.AddrOfPinnedObject(),
            };
            if (!ChangeServiceConfig2(service, ServiceConfigFailureActions, ref info))
                Log.Error($"Servis kurtarma ayarı yapılamadı: {name}", new Win32Exception(Marshal.GetLastWin32Error()));

            // Hata koduyla kapanma (çökme olmadan) da yeniden başlatmayı tetiklesin.
            var flag = new ServiceFailureActionsFlag { OnNonCrashFailures = 1 };
            ChangeServiceConfig2(service, ServiceConfigFailureActionsFlag, ref flag);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Servisi durdurur ve siler. Servis yoksa sessizce döner.</summary>
    public static void StopAndDelete(string name)
    {
        if (!Exists(name)) return;

        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is not (ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending))
                sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException)
        {
            Log.Error($"Servis durdurulamadı: {name}", ex);
        }

        var scm = OpenSCManager(null, null, ScManagerAllAccess);
        if (scm == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var svc = OpenService(scm, name, ServiceAllAccess);
            if (svc == IntPtr.Zero) return;
            if (!DeleteService(svc))
            {
                var err = Marshal.GetLastWin32Error();
                if (err != ErrorServiceMarkedForDelete) throw new Win32Exception(err);
            }
            CloseServiceHandle(svc);
        }
        finally
        {
            CloseServiceHandle(scm);
        }

        // SCM silmeyi, servisin tüm tutamaçları kapanınca tamamlar; yeni kurulumdan önce bekle.
        for (var i = 0; i < 20 && Exists(name); i++) Thread.Sleep(250);
    }
}
