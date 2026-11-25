using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerCheck.Core;

namespace PowerCheck.App;

public partial class MainWindow : Window
{
    private readonly DeviceStore _store = new();
    private readonly ObservableCollection<DeviceUse> _items = [];
    private readonly bool _ready;

    public MainWindow()
    {
        InitializeComponent();

        _store.Changed += Store_Changed;
        RefreshPicker(null);
        InitDevicesTab();

        ItemsList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => Recalculate();

        _ready = true;
        Recalculate();
    }

    // ---- Brushes for the traffic lights ----
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;

    internal static Brush Dot(Verdict v) => v switch
    {
        Verdict.Green => Brush("#2E7D32"),
        Verdict.Yellow => Brush("#F2A900"),
        _ => Brush("#C62828"),
    };

    private static Brush Light(Verdict v) => v switch
    {
        Verdict.Green => Brush("#E5F3E6"),
        Verdict.Yellow => Brush("#FFF4D6"),
        _ => Brush("#FBE3E3"),
    };

    // ---- Device list shared by the tabs ----
    private void Store_Changed(string? oldName, Device? device)
    {
        // Update anything already on the cord that used the old version.
        if (oldName is not null && device is not null)
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Device.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    _items[i] = _items[i] with { Device = device };

        RefreshPicker(device?.Name);
        RefreshDevicesTab(device?.Name);
    }

    private void RefreshPicker(string? select)
    {
        var all = _store.FavoritesFirst;
        DevicePicker.ItemsSource = all;
        DevicePicker.SelectedItem = all.FirstOrDefault(d => d.Name.Equals(select ?? "", StringComparison.OrdinalIgnoreCase))
                                    ?? all.FirstOrDefault();
    }

    private void DevicePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DevicePicker.SelectedItem is not Device d)
        {
            DeviceNote.Text = "";
            DeleteDeviceButton.IsEnabled = false;
            return;
        }
        string source = _store.SourceLabel(d) switch { "Edited" => " (your edited copy)", "Yours" => " (your device)", _ => "" };
        DeviceNote.Text = $"{d.Watts:0} W ({d.Amps:0.0} A){source}. {d.Note}".TrimEnd();
        SetDeleteButton(DeleteDeviceButton, d);
    }

    private void SetDeleteButton(Button button, Device? d)
    {
        bool mine = d is not null && _store.IsMine(d);
        button.IsEnabled = mine;
        button.Content = mine && _store.IsBuiltIn(d!.Name) ? "Undo my edit" : "Delete";
        button.ToolTip = mine ? null : "Built-in devices can be edited (that saves your own copy) but not deleted.";
    }

    private void NewDevice_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void EditDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePicker.SelectedItem is Device d) OpenEditor(d);
    }

    private void DeleteDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePicker.SelectedItem is Device d) DeleteDevice(d);
    }

    private void OpenEditor(Device? device)
    {
        var editor = new DeviceEditor(device, _store.Categories) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        try
        {
            _store.Save(editor.Result, device);
            _store.RecycleImageIfUnused(editor.ReplacedImage);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save your devices to {DeviceStore.MinePath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void DeleteDevice(Device d)
    {
        if (!_store.IsMine(d)) return;
        string question = _store.IsBuiltIn(d.Name)
            ? $"Go back to the built-in values for \"{d.Name}\"?"
            : $"Delete \"{d.Name}\" from your devices?";
        if (MessageBox.Show(this, question, "PowerCheck", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { _store.Remove(d); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save your devices to {DeviceStore.MinePath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePicker.SelectedItem is not Device d) return;
        int qty = int.TryParse(QtyBox.Text, out var q) && q > 0 ? Math.Min(q, 100) : 1;

        var existing = _items.FirstOrDefault(i => i.Device == d);
        if (existing is not null)
        {
            _items[_items.IndexOf(existing)] = existing with { Quantity = existing.Quantity + qty };
            return;
        }
        _items.Add(new DeviceUse(d, qty));
    }

    private void AddLabel_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(LabelValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value <= 0)
        {
            MessageBox.Show(this, "Type the number from the label, for example 1500 or 12.5.", "PowerCheck");
            return;
        }
        bool isAmps = LabelUnit.SelectedIndex == 1;
        double watts = isAmps ? Electric.AmpsToWatts(value) : value;
        string unit = isAmps ? "A" : LabelUnit.SelectedIndex == 2 ? "VA" : "W";

        _items.Add(new DeviceUse(new Device
        {
            Name = $"Label: {value:0.##} {unit}",
            Watts = watts,
            Continuous = LabelContinuous.IsChecked == true,
        }));
        LabelValue.Clear();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DeviceUse u) _items.Remove(u);
    }

    private void Input_Changed(object sender, RoutedEventArgs e) => Recalculate();

    private int LengthFeet()
    {
        if (Len25.IsChecked == true) return 25;
        if (Len100.IsChecked == true) return 100;
        if (LenCustom.IsChecked == true)
            return int.TryParse(LenCustomBox.Text, out var ft) && ft > 0 ? Math.Min(ft, 1000) : 0;
        return 50;
    }

    // ---- Answer ----
    private void Recalculate()
    {
        if (!_ready) return;

        int feet = LengthFeet();
        bool outdoor = Outdoor.IsChecked == true;
        double watts = _items.Sum(i => i.Watts);
        TotalText.Text = _items.Count == 0 ? "" : $"Total: {watts:0} W  ({Electric.WattsToAmps(watts):0.0} A)";

        if (_items.Count == 0 || feet <= 0)
        {
            Banner.Background = Brush("#E9ECF0");
            Banner.BorderThickness = new Thickness(0);
            BannerTitle.Text = _items.Count == 0 ? "Add what you'll plug in" : "Enter a cord length";
            BannerDetail.Text = "Pick a device from the list, or type the watts or amps from its label.";
            GaugeRows.ItemsSource = null;
            NotesList.ItemsSource = null;
            return;
        }

        var report = CordSizer.Size(_items, feet, outdoor);
        bool wallOnly = _items.Any(i => i.Device.WallOnly);

        Banner.Background = Light(report.Overall);
        Banner.BorderBrush = Dot(report.Overall);
        Banner.BorderThickness = new Thickness(4, 0, 0, 0);
        BannerTitle.Text = wallOnly ? "Don't use an extension cord for this" : report.Headline;
        BannerDetail.Text = wallOnly && report.Recommended is not null
            ? $"If you have no choice for a short time: {report.Headline} Never cover it or run it under a rug."
            : $"{report.Watts:0} W ({report.Amps:0.0} A) over {feet} ft{(outdoor ? ", outdoors" : "")}.";

        GaugeRows.ItemsSource = report.Gauges.Select(g => new
        {
            Brush = Dot(g.Verdict),
            Gauge = g.Gauge.Label,
            g.Reason,
            Lengths = g.MaxFeet == 0 ? "Not usable"
                : g.GreenMaxFeet >= 1000 ? "Any normal length"
                : g.GreenMaxFeet > 0 ? $"Best up to {g.GreenMaxFeet} ft\nMax {Math.Min(g.MaxFeet, 999)} ft"
                : $"Max {Math.Min(g.MaxFeet, 999)} ft",
        }).ToList();

        NotesList.ItemsSource = report.Notes.Count > 0
            ? report.Notes
            : ["Lower gauge number = thicker wire = carries more. A 12 gauge cord is thicker than a 16 gauge one."];
    }
}
