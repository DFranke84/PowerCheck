using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>One row in the Devices tab.</summary>
public sealed class DeviceRow(Device device, string source)
{
    public Device Device { get; } = device;
    public string Name => Device.Name;
    public string Note => Device.Note;
    public string Category => Device.Category;
    public double Watts => Device.Watts;
    public double Amps => Device.Amps;
    public string Source { get; } = source switch { "Yours" => "Yours", "Edited" => "Edited", _ => "Built-in" };
    public ImageSource? Thumb { get; } = DeviceStore.LoadThumb(device);
    public string ThumbTip => Thumb is null ? "No picture yet. Use Edit to add one." : "Click to see it bigger";
    public string Icon => CategoryIcon(Device.Category);
    public Uri Url => new(Device.ShopUrl);
    public string LinkText => Device.HasOwnLink ? "Product page" : "Find on Amazon";

    public string Flags
    {
        get
        {
            var f = new List<string>();
            if (Device.WallOnly) f.Add("Wall outlet only");
            if (Device.Continuous) f.Add("Runs for hours");
            if (Device.Motor) f.Add("Motor (starts hard)");
            return string.Join("\n", f);
        }
    }

    /// <summary>Simple symbol shown when a device has no picture.</summary>
    public static string CategoryIcon(string category)
    {
        string c = category.ToLowerInvariant();
        if (c.Contains("snow") || c.Contains("winter")) return "❄";      // snowflake
        if (c.Contains("light") || c.Contains("holiday")) return "★";    // star
        if (c.Contains("yard") || c.Contains("garden")) return "⚘";      // flower
        if (c.Contains("animal") || c.Contains("pet")) return "♥";       // heart
        if (c.Contains("tool") || c.Contains("garage")) return "⚒";      // hammer and pick
        if (c.Contains("cook") || c.Contains("kitchen")) return "☕";     // hot drink
        if (c.Contains("heat") || c.Contains("cool")) return "♨";        // hot springs
        if (c.Contains("pump") || c.Contains("water")) return "☔";       // umbrella with rain
        if (c.Contains("charg") || c.Contains("electronic")) return "⚡"; // lightning
        if (c.Contains("camp") || c.Contains("rv")) return "⛺";          // tent
        return "⚡";
    }
}

public partial class MainWindow
{
    private const string AllGroups = "All groups";
    private bool _devicesReady;

    private void InitDevicesTab()
    {
        RefreshGroups();
        _devicesReady = true;
        RefreshDevicesTab(null);
    }

    private void RefreshGroups()
    {
        string? current = GroupFilter.SelectedItem as string;
        var groups = new List<string> { AllGroups };
        groups.AddRange(_store.Categories);
        GroupFilter.ItemsSource = groups;
        GroupFilter.SelectedItem = current is not null && groups.Contains(current) ? current : AllGroups;
    }

    private void RefreshDevicesTab(string? select)
    {
        if (!_devicesReady) return;
        _devicesReady = false;
        RefreshGroups();
        _devicesReady = true;

        string search = DeviceSearch.Text.Trim();
        string group = GroupFilter.SelectedItem as string ?? AllGroups;
        var all = _store.All;

        var rows = all
            .Where(d => group == AllGroups || d.Category == group)
            .Where(d => search.Length == 0
                        || d.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                        || d.Note.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                        || d.Category.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(d => d.Category).ThenBy(d => d.Name)
            .Select(d => new DeviceRow(d, _store.SourceLabel(d)))
            .ToList();

        DeviceTable.ItemsSource = rows;
        DeviceCount.Text = rows.Count == all.Count ? $"{all.Count} devices" : $"{rows.Count} of {all.Count} devices";
        DeviceTable.SelectedItem = rows.FirstOrDefault(r => r.Name.Equals(select ?? "", StringComparison.OrdinalIgnoreCase));
        if (DeviceTable.SelectedItem is not null) DeviceTable.ScrollIntoView(DeviceTable.SelectedItem);
        SetDeleteButton(ListDeleteButton, (DeviceTable.SelectedItem as DeviceRow)?.Device);
    }

    private Device? SelectedListDevice => (DeviceTable.SelectedItem as DeviceRow)?.Device;

    private void DeviceFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_devicesReady) RefreshDevicesTab(SelectedListDevice?.Name);
    }

    private void DeviceTable_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SetDeleteButton(ListDeleteButton, SelectedListDevice);

    private void DeviceTable_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedListDevice is Device d) OpenEditor(d);
    }

    private void ListEdit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedListDevice is Device d) OpenEditor(d);
        else MessageBox.Show(this, "Click a device in the list first.", "PowerCheck");
    }

    private void ListDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedListDevice is Device d) DeleteDevice(d);
    }

    private void UseOnCord_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedListDevice is not Device d)
        {
            MessageBox.Show(this, "Click a device in the list first.", "PowerCheck");
            return;
        }
        var existing = _items.FirstOrDefault(i => i.Device.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) _items[_items.IndexOf(existing)] = existing with { Quantity = existing.Quantity + 1 };
        else _items.Add(new DeviceUse(d));
        Tabs.SelectedItem = CordTab;
    }

    private void Thumb_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not DeviceRow row) return;
        DeviceTable.SelectedItem = row;
        var image = DeviceStore.LoadImage(DeviceStore.ImagePath(row.Device));
        if (image is null)
        {
            if (MessageBox.Show(this, $"\"{row.Name}\" has no picture yet. Add one now?", "PowerCheck",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                OpenEditor(row.Device);
            return;
        }
        ShowPicture(this, row.Name, image);
    }

    internal static void ShowPicture(Window owner, string title, ImageSource image)
    {
        var viewer = new Window
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 820,
            Height = 640,
            Background = Brushes.White,
            ShowInTaskbar = false,
            Content = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(10) },
        };
        viewer.KeyDown += (_, k) => { if (k.Key == Key.Escape) viewer.Close(); };
        viewer.ShowDialog();
    }

    private void ShopLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Only web links are ever opened (Device.ShopUrl guarantees http/https).
        if (e.Uri.Scheme is "http" or "https")
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
