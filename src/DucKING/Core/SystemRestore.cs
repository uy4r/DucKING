using Microsoft.Win32;

namespace DucKING.Core;

public static class SystemRestore
{
    private const string Key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string Frequency = "SystemRestorePointCreationFrequency";

    /// <summary>
    /// Sistem geri yükleme noktası oluşturur. Windows normalde 24 saatte bir noktaya izin verir;
    /// bu sınır geçici olarak kaldırılıp eski haline döndürülür.
    /// </summary>
    public static void Create(string description)
    {
        var previous = Reg.Read(RegistryHive.LocalMachine, Key, Frequency);
        Reg.Write(RegistryHive.LocalMachine, Key, Frequency, 0, RegistryValueKind.DWord);
        try
        {
            var result = ProcessRunner.PowerShell($"""
                Enable-ComputerRestore -Drive "$env:SystemDrive\"
                Checkpoint-Computer -Description {ProcessRunner.PsQuote(description)} -RestorePointType MODIFY_SETTINGS
                """, timeoutMs: 300_000);
            if (!result.Success)
                throw new InvalidOperationException(result.Output.Split('\n').FirstOrDefault()?.Trim());
        }
        finally
        {
            if (previous is null) Reg.Delete(RegistryHive.LocalMachine, Key, Frequency);
            else Reg.Write(RegistryHive.LocalMachine, Key, Frequency, previous, RegistryValueKind.DWord);
        }
        Log.Info($"Geri yükleme noktası oluşturuldu: {description}");
    }
}
