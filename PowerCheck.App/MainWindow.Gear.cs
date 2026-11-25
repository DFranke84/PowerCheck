using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>One row in the Strips, hubs &amp; cords section of the Devices tab.</summary>
public sealed class GearRow(Gear gear, string source)
{
    public Gear Gear { get; } = gear;
    public string Name => Gear.Name;
    public string Note => Gear.Note;
    public string Type => Gear.Type;
    public double Amps => Gear.Amps;
    public int Outlets => Gear.Outlets;
    public string Wire => (Gear.Gauge > 0 ? $"{Gear.Gauge} gauge" : "") +
                          (Gear.LengthFeet > 0 ? $"{(Gear.Gauge > 0 ? ", " : "")}{Gear.LengthFeet} ft" : "");
    public string Source { get; } = source;
    public ImageSource? Thumb { get; } = DeviceStore.LoadThumb(AsDevice(gear));
    public string ThumbTip => Thumb is null ? "No picture yet. Use Edit to add one." : "Click to see it bigger";
    public Uri? Url => Uri.TryCreate(Gear.Link ?? "", UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u : null;
    public string LinkText => Url is null ? "" : "Product page";

    /// <summary>The picture helpers work on devices; this gives them the gear's picture.</summary>
    public static Device AsDevice(Gear g) => new() { Name = g.Name, Watts = 1, Image = g.Image };
}

public partial class MainWindow
{
    private void InitGearTable()
    {
        _store.GearChanged += RefreshGearTable;
        RefreshGearTable(null);
    }

    private Gear? SelectedGearRow => (GearTable.SelectedItem as GearRow)?.Gear;

    private void RefreshGearTable(string? select)
    {
        var rows = _store.AllGear
            .Select(g => new GearRow(g, _store.IsMyGear(g) ? (_store.IsBuiltInGear(g.Name) ? "Edited" : "Yours") : "Built-in"))
            .ToList();
        GearTable.ItemsSource = rows;
        GearCount.Text = $"{rows.Count} item{(rows.Count == 1 ? "" : "s")}";
        GearTable.SelectedItem = rows.FirstOrDefault(r => r.Name.Equals(select ?? "", StringComparison.OrdinalIgnoreCase));
        if (GearTable.SelectedItem is not null) GearTable.ScrollIntoView(GearTable.SelectedItem);
        UpdateGearDeleteButton();
    }

    private void UpdateGearDeleteButton()
    {
        var g = SelectedGearRow;
        bool mine = g is not null && _store.IsMyGear(g);
        GearDeleteButton.IsEnabled = mine;
        GearDeleteButton.Content = mine && _store.IsBuiltInGear(g!.Name) ? "Undo my edit" : "Delete";
        GearDeleteButton.ToolTip = mine ? null : "Built-in items can be edited (that saves your own copy) but not deleted.";
    }

    private void GearTable_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateGearDeleteButton();

    private void GearTable_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedGearRow is Gear g) OpenGearEditor(g);
    }

    private void GearNew_Click(object sender, RoutedEventArgs e) => OpenGearEditor(null);

    private void GearEdit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGearRow is Gear g) OpenGearEditor(g);
        else MessageBox.Show(this, "Click a strip, hub or cord in the list first.", "PowerCheck");
    }

    private void OpenGearEditor(Gear? gear)
    {
        var editor = new GearEditor(gear) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        try
        {
            _store.SaveGear(editor.Result, gear);
            _store.RecycleImageIfUnused(editor.ReplacedImage);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save to {DeviceStore.MyGearPath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void GearDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGearRow is not Gear g || !_store.IsMyGear(g)) return;
        string question = _store.IsBuiltInGear(g.Name)
            ? $"Go back to the original values for \"{g.Name}\"?"
            : $"Delete \"{g.Name}\"?";
        if (MessageBox.Show(this, question, "PowerCheck", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { _store.RemoveGear(g); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save to {DeviceStore.MyGearPath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void GearThumb_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not GearRow row) return;
        GearTable.SelectedItem = row;
        var image = DeviceStore.LoadImage(DeviceStore.ImagePath(GearRow.AsDevice(row.Gear)));
        if (image is null)
        {
            if (MessageBox.Show(this, $"\"{row.Name}\" has no picture yet. Add one now?", "PowerCheck",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                OpenGearEditor(row.Gear);
            return;
        }
        ShowPicture(this, row.Name, image);
    }
}
