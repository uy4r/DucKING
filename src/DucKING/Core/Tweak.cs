using System.ServiceProcess;

namespace DucKING.Core;

public enum Risk
{
    /// <summary>Hiçbir özelliği bozmaz.</summary>
    Safe,
    /// <summary>Bazı kullanıcıların kullandığı bir özelliği kapatabilir.</summary>
    Moderate,
}

/// <summary>
/// Açılıp kapatılabilen tek bir ayar. Arayüzdeki anahtar <see cref="IsOn"/>'a bağlanır;
/// değiştirildiğinde ayar arka planda uygulanır/geri alınır ve gerçek durum yeniden okunur.
/// </summary>
public abstract class Tweak : ObservableObject
{
    private bool _isApplied;
    private bool _isBusy;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public Risk Risk { get; init; } = Risk.Safe;
    public bool Recommended { get; init; } = true;
    public bool RequiresRestart { get; init; }

    public string RiskText => Risk == Risk.Safe ? Loc.T("Güvenli", "Safe", "Безопасно") : UiText.Get("tweak.caution");
    public bool IsModerate => Risk == Risk.Moderate;

    /// <summary>Bu sistemde uygulanabilir mi (ör. servis hiç yoksa gizlenir).</summary>
    public virtual bool IsAvailable => true;

    public bool IsApplied
    {
        get => _isApplied;
        private set
        {
            if (Set(ref _isApplied, value)) OnPropertyChanged(nameof(IsOn));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public bool IsOn
    {
        get => _isApplied;
        set
        {
            if (value == _isApplied || _isBusy)
            {
                OnPropertyChanged(); // Anahtarı gerçek duruma geri çek.
                return;
            }
            _ = SetStateAsync(value);
        }
    }

    protected abstract bool CheckApplied();
    protected abstract void ApplyCore();
    protected abstract void RevertCore();

    public void Refresh()
    {
        try { IsApplied = CheckApplied(); }
        catch (Exception ex)
        {
            Log.Error($"Durum okunamadı: {Id}", ex);
            IsApplied = false;
        }
    }

    public Task ApplyAsync() => SetStateAsync(true);
    public Task RevertAsync() => SetStateAsync(false);

    private async Task SetStateAsync(bool on)
    {
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                if (on) ApplyCore(); else RevertCore();
            });
            Log.Info($"{(on ? "Uygulandı" : "Geri alındı")}: {Id}");
            if (RequiresRestart)
                Log.Notify(Loc.T($"\"{Title}\" için bilgisayarı yeniden başlatmanız gerekiyor.",
                    $"Restart your PC for \"{Title}\" to take effect.",
                    $"Чтобы «{Title}» вступило в силу, перезагрузите компьютер."));
        }
        catch (Exception ex)
        {
            Log.Error($"{Id} {(on ? "uygulanamadı" : "geri alınamadı")}", ex);
            Log.Notify(on
                ? Loc.T($"\"{Title}\" uygulanamadı: {ex.Message}", $"Couldn't apply \"{Title}\": {ex.Message}", $"Не удалось применить «{Title}»: {ex.Message}")
                : Loc.T($"\"{Title}\" geri alınamadı: {ex.Message}", $"Couldn't revert \"{Title}\": {ex.Message}", $"Не удалось отменить «{Title}»: {ex.Message}"),
                isError: true);
        }
        finally
        {
            Refresh();
            OnPropertyChanged(nameof(IsOn));
            IsBusy = false;
        }
    }
}

/// <summary>Bir grup kayıt defteri değerini yazar; geri alırken orijinal değerleri geri koyar.</summary>
public sealed class RegistryTweak : Tweak
{
    private readonly Func<IEnumerable<RegValue>> _values;

    public RegistryTweak(params RegValue[] values) => _values = () => values;

    /// <summary>Değer listesi çalışma anında hesaplanır (ör. ağ adaptörü başına anahtarlar).</summary>
    public RegistryTweak(Func<IEnumerable<RegValue>> values) => _values = values;

    protected override bool CheckApplied()
    {
        var values = _values().ToList();
        return values.Count > 0 && values.All(v => Reg.Matches(Reg.Read(v.Hive, v.Path, v.Name), v.Value));
    }

    protected override void ApplyCore()
    {
        foreach (var v in _values())
            BackupStore.RememberAndWrite(v.BackupKey, () => Reg.Capture(v), () => Reg.Write(v.Hive, v.Path, v.Name, v.Value, v.Kind));
    }

    protected override void RevertCore()
    {
        foreach (var v in _values())
        {
            if (BackupStore.TryGet(v.BackupKey, out var entry))
            {
                Reg.Restore(v, entry);
                BackupStore.Forget(v.BackupKey);
            }
            else if (v.Default is not null)
            {
                Reg.Write(v.Hive, v.Path, v.Name, v.Default, v.Kind);
            }
            else
            {
                Reg.Delete(v.Hive, v.Path, v.Name);
            }
        }
    }
}

/// <summary>Servisleri devre dışı bırakır (ya da elle başlatmaya alır) ve durdurur.</summary>
public sealed class ServiceTweak : Tweak
{
    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services\";
    private readonly string[] _services;
    private readonly int _targetStart;

    /// <param name="targetStart">3 = Elle, 4 = Devre dışı.</param>
    public ServiceTweak(int targetStart, params string[] services)
    {
        _targetStart = targetStart;
        _services = services;
    }

    private IEnumerable<string> Existing =>
        _services.Where(s => Reg.Read(Microsoft.Win32.RegistryHive.LocalMachine, ServicesPath + s, "Start") is not null);

    public override bool IsAvailable => Existing.Any();

    protected override bool CheckApplied() =>
        Existing.All(s => Convert.ToInt32(Reg.Read(Microsoft.Win32.RegistryHive.LocalMachine, ServicesPath + s, "Start")) == _targetStart);

    protected override void ApplyCore()
    {
        foreach (var name in Existing)
        {
            var value = new RegValue(Microsoft.Win32.RegistryHive.LocalMachine, ServicesPath + name, "Start", _targetStart);
            BackupStore.RememberAndWrite(value.BackupKey, () => Reg.Capture(value),
                () => Reg.Write(value.Hive, value.Path, value.Name, _targetStart, Microsoft.Win32.RegistryValueKind.DWord));
            TryStop(name);
        }
    }

    protected override void RevertCore()
    {
        foreach (var name in Existing)
        {
            var value = new RegValue(Microsoft.Win32.RegistryHive.LocalMachine, ServicesPath + name, "Start", 3);
            if (BackupStore.TryGet(value.BackupKey, out var entry))
            {
                Reg.Restore(value, entry);
                BackupStore.Forget(value.BackupKey);
            }
            else
            {
                Reg.Write(value.Hive, value.Path, value.Name, 3, Microsoft.Win32.RegistryValueKind.DWord);
            }

            if (Convert.ToInt32(Reg.Read(value.Hive, value.Path, "Start")) == 2)
                TryStart(name);
        }
    }

    private static void TryStop(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Servis durdurulamadı: {name}", ex);
        }
    }

    private static void TryStart(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
        }
        catch (Exception ex)
        {
            Log.Error($"Servis başlatılamadı: {name}", ex);
        }
    }
}

/// <summary>Durumu, uygulaması ve geri alması özel kodla yazılan ayarlar için.</summary>
public sealed class ActionTweak : Tweak
{
    public required Func<bool> Check { get; init; }
    public required Action Apply { get; init; }
    public required Action Revert { get; init; }
    public Func<bool>? Available { get; init; }

    public override bool IsAvailable => Available?.Invoke() ?? true;

    protected override bool CheckApplied() => Check();
    protected override void ApplyCore() => Apply();
    protected override void RevertCore() => Revert();
}
