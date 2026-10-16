using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DucKING.Core;
using DucKING.Modules.Startup;

namespace DucKING.Views;

public partial class StartupView : UserControl, IRefreshable
{
    private IReadOnlyList<StartupItem> _items = [];

    public StartupView() => InitializeComponent();

    public async Task RefreshAsync()
    {
        foreach (var item in _items) item.PropertyChanged -= OnItemChanged;
        _items = await Task.Run(StartupManager.GetItems);
        foreach (var item in _items) item.PropertyChanged += OnItemChanged;

        List.ItemsSource = _items;
        UpdateCount();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => UpdateCount();

    private void UpdateCount() =>
        CountText.Text = Loc.T($"{_items.Count(i => i.IsEnabled)} / {_items.Count} program açılışta çalışıyor",
            $"{_items.Count(i => i.IsEnabled)} / {_items.Count} programs run at startup",
            $"В автозагрузке: {_items.Count(i => i.IsEnabled)} из {_items.Count}");

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
}
