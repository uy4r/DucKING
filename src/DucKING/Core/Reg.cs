using Microsoft.Win32;

namespace DucKING.Core;

/// <summary>Yazılacak tek bir kayıt defteri değeri.</summary>
/// <param name="Default">Yedek yoksa geri alırken yazılacak Windows varsayılanı; null ise değer silinir.</param>
public sealed record RegValue(
    RegistryHive Hive,
    string Path,
    string Name,
    object Value,
    RegistryValueKind Kind = RegistryValueKind.DWord,
    object? Default = null)
{
    public string BackupKey => $"reg|{Hive}\\{Path}\\{Name}";

    public static RegValue Hkcu(string path, string name, object value, object? @default = null, RegistryValueKind kind = RegistryValueKind.DWord) =>
        new(RegistryHive.CurrentUser, path, name, value, kind, @default);

    public static RegValue Hklm(string path, string name, object value, object? @default = null, RegistryValueKind kind = RegistryValueKind.DWord) =>
        new(RegistryHive.LocalMachine, path, name, value, kind, @default);
}

public static class Reg
{
    public static RegistryKey Base(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    public static object? Read(RegistryHive hive, string path, string name)
    {
        using var key = Base(hive).OpenSubKey(path);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public static void Write(RegistryHive hive, string path, string name, object value, RegistryValueKind kind)
    {
        using var key = Base(hive).CreateSubKey(path, writable: true);
        key.SetValue(name, value, kind);
    }

    public static void Delete(RegistryHive hive, string path, string name)
    {
        using var key = Base(hive).OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static bool Matches(object? current, object expected)
    {
        if (current is null) return false;
        return expected switch
        {
            string s => current is string c && string.Equals(c, s, StringComparison.OrdinalIgnoreCase),
            byte[] b => current is byte[] cb && cb.AsSpan().SequenceEqual(b),
            // DWORD 0xFFFFFFFF hem int(-1) hem uint olarak gelebilir; alt 32 bit karşılaştırılır.
            int or uint or long => current is int or long && (Convert.ToInt64(current) & 0xFFFFFFFF) == (Convert.ToInt64(expected) & 0xFFFFFFFF),
            _ => Equals(current, expected),
        };
    }

    public static BackupStore.Entry Capture(RegValue v)
    {
        using var key = Base(v.Hive).OpenSubKey(v.Path);
        var current = key?.GetValue(v.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (current is null) return new BackupStore.Entry { Exists = false };

        var kind = key!.GetValueKind(v.Name);
        var data = current switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            string[] lines => string.Join('\0', lines),
            int dword => unchecked((uint)dword).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture),
        };
        return new BackupStore.Entry { Exists = true, Kind = kind.ToString(), Data = data };
    }

    public static void Restore(RegValue v, BackupStore.Entry entry)
    {
        if (!entry.Exists)
        {
            Delete(v.Hive, v.Path, v.Name);
            return;
        }

        var kind = Enum.Parse<RegistryValueKind>(entry.Kind!);
        object value = kind switch
        {
            RegistryValueKind.DWord => unchecked((int)uint.Parse(entry.Data!)),
            RegistryValueKind.QWord => long.Parse(entry.Data!),
            RegistryValueKind.Binary => Convert.FromBase64String(entry.Data!),
            RegistryValueKind.MultiString => entry.Data!.Split('\0'),
            _ => entry.Data ?? string.Empty,
        };
        Write(v.Hive, v.Path, v.Name, value, kind);
    }
}
