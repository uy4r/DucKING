using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DucKING.Core;

namespace DucKING.Views;

/// <summary>
/// Ana sayfa widget düzeni: 4 sütunlu ızgarada kutular sırayla ilk uygun boşluğa yerleşir.
/// Düzenleme modunda kutular sürüklenip başka bir kutunun üzerine bırakılarak yer değiştirir,
/// boyutu (1×1, 2×1, 2×2) seçilir ya da kaldırılır. Düzen ayarlara kaydedilir.
/// </summary>
public partial class HomeView
{
    private const int Columns = 4;
    private const string DragFormat = "ducking-widget";

    private sealed record WidgetDef(string Id, string Title, Border Tile);

    private static readonly WidgetPlacement[] DefaultLayout =
    [
        new() { Id = "dns", Size = WidgetSize.Large },
        new() { Id = "oneclick", Size = WidgetSize.Small },
        new() { Id = "dpi", Size = WidgetSize.Small },
        new() { Id = "gpu", Size = WidgetSize.Small },
        new() { Id = "ping", Size = WidgetSize.Small },
        new() { Id = "games", Size = WidgetSize.Small },
        new() { Id = "performance", Size = WidgetSize.Small },
        new() { Id = "privacy", Size = WidgetSize.Small },
        new() { Id = "startup", Size = WidgetSize.Small },
    ];

    private Dictionary<string, WidgetDef> _widgets = [];
    private List<WidgetPlacement> _layout = [];
    private bool _editing;
    private double _rowHeight = 200;
    private Point _dragStart;

    private void InitializeLayout()
    {
        WidgetDef[] all =
        [
            new("dns", UiText.Get("nav.dns"), DnsTile),
            new("oneclick", UiText.Get("home.oneClick"), OneClickTile),
            new("dpi", UiText.Get("nav.dpi"), DpiTile),
            new("gpu", UiText.Get("home.gpu"), GpuTile),
            new("ping", UiText.Get("home.ping"), PingTile),
            new("games", UiText.Get("nav.games"), GamesTile),
            new("performance", UiText.Get("nav.performance"), PerfTile),
            new("privacy", UiText.Get("nav.privacy"), PrivacyTile),
            new("startup", UiText.Get("home.startup"), StartupTile),
            new("system", UiText.Get("home.system"), SystemTile),
            new("apps", UiText.Get("nav.apps"), AppsTile),
        ];
        _widgets = all.ToDictionary(w => w.Id);
        foreach (var widget in all) Pool.Children.Remove(widget.Tile);

        _layout = AppSettings.Current.HomeLayout?
                      .Where(p => _widgets.ContainsKey(p.Id))
                      .DistinctBy(p => p.Id)
                      .Select(Clone)
                      .ToList() is { Count: > 0 } saved
            ? saved
            : DefaultLayout.Select(Clone).ToList();
        Rebuild();
    }

    private static WidgetPlacement Clone(WidgetPlacement p) => new() { Id = p.Id, Size = p.Size };

    private static (int Width, int Height) Span(WidgetSize size) => size switch
    {
        WidgetSize.Wide => (2, 1),
        WidgetSize.Large => (2, 2),
        _ => (1, 1),
    };

    private void Rebuild()
    {
        foreach (var cell in Board.Children.OfType<Grid>()) cell.Children.Clear(); // kutuları eski hücrelerinden ayır
        Board.Children.Clear();
        Board.RowDefinitions.Clear();
        Board.ColumnDefinitions.Clear();
        for (var c = 0; c < Columns; c++) Board.ColumnDefinitions.Add(new ColumnDefinition());

        var occupied = new List<bool[]>();
        foreach (var placement in _layout)
        {
            var (width, height) = Span(placement.Size);
            var (row, col) = FindSlot(occupied, width, height);
            for (var r = row; r < row + height; r++)
                for (var c = col; c < col + width; c++)
                    occupied[r][c] = true;

            var cell = CreateCell(placement);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            Grid.SetRowSpan(cell, height);
            Grid.SetColumnSpan(cell, width);
            Board.Children.Add(cell);
        }

        for (var r = 0; r < Math.Max(1, occupied.Count); r++)
            Board.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_rowHeight) });
    }

    /// <summary>Soldan sağa, yukarıdan aşağıya ilk sığan boşluk (ızgara boşluksuz kalır).</summary>
    private static (int Row, int Col) FindSlot(List<bool[]> occupied, int width, int height)
    {
        for (var row = 0; ; row++)
        {
            while (occupied.Count < row + height) occupied.Add(new bool[Columns]);
            for (var col = 0; col + width <= Columns; col++)
            {
                var free = true;
                for (var r = row; r < row + height && free; r++)
                    for (var c = col; c < col + width && free; c++)
                        free = !occupied[r][c];
                if (free) return (row, col);
            }
        }
    }

    private Grid CreateCell(WidgetPlacement placement)
    {
        var widget = _widgets[placement.Id];
        var cell = new Grid { Margin = new Thickness(5) };
        cell.Children.Add(widget.Tile);
        if (_editing) cell.Children.Add(CreateOverlay(widget, placement));
        return cell;
    }

    // ---- Düzenleme katmanı ----

    private Border CreateOverlay(WidgetDef widget, WidgetPlacement placement)
    {
        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x07, 0x07, 0x08)),
            BorderBrush = (Brush)FindResource("AccentLine"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(14),
            Cursor = Cursors.SizeAll,
            AllowDrop = true,
            Tag = widget.Id,
        };
        AutomationProperties_SetName(overlay, Loc.T($"{widget.Title} widget'ını taşı", $"Move the {widget.Title} widget",
            $"Переместить виджет «{widget.Title}»"));

        var content = new Grid();
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(new TextBlock
        {
            Text = "",
            FontFamily = (FontFamily)FindResource("IconFont"),
            FontSize = 20,
            Foreground = (Brush)FindResource("Danger"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        center.Children.Add(new TextBlock
        {
            Text = widget.Title,
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("Text"),
        });
        content.Children.Add(center);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8) };
        foreach (var (size, label) in new[] { (WidgetSize.Small, "1×1"), (WidgetSize.Wide, "2×1"), (WidgetSize.Large, "2×2") })
            tools.Children.Add(CreateChip(label, placement.Size == size, Loc.T("Boyut", "Size", "Размер") + " " + label, () => Resize(widget.Id, size)));
        tools.Children.Add(CreateChip("", false, Loc.T("Kaldır", "Remove", "Убрать"), () => RemoveWidget(widget.Id), icon: true));
        content.Children.Add(tools);
        overlay.Child = content;

        overlay.MouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(this);
        overlay.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var delta = e.GetPosition(this) - _dragStart;
            if (Math.Abs(delta.X) < 6 && Math.Abs(delta.Y) < 6) return;
            DragDrop.DoDragDrop(overlay, new DataObject(DragFormat, widget.Id), DragDropEffects.Move);
        };
        overlay.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
            overlay.BorderBrush = (Brush)FindResource("Accent");
            e.Handled = true;
        };
        overlay.DragLeave += (_, _) => overlay.BorderBrush = (Brush)FindResource("AccentLine");
        overlay.Drop += (_, e) =>
        {
            if (e.Data.GetData(DragFormat) is string dragged) MoveWidget(dragged, widget.Id);
            e.Handled = true;
        };
        return overlay;
    }

    private static void AutomationProperties_SetName(DependencyObject element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);

    private Button CreateChip(string text, bool active, string name, Action click, bool icon = false)
    {
        var label = new TextBlock { Text = text, FontSize = icon ? 10 : 11 };
        if (icon) label.FontFamily = (FontFamily)FindResource("IconFont");
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = name,
        };
        button.SetResourceReference(StyleProperty, active ? "PrimaryButton" : "ButtonBase");
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private void MoveWidget(string dragged, string target)
    {
        if (dragged == target) return;
        var item = _layout.First(p => p.Id == dragged);
        _layout.Remove(item);
        _layout.Insert(_layout.FindIndex(p => p.Id == target) is var i and >= 0 ? i : _layout.Count, item);
        SaveAndRebuild();
    }

    private void Resize(string id, WidgetSize size)
    {
        _layout.First(p => p.Id == id).Size = size;
        SaveAndRebuild();
    }

    private void RemoveWidget(string id)
    {
        if (_layout.Count <= 1) return;
        _layout.RemoveAll(p => p.Id == id);
        SaveAndRebuild();
    }

    private void SaveAndRebuild()
    {
        AppSettings.Current.SaveLayout(_layout.Select(Clone).ToList());
        Rebuild();
    }

    // ---- Üst çubuk ----

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        _editing = !_editing;
        EditText.Text = UiText.Get(_editing ? "home.done" : "home.edit");
        EditIcon.Text = _editing ? "" : "";
        var visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        EditHint.Visibility = AddWidgetButton.Visibility = ResetLayoutButton.Visibility = visibility;
        Rebuild();
    }

    private void AddWidget_Click(object sender, RoutedEventArgs e)
    {
        var hidden = _widgets.Values.Where(w => _layout.All(p => p.Id != w.Id)).ToList();
        if (hidden.Count == 0)
        {
            Log.Notify(Loc.T("Tüm widget'lar zaten ana sayfada.", "All widgets are already on the home page.", "Все виджеты уже на главной."));
            return;
        }

        var menu = new ContextMenu { PlacementTarget = AddWidgetButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var widget in hidden)
        {
            var item = new MenuItem { Header = widget.Title };
            item.Click += (_, _) =>
            {
                _layout.Add(new WidgetPlacement { Id = widget.Id, Size = WidgetSize.Small });
                SaveAndRebuild();
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        _layout = DefaultLayout.Select(Clone).ToList();
        AppSettings.Current.SaveLayout(null);
        Rebuild();
    }

    /// <summary>Ekranda 3 satır sığacak şekilde satır yüksekliği (en az 150 px); fazlası kaydırılır.</summary>
    private void BoardScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.HeightChanged) return;
        _rowHeight = Math.Max(150, Math.Floor(BoardScroll.ActualHeight / 3));
        foreach (var row in Board.RowDefinitions) row.Height = new GridLength(_rowHeight);
    }
}
