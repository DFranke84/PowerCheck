using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>One device at the end of the chain. Quantity and "runs" can be changed in place.</summary>
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

/// <summary>A choice in the "Add next" list.</summary>
public sealed record ChainOption(string Label, Gear? Gear = null, bool IsCord = false, bool IsStrip = false)
{
    public override string ToString() => Label;
}

/// <summary>A link as shown in the Step 2 list.</summary>
public sealed record ChainRow(int Number, ChainLink Link)
{
    public string Text => $"{Link.Name}  ({Link.Limit:0.#} A, {Link.Outlets} outlet{(Link.Outlets == 1 ? "" : "s")})";
}

/// <summary>A link as shown in the answer on the right.</summary>
public sealed record LinkRow(string Name, string Reason, Brush Brush, double Percent);

public partial class MainWindow
{
    private readonly ObservableCollection<LoadRow> _loadRows = [];
    private readonly List<ChainLink> _chain = [];
    private bool _loadReady;

    private void InitLoadTab()
    {
        ChainCordGauge.ItemsSource = CordGauge.All;
        ChainCordGauge.DisplayMemberPath = nameof(CordGauge.Label);
        ChainCordGauge.SelectedItem = CordGauge.All.Single(g => g.Awg == 14);

        LightType.ItemsSource = Core.LightType.All;
        LightType.SelectedIndex = 0;

        LoadItems.ItemsSource = _loadRows;
        _loadRows.CollectionChanged += (_, e) =>
        {
            foreach (LoadRow r in e.NewItems ?? Array.Empty<LoadRow>()) r.PropertyChanged += (_, _) => RecalcLoad();
            RecalcLoad();
        };

        RefreshLoadPicker(null);
        _store.GearChanged += RefreshChainOptions;
        RefreshChainOptions(null);
        _loadReady = true;
        ShowChain();
        RecalcLights();
    }

    // ---- Step 2: the chain ----

    private void RefreshChainOptions(string? selectGear)
    {
        var options = new List<ChainOption>
        {
            new("Extension cord (pick its size and length)", IsCord: true),
            new("Power strip (15 A, 6 outlets)", IsStrip: true),
        };
        options.AddRange(_store.AllGear.Select(g => new ChainOption(g.Name, g)));

        string? current = selectGear ?? (ChainAddPick.SelectedItem as ChainOption)?.Gear?.Name;
        ChainAddPick.ItemsSource = options;
        ChainAddPick.SelectedItem = options.FirstOrDefault(o => o.Gear is not null && current is not null &&
                                                                o.Gear.Name.Equals(current, StringComparison.OrdinalIgnoreCase))
                                    ?? options[0];
        ShowAddOption();
    }

    private void ShowAddOption()
    {
        var o = ChainAddPick.SelectedItem as ChainOption;
        ChainCordFields.Visibility = o?.IsCord == true ? Visibility.Visible : Visibility.Collapsed;
        ChainAddNote.Text = o?.Gear is Gear g
            ? $"{g.Type}, {g.Amps:0.#} A, {g.Outlets} outlet{(g.Outlets == 1 ? "" : "s")}. {g.Note}".Trim()
            : o?.IsStrip == true ? "A typical strip. To list your own, add it on the Devices tab under Strips, hubs & cords." : "";
    }

    private void ChainAddPick_Changed(object sender, SelectionChangedEventArgs e) => ShowAddOption();

    private void ChainAdd_Click(object sender, RoutedEventArgs e)
    {
        switch (ChainAddPick.SelectedItem)
        {
            case ChainOption { IsCord: true }:
                if (ChainCordGauge.SelectedItem is not CordGauge g || !int.TryParse(ChainCordFeet.Text, out var ft) || ft <= 0)
                {
                    MessageBox.Show(this, "Pick the cord's gauge and type its length in feet.", "PowerCheck");
                    return;
                }
                _chain.Add(ChainLink.ExtensionCord(g, Math.Min(ft, 1000)));
                break;
            case ChainOption { IsStrip: true }:
                _chain.Add(ChainLink.GenericStrip());
                break;
            case ChainOption { Gear: Gear gear }:
                _chain.Add(ChainLink.From(gear));
                break;
            default:
                return;
        }
        ShowChain();
    }

    private void ChainRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ChainRow row) _chain.RemoveAt(row.Number - 1);
        ShowChain();
    }

    private void ChainUp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChainRow row || row.Number < 2) return;
        int i = row.Number - 1;
        (_chain[i - 1], _chain[i]) = (_chain[i], _chain[i - 1]);
        ShowChain();
    }

    private void ShowChain()
    {
        ChainList.ItemsSource = _chain.Select((l, i) => new ChainRow(i + 1, l)).ToList();
        ChainEmpty.Visibility = _chain.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecalcLoad();
    }

    private void Chain_Changed(object sender, RoutedEventArgs e) => RecalcLoad();

    // ---- Step 3: the devices ----

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

    private void LoadAdd_Click(object sender, RoutedEventArgs e)
    {
        if (LoadPicker.SelectedItem is not Device d) return;
        int qty = int.TryParse(LoadQty.Text, out var q) && q > 0 ? Math.Min(q, 100) : 1;
        var existing = _loadRows.FirstOrDefault(r => r.Device.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) existing.Quantity += qty;
        else _loadRows.Add(new LoadRow(d, qty));
    }

    /// <summary>Adds a one-off device from the numbers on its label (not saved to the list).</summary>
    private void LoadLabelAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(LoadLabelValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value <= 0)
        {
            MessageBox.Show(this, "Type the number from the label, for example 1500 or 12.5.", "PowerCheck");
            return;
        }
        bool isAmps = LoadLabelUnit.SelectedIndex == 1;
        string unit = isAmps ? "A" : LoadLabelUnit.SelectedIndex == 2 ? "VA" : "W";
        _loadRows.Add(new LoadRow(new Device
        {
            Name = $"Label: {value:0.##} {unit}",
            Watts = isAmps ? Electric.AmpsToWatts(value) : value,
            Continuous = LoadLabelContinuous.IsChecked == true,
        }, 1));
        LoadLabelValue.Clear();
    }

    private void LoadRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LoadRow r) _loadRows.Remove(r);
    }

    // ---- The answer ----

    private void RecalcLoad()
    {
        if (!_loadReady) return;
        double breaker = Wall20.IsChecked == true ? 20 : 15;
        var report = ChainCheck.Check(breaker, _chain, _loadRows.Select(r => r.ToUse()).ToList());

        if (_loadRows.Count == 0)
        {
            LoadBanner.Background = Brush("#E9ECF0");
            LoadBanner.BorderThickness = new Thickness(0);
            LoadTitle.Text = "Add the devices in Step 3";
            LoadDetail.Text = "The answer shows here, with a check of every link from the wall to the devices.";
        }
        else
        {
            LoadBanner.Background = Light(report.Verdict);
            LoadBanner.BorderBrush = Dot(report.Verdict);
            LoadBanner.BorderThickness = new Thickness(4, 0, 0, 0);
            LoadTitle.Text = report.Verdict switch { Verdict.Green => "OK", Verdict.Yellow => "Near the limit", _ => "Too much" };
            LoadDetail.Text = report.Headline;
        }

        var rows = report.Links.Select(l => new LinkRow(l.Name, l.Reason, Dot(l.Verdict), Math.Min(l.Share * 100, 125))).ToList();
        int plugs = _loadRows.Sum(r => r.Quantity);
        rows.Add(new LinkRow($"{plugs} device{(plugs == 1 ? "" : "s")} at the end",
            $"{report.Watts:0} W in total ({report.Amps:0.0} A).", Brush("#7A828C"), 0));
        LinkRows.ItemsSource = rows;
        LoadNotes.ItemsSource = _loadRows.Count == 0 ? null : report.Notes;
    }

    // ---- Christmas lights helper (on the Extension cord tab) ----

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
                             (r.Cord.Recommended is null ? r.Cord.Headline : $"Cord for {feet} ft: {r.Cord.Headline}");
    }
}
