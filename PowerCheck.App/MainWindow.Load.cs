using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>One line on the Power strip / circuit tab. Quantity and "on together" can be changed in place.</summary>
public sealed class LoadRow : INotifyPropertyChanged
{
    private Device _device;
    private int _quantity;
    private bool _onAtSameTime = true;

    public LoadRow(Device device, int quantity)
    {
        _device = device;
        _quantity = quantity;
    }

    public Device Device
    {
        get => _device;
        set { _device = value; Changed(); Changed(nameof(Watts)); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = Math.Clamp(value, 0, 100); Changed(); Changed(nameof(Watts)); }
    }

    public bool OnAtSameTime
    {
        get => _onAtSameTime;
        set { _onAtSameTime = value; Changed(); Changed(nameof(Watts)); }
    }

    public double Watts => OnAtSameTime ? Device.Watts * Quantity : 0;

    public DeviceUse ToUse() => new(Device, Quantity, OnAtSameTime);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class MainWindow
{
    private readonly ObservableCollection<LoadRow> _loadRows = [];
    private bool _loadReady;

    private void InitLoadTab()
    {
        SrcCordGauge.ItemsSource = CordGauge.All;
        SrcCordGauge.DisplayMemberPath = nameof(CordGauge.Label);
        SrcCordGauge.SelectedItem = CordGauge.All.Single(g => g.Awg == 14);

        LightType.ItemsSource = Core.LightType.All;
        LightType.SelectedIndex = 0;

        LoadItems.ItemsSource = _loadRows;
        _loadRows.CollectionChanged += (_, e) =>
        {
            foreach (LoadRow r in e.NewItems ?? Array.Empty<LoadRow>()) r.PropertyChanged += (_, _) => RecalcLoad();
            RecalcLoad();
        };

        RefreshLoadPicker(null);
        _store.GearChanged += RefreshGear;
        RefreshGear(null);
        _loadReady = true;
        RecalcLoad();
        RecalcLights();
    }

    private void RefreshLoadPicker(string? select)
    {
        var current = select ?? (LoadPicker.SelectedItem as Device)?.Name;
        var all = _store.FavoritesFirst;
        LoadPicker.ItemsSource = all;
        LoadPicker.SelectedItem = all.FirstOrDefault(d => d.Name.Equals(current ?? "", StringComparison.OrdinalIgnoreCase))
                                  ?? all.FirstOrDefault();
    }

    /// <summary>Keeps rows on this tab in step when a device is edited, renamed or removed.</summary>
    private void UpdateLoadRows(string? oldName, Device? device)
    {
        if (oldName is null || device is null) return;
        foreach (var r in _loadRows.Where(r => r.Device.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
            r.Device = device;
    }

    private PowerSource SelectedSource()
    {
        if (SrcCord.IsChecked == true && SrcCordGauge.SelectedItem is CordGauge g) return PowerSource.Cord(g);
        if (SrcCircuit15.IsChecked == true) return PowerSource.Circuit15;
        if (SrcCircuit20.IsChecked == true) return PowerSource.Circuit20;
        if (SrcGear.IsChecked == true && GearPick.SelectedItem is Gear gear) return gear.ToSource();
        return PowerSource.PowerStrip;
    }

    private void Load_Changed(object sender, RoutedEventArgs e)
    {
        if (sender == SrcCordGauge && _loadReady) SrcCord.IsChecked = true;
        RecalcLoad();
    }

    private void AddLoad(Device d, int qty)
    {
        var existing = _loadRows.FirstOrDefault(r => r.Device.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) existing.Quantity += qty;
        else _loadRows.Add(new LoadRow(d, qty));
    }

    private void LoadAdd_Click(object sender, RoutedEventArgs e)
    {
        if (LoadPicker.SelectedItem is not Device d) return;
        int qty = int.TryParse(LoadQty.Text, out var q) && q > 0 ? Math.Min(q, 100) : 1;
        AddLoad(d, qty);
    }

    private void LoadRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LoadRow r) _loadRows.Remove(r);
    }

    private void LoadClear_Click(object sender, RoutedEventArgs e) => _loadRows.Clear();

    private void RecalcLoad()
    {
        if (!_loadReady) return;
        var source = SelectedSource();

        if (_loadRows.Count == 0)
        {
            LoadBanner.Background = Brush("#E9ECF0");
            LoadBanner.BorderThickness = new Thickness(0);
            LoadTitle.Text = "Add what's plugged in";
            LoadDetail.Text = $"Checking against: {source.Name}, {source.Watts:0} W.";
            LoadBar.Value = 0;
            LoadNumbers.Text = "";
            LoadNotes.ItemsSource = null;
            return;
        }

        var report = LoadChecker.Check(_loadRows.Select(r => r.ToUse()), source);
        LoadBanner.Background = Light(report.Verdict);
        LoadBanner.BorderBrush = Dot(report.Verdict);
        LoadBanner.BorderThickness = new Thickness(4, 0, 0, 0);
        LoadTitle.Text = report.Verdict switch
        {
            Verdict.Green => "OK",
            Verdict.Yellow => "Near the limit",
            _ => "Overloaded"
        };
        LoadDetail.Text = report.Headline;

        LoadBar.Value = Math.Min(report.Share * 100, 125);
        LoadBar.Foreground = Dot(report.Verdict);
        int plugs = _loadRows.Sum(r => r.Quantity);
        LoadNumbers.Text = $"{report.Watts:0} W of {source.Watts:0} W  ({report.Amps:0.0} A of {source.Amps:0} A), " +
                           $"{plugs} plug{(plugs == 1 ? "" : "s")}. Room left: {Math.Max(0, source.Watts - report.Watts):0} W.";
        LoadNotes.ItemsSource = report.Notes;
    }

    // ---- Your own strips, hubs and cords ----

    private void RefreshGear(string? select)
    {
        var current = select ?? (GearPick.SelectedItem as Gear)?.Name;
        var all = _store.AllGear;
        GearPick.ItemsSource = all;
        GearPick.SelectedItem = all.FirstOrDefault(g => g.Name.Equals(current ?? "", StringComparison.OrdinalIgnoreCase))
                                ?? all.FirstOrDefault();
        ShowGearDetails();
        RecalcLoad();
    }

    private void ShowGearDetails()
    {
        var g = GearPick.SelectedItem as Gear;
        GearThumb.Source = g is null ? null
            : DeviceStore.LoadThumb(new Device { Name = g.Name, Watts = 1, Image = g.Image });
        GearNote.Text = g is null
            ? "Add your own power strips, hubs, splitters and cords with New."
            : $"{g.Type}, {g.Amps:0.#} A, {g.Outlets} outlet{(g.Outlets == 1 ? "" : "s")}. {g.Note}".Trim();
        GearDeleteButton.IsEnabled = g is not null && _store.IsMyGear(g);
        GearDeleteButton.Content = g is not null && _store.IsMyGear(g) && _store.IsBuiltInGear(g.Name) ? "Undo my edit" : "Delete";
        GearLink.NavigateUri = g?.Link is string l && Uri.TryCreate(l, UriKind.Absolute, out var u) ? u : null;
        GearLink.IsEnabled = GearLink.NavigateUri is not null;
    }

    private void GearPick_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadReady) return;
        ShowGearDetails();
        if (GearPick.SelectedItem is not null) SrcGear.IsChecked = true;
        RecalcLoad();
    }

    private void GearNew_Click(object sender, RoutedEventArgs e) => OpenGearEditor(null);

    private void GearEdit_Click(object sender, RoutedEventArgs e)
    {
        if (GearPick.SelectedItem is Gear g) OpenGearEditor(g);
    }

    private void OpenGearEditor(Gear? gear)
    {
        var editor = new GearEditor(gear) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        try
        {
            _store.SaveGear(editor.Result, gear);
            _store.RecycleImageIfUnused(editor.ReplacedImage);
            SrcGear.IsChecked = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save to {DeviceStore.MyGearPath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void GearDelete_Click(object sender, RoutedEventArgs e)
    {
        if (GearPick.SelectedItem is not Gear g || !_store.IsMyGear(g)) return;
        string question = _store.IsBuiltInGear(g.Name)
            ? $"Go back to the original values for \"{g.Name}\"?"
            : $"Delete \"{g.Name}\" from your strips and cords?";
        if (MessageBox.Show(this, question, "PowerCheck", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { _store.RemoveGear(g); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save to {DeviceStore.MyGearPath}.\n\n{ex.Message}", "PowerCheck");
        }
    }

    private void GearPicture_Click(object sender, RoutedEventArgs e)
    {
        if (GearPick.SelectedItem is not Gear g) return;
        var image = DeviceStore.LoadImage(DeviceStore.ImagePath(new Device { Name = g.Name, Watts = 1, Image = g.Image }));
        if (image is null)
        {
            if (MessageBox.Show(this, $"\"{g.Name}\" has no picture yet. Add one now?", "PowerCheck",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                OpenGearEditor(g);
            return;
        }
        ShowPicture(this, g.Name, image);
    }

    // ---- Christmas lights helper ----

    private void LightType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LightType.SelectedItem is not Core.LightType t) return;
        LightWatts.Text = t.WattsPerString.ToString("0.#", CultureInfo.CurrentCulture);
        LightMax.Text = t.TypicalMaxConnect.ToString(CultureInfo.CurrentCulture);
        RecalcLights();
    }

    private void Light_Changed(object sender, RoutedEventArgs e) => RecalcLights();

    private void RecalcLights()
    {
        if (!_loadReady) return;
        bool ok = double.TryParse(LightWatts.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var w) & w > 0;
        ok &= int.TryParse(LightCount.Text, out var count) & count > 0;
        ok &= int.TryParse(LightMax.Text, out var max) & max > 0;
        ok &= int.TryParse(LightFeet.Text, out var feet) & feet > 0;
        if (!ok)
        {
            LightBanner.Background = Brush("#E9ECF0");
            LightChainText.Text = "Fill in all the numbers above.";
            LightCordText.Text = "";
            return;
        }

        var r = LightChain.Check(w, count, max, Math.Min(feet, 1000));
        var worst = Electric.Worst(r.ChainVerdict, r.Cord.Overall);
        LightBanner.Background = Light(worst);
        LightChainText.Text = r.ChainAdvice;
        LightCordText.Text = $"All {count} strings: {r.Watts:0} W ({Electric.WattsToAmps(r.Watts):0.0} A). " +
                             (r.Cord.Recommended is null
                                 ? r.Cord.Headline
                                 : $"Cord for {feet} ft: {r.Cord.Headline}");
    }

    // ---- Ready-made examples ----

    private Device? Find(string name) =>
        _store.All.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void LoadExample(PowerSource source, params (string Name, int Qty)[] items)
    {
        _loadRows.Clear();
        foreach (var (name, qty) in items)
            if (Find(name) is Device d) _loadRows.Add(new LoadRow(d, qty));
        SrcStrip.IsChecked = source.Kind == SourceKind.PowerStrip;
        SrcCircuit15.IsChecked = source == PowerSource.Circuit15;
        SrcCircuit20.IsChecked = source == PowerSource.Circuit20;
        RecalcLoad();
    }

    private void ExampleGarage_Click(object sender, RoutedEventArgs e) =>
        LoadExample(PowerSource.PowerStrip,
            ("DEWALT 4 A waterproof charger/maintainer (DXAEWPC4)", 1),
            ("BLACK+DECKER 2 A waterproof charger/maintainer (BC2WBD)", 2),
            ("Heated pet bed", 1));

    private void ExampleWorkshop_Click(object sender, RoutedEventArgs e) =>
        LoadExample(PowerSource.Circuit15,
            ("Miter saw", 1),
            ("Shop vac", 1),
            ("DEWALT 8 A fast charger (DCB118)", 1),
            ("LED work light", 1));

    private void ExampleLights_Click(object sender, RoutedEventArgs e)
    {
        LightsExpander.IsExpanded = true;
        LightType.SelectedItem = Core.LightType.All.First(t => t.Name.StartsWith("Incandescent C9"));
        LightCount.Text = "4";
        LightFeet.Text = "100";
        LightsExpander.BringIntoView();
    }
}
