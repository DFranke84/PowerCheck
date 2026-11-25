using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>Draws each tab with example data into PNG files (for the README), off-screen.</summary>
public partial class MainWindow
{
    public async Task MakeScreenshots(string folder)
    {
        Directory.CreateDirectory(folder);
        Width = 1220;
        Height = 780;
        Show();
        await Settle();

        // 1. Extension cord: a leaf blower on 100 ft outdoors shows every colour.
        Tabs.SelectedItem = CordTab;
        _items.Clear();
        if (FindDevice("Leaf blower (corded)") is Device blower) _items.Add(new DeviceUse(blower));
        Len100.IsChecked = true;
        Outdoor.IsChecked = true;
        await Save(folder, "extension-cord.png");

        // 2. Christmas lights helper on the same tab (taller window so it fits).
        LightsExpander.IsExpanded = true;
        LightType.SelectedItem = Core.LightType.All.First(t => t.Name.Contains("C6, 200"));
        LightCount.Text = "20";
        LightFeet.Text = "100";
        Height = 1080;
        await Save(folder, "christmas-lights.png");
        LightsExpander.IsExpanded = false;
        Height = 780;

        // 3. Power strip / circuit: wall -> 50 ft 12 gauge cord -> 5-outlet hub -> garage chargers.
        Tabs.SelectedItem = LoadTab;
        Height = 940;
        _chain.Clear();
        _chain.Add(ChainLink.ExtensionCord(CordGauge.All.Single(g => g.Awg == 12), 50));
        if (_store.AllGear.FirstOrDefault(g => g.Name.StartsWith("VANGUARD")) is Gear hub) _chain.Add(ChainLink.From(hub));
        ShowChain();
        _loadRows.Clear();
        foreach (var (name, qty) in new[]
                 {
                     ("DEWALT 4 A waterproof charger/maintainer (DXAEWPC4)", 1),
                     ("BLACK+DECKER 2 A waterproof charger/maintainer (BC2WBD)", 2),
                     ("Heated pet bed", 1),
                 })
            if (FindDevice(name) is Device d) _loadRows.Add(new LoadRow(d, qty));
        await Save(folder, "power-strip-chain.png");

        // 4. The same chain with too much on it: a shop vac and a miter saw on a 16 gauge cord.
        _chain.Clear();
        _chain.Add(ChainLink.ExtensionCord(CordGauge.All.Single(g => g.Awg == 16), 50));
        _chain.Add(ChainLink.GenericStrip());
        ShowChain();
        _loadRows.Clear();
        foreach (var name in new[] { "Shop vac", "Miter saw" })
            if (FindDevice(name) is Device d) _loadRows.Add(new LoadRow(d, 1));
        await Save(folder, "power-strip-overloaded.png");

        Height = 780;

        // 5. Devices tab.
        Tabs.SelectedItem = DevicesTab;
        GroupFilter.SelectedItem = "Snow & winter";
        await Save(folder, "devices.png");

        // 6. Learn tab with two topics open and one warning ticked.
        Tabs.SelectedItem = LearnTab;
        await Settle();
        int opened = 0;
        foreach (var exp in FindAll<Expander>(LearnTopics))
            if (opened++ < 2) exp.IsExpanded = true;
        if (FindAll<CheckBox>(WarningList).FirstOrDefault() is CheckBox warm) warm.IsChecked = true;
        await Save(folder, "learn.png");

        Close();
    }

    private Device? FindDevice(string name) =>
        _store.All.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private async Task Save(string folder, string file)
    {
        await Settle();
        var root = (FrameworkElement)Content;
        int w = (int)Math.Ceiling(root.ActualWidth), h = (int)Math.Ceiling(root.ActualHeight);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
        }
        bmp.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(folder, file));
        enc.Save(fs);
    }

    /// <summary>Lets layout, bindings and pictures finish before drawing.</summary>
    private async Task Settle()
    {
        UpdateLayout();
        for (int i = 0; i < 3; i++)
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(150);
        UpdateLayout();
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var inner in FindAll<T>(child)) yield return inner;
        }
    }
}
