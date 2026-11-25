using PowerCheck.Core;

namespace PowerCheck.Tests;

public class RuleTests
{
    private static readonly Device Heater = new() { Name = "Space heater", Watts = 1500, Continuous = true, WallOnly = true };
    private static readonly Device ShopVac = new() { Name = "Shop vac", Watts = 1560, Motor = true };
    private static readonly Device CoffeeMaker = new() { Name = "Coffee maker", Watts = 900 };
    private static readonly Device Maintainer = new() { Name = "Battery maintainer", Watts = 25, Continuous = true };
    private static readonly Device PetBed = new() { Name = "Heated pet bed", Watts = 40, Continuous = true };

    private static GaugeResult Gauge(CordReport r, int awg) => r.Gauges.Single(g => g.Gauge.Awg == awg);

    [Fact]
    public void Watts_and_amps_convert_at_120_volts()
    {
        Assert.Equal(12.5, Electric.WattsToAmps(1500), 3);
        Assert.Equal(1800, Electric.AmpsToWatts(15), 3);
    }

    [Fact]
    public void Space_heater_50ft_needs_12_gauge_and_warns_to_use_the_wall()
    {
        var r = CordSizer.Size([new DeviceUse(Heater)], 50, outdoor: false);
        Assert.Equal(12, r.Recommended!.Gauge.Awg);
        Assert.Equal(Verdict.Yellow, Gauge(r, 14).Verdict); // 83 % of 15 A, too close for hours
        Assert.Equal(Verdict.Red, r.Overall);               // wall-only device
        Assert.Contains(r.Notes, n => n.Contains("wall outlet"));
    }

    [Fact]
    public void Small_lights_100ft_are_fine_on_16_gauge()
    {
        var r = CordSizer.Size(Electric.WattsToAmps(100), 100, outdoor: true, continuous: true);
        Assert.Equal(16, r.Recommended!.Gauge.Awg);
        Assert.Equal(Verdict.Green, r.Overall);
    }

    [Fact]
    public void Shop_vac_100ft_is_too_far_for_14_gauge()
    {
        var r = CordSizer.Size([new DeviceUse(ShopVac)], 100, outdoor: false);
        Assert.Equal(Verdict.Red, Gauge(r, 14).Verdict);      // about 5.5 % drop
        Assert.Equal(Verdict.Yellow, Gauge(r, 12).Verdict);   // about 3.4 % drop
        Assert.Equal(10, r.Recommended!.Gauge.Awg);
        Assert.Contains(r.Notes, n => n.Contains("Motors"));
    }

    [Fact]
    public void Too_many_amps_for_any_cord_is_red()
    {
        var r = CordSizer.Size(18, 25, outdoor: false, continuous: false);
        Assert.Null(r.Recommended);
        Assert.Equal(Verdict.Red, r.Overall);
        Assert.All(r.Gauges, g => Assert.Equal(0, g.MaxFeet));
    }

    [Fact]
    public void Max_length_matches_voltage_drop_limit()
    {
        var g14 = CordGauge.All.Single(g => g.Awg == 14);
        int max = CordSizer.MaxFeetFor(g14, 10, 5);
        Assert.InRange(CordSizer.DropPercent(g14, 10, max), 4.9, 5.0);
        Assert.True(CordSizer.DropPercent(g14, 10, max + 1) > 5.0);
    }

    [Fact]
    public void Heater_and_coffee_maker_overload_a_15A_circuit()
    {
        var r = LoadChecker.Check([new DeviceUse(Heater), new DeviceUse(CoffeeMaker)], PowerSource.Circuit15);
        Assert.Equal(Verdict.Red, r.Verdict);
        Assert.Contains(r.Notes, n => n.Contains("Space heater") && n.Contains("different circuit"));
    }

    [Fact]
    public void Device_not_on_at_same_time_is_not_counted()
    {
        var r = LoadChecker.Check([new DeviceUse(Heater), new DeviceUse(CoffeeMaker, 1, OnAtSameTime: false)], PowerSource.Circuit20);
        Assert.Equal(1500, r.Watts, 3);
        Assert.Equal(Verdict.Green, r.Verdict);
    }

    [Fact]
    public void Each_source_gets_its_own_tip()
    {
        DeviceUse[] two = [new(Maintainer, 2)];
        Assert.Contains(LoadChecker.Check(two, PowerSource.PowerStrip).Notes, n => n.Contains("straight into a wall outlet"));
        Assert.Contains(LoadChecker.Check(two, PowerSource.Circuit15).Notes, n => n.Contains("same breaker"));
        Assert.Contains(LoadChecker.Check(two, PowerSource.Cord(CordGauge.All[0])).Notes, n => n.Contains("power block"));
        Assert.Contains(LoadChecker.Check([new(Maintainer, 8)], PowerSource.PowerStrip).Notes, n => n.Contains("8 plugs"));
    }

    [Fact]
    public void Busy_workshop_circuit_warns_about_motor_starts()
    {
        var saw = new Device { Name = "Miter saw", Watts = 1800, Motor = true };
        var r = LoadChecker.Check([new(saw), new(ShopVac)], PowerSource.Circuit20);
        Assert.Equal(Verdict.Red, r.Verdict);
        Assert.Contains(r.Notes, n => n.Contains("one at a time"));
    }

    [Fact]
    public void Too_many_plugs_for_a_hub_is_yellow_even_when_power_is_fine()
    {
        var splitter = new Gear { Name = "Splitter", Type = "Splitter cord", Amps = 15, Outlets = 2 };
        var r = LoadChecker.Check([new(Maintainer, 3)], splitter.ToSource());
        Assert.Equal(Verdict.Yellow, r.Verdict);
        Assert.StartsWith("That's 3 plugs, but it only has 2 outlets", r.Notes[0]);
        Assert.Contains(r.Notes, n => n.Contains("Never stack adapters"));
    }

    [Fact]
    public void Gear_rating_never_goes_above_the_plug_or_the_cord_wire()
    {
        Assert.Equal(15, new Gear { Name = "Strip", Amps = 20 }.ToSource().Amps);
        Assert.Equal(13, new Gear { Name = "Cord", Type = "Extension cord", Amps = 15, Gauge = 16 }.ToSource().Amps);
        Assert.Equal(SourceKind.Adapter, new Gear { Name = "Hub", Type = "Power hub / outlet adapter" }.Kind);
    }

    // ---- Chain: wall outlet -> cords / strips / hubs -> devices ----

    private static readonly ChainLink Hub = new() { Name = "Hub", Kind = SourceKind.Adapter, Amps = 15, Outlets = 5 };
    private static readonly ChainLink Splitter = new() { Name = "Splitter", Kind = SourceKind.Adapter, Amps = 13, Outlets = 2, Gauge = 16 };

    [Fact]
    public void Garage_chain_through_the_hub_is_green()
    {
        var dewalt = new Device { Name = "DeWalt", Watts = 144, Continuous = true };
        var bd = new Device { Name = "B+D", Watts = 60, Continuous = true };
        var r = ChainCheck.Check(15, [Hub], [new(dewalt), new(bd, 2), new(PetBed)]);
        Assert.Equal(Verdict.Green, r.Verdict);
        Assert.Equal(2, r.Links.Count); // wall + hub
    }

    [Fact]
    public void Four_plugs_on_a_two_outlet_splitter_is_yellow()
    {
        var r = ChainCheck.Check(15, [Splitter], [new(Maintainer, 3), new(PetBed)]);
        Assert.Equal(Verdict.Yellow, r.Verdict);
        Assert.StartsWith("4 plugs but only 2 outlets", r.Notes[0]);
        Assert.Contains("more plugs than outlets", r.Headline);
    }

    [Fact]
    public void Long_thin_cord_is_the_weak_link()
    {
        var cord = ChainLink.ExtensionCord(CordGauge.All.Single(g => g.Awg == 14), 100);
        var r = ChainCheck.Check(20, [cord], [new(ShopVac)]);
        Assert.Equal(Verdict.Red, r.Verdict);
        Assert.Equal(Verdict.Red, r.Links[1].Verdict);
        Assert.Contains("100 ft 14 gauge", r.Headline);
    }

    [Fact]
    public void Strip_plugged_into_a_cord_and_stacked_adapters_get_warnings()
    {
        var cord = ChainLink.ExtensionCord(CordGauge.All.Single(g => g.Awg == 12), 25);
        var r = ChainCheck.Check(15, [cord, ChainLink.GenericStrip(), Hub, Splitter], [new(Maintainer)]);
        Assert.Contains(r.Notes, n => n.Contains("plugged into an extension cord"));
        Assert.Contains(r.Notes, n => n.Contains("stacked on another adapter"));
    }

    [Fact]
    public void Wall_only_device_through_a_chain_is_red()
    {
        var r = ChainCheck.Check(20, [ChainLink.GenericStrip()], [new(Heater)]);
        Assert.Equal(Verdict.Red, r.Verdict);
        Assert.StartsWith("Plug the Space heater straight into the wall", r.Headline);
    }

    [Fact]
    public void Two_big_saws_overload_even_a_20A_circuit()
    {
        var saw = new Device { Name = "Saw", Watts = 1800, Motor = true };
        var r = ChainCheck.Check(20, [], [new(saw, 2)]);
        Assert.Equal(Verdict.Red, r.Verdict);
        Assert.Contains(r.Notes, n => n.Contains("15 A through one ordinary plug"));
    }

    [Fact]
    public void Cord_source_is_capped_at_the_15A_plug()
    {
        Assert.Equal(15, PowerSource.Cord(CordGauge.All.Single(g => g.Awg == 12)).Amps);
        Assert.Equal(13, PowerSource.Cord(CordGauge.All.Single(g => g.Awg == 16)).Amps);
    }

    [Fact]
    public void Garage_chargers_and_pet_bed_are_green_on_16_gauge_50ft()
    {
        DeviceUse[] garage = [new(Maintainer, 4), new(PetBed)];
        var load = LoadChecker.Check(garage, PowerSource.PowerStrip);
        Assert.Equal(Verdict.Green, load.Verdict);
        Assert.InRange(load.Watts, 100, 200);

        var cord = CordSizer.Size(garage, 50, outdoor: true);
        Assert.Equal(16, cord.Recommended!.Gauge.Awg);
        Assert.Equal(Verdict.Green, cord.Overall);
    }

    [Fact]
    public void Four_C9_strings_at_100ft_need_14_gauge()
    {
        var r = LightChain.Check(175, 4, maxConnect: 4, cordFeet: 100);
        Assert.Equal(700, r.Watts, 3);
        Assert.Equal(Verdict.Yellow, Gauge(r.Cord, 16).Verdict); // about 3.9 % drop
        Assert.Equal(14, r.Cord.Recommended!.Gauge.Awg);
    }

    [Fact]
    public void Twenty_LED_strings_chain_fine_on_16_gauge()
    {
        var r = LightChain.Check(6, 20, maxConnect: 40, cordFeet: 100);
        Assert.Equal(Verdict.Green, r.ChainVerdict);
        Assert.Equal(1, r.RunsNeeded);
        Assert.Equal(16, r.Cord.Recommended!.Gauge.Awg);
    }

    [Fact]
    public void More_strings_than_the_tag_allows_are_split_into_runs()
    {
        var r = LightChain.Check(40, 5, maxConnect: 3, cordFeet: 25);
        Assert.Equal(2, r.RunsNeeded);
        Assert.Equal(Verdict.Yellow, r.ChainVerdict);
        Assert.Equal(Verdict.Red, LightChain.ChainAsConnected(5, 3));
    }

    [Fact]
    public void Device_list_round_trips_through_json()
    {
        string path = Path.Combine(Path.GetTempPath(), $"pc-devices-{Guid.NewGuid():N}.json");
        try
        {
            DeviceLibrary.Save(path, [Heater, Maintainer]);
            var back = DeviceLibrary.Load(path);
            Assert.Equal(2, back.Count);
            Assert.True(back[0].WallOnly);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Your_device_replaces_built_in_one_with_same_name()
    {
        var mine = new Device { Name = "space heater", Watts = 750, Continuous = true };
        var merged = DeviceLibrary.Merge([Heater, ShopVac], [mine]);
        Assert.Equal(2, merged.Count);
        Assert.Equal(750, merged.Single(d => d.Name.Equals("space heater", StringComparison.OrdinalIgnoreCase)).Watts);
    }

    [Fact]
    public void Renaming_your_device_does_not_leave_the_old_one()
    {
        var list = DeviceLibrary.Upsert([Maintainer], Maintainer with { Name = "ATV maintainer" }, oldName: "Battery maintainer");
        Assert.Single(list);
        Assert.Equal("ATV maintainer", list[0].Name);
    }

    [Fact]
    public void Several_plugs_get_connection_advice_one_plug_does_not()
    {
        var one = CordSizer.Size([new DeviceUse(Maintainer)], 25, outdoor: true);
        Assert.DoesNotContain(one.Notes, n => n.StartsWith("How to connect"));

        var garage = CordSizer.Size([new DeviceUse(Maintainer, 3), new DeviceUse(PetBed)], 25, outdoor: true);
        Assert.Contains(garage.Notes, n => n.StartsWith("How to connect 4 plugs"));
        Assert.Contains(garage.Notes, n => n.Contains("power strip into an extension cord"));
        Assert.Contains(garage.Notes, n => n.Contains("off the floor"));
    }

    [Fact]
    public void Shop_link_is_amazon_search_unless_you_give_a_web_link()
    {
        Assert.Equal("https://www.amazon.com/s?k=Space%20heater", Heater.ShopUrl);
        Assert.False(Heater.HasOwnLink);

        var mine = Heater with { Link = "https://example.com/heater" };
        Assert.Equal("https://example.com/heater", mine.ShopUrl);
        Assert.True(mine.HasOwnLink);

        // Anything that isn't a web link falls back to the search.
        Assert.False((Heater with { Link = "file:///C:/Windows/notepad.exe" }).HasOwnLink);
    }

    [Fact]
    public void Shipped_device_list_loads()
    {
        var list = DeviceLibrary.Load(Path.Combine(AppContext.BaseDirectory, "devices.json"));
        Assert.True(list.Count >= 50);
        Assert.All(list, d => Assert.InRange(d.Watts, 1, 2400));
        Assert.All(list, d => Assert.False(string.IsNullOrWhiteSpace(d.Note)));
        Assert.Equal(list.Count, list.Select(d => d.Name.ToLowerInvariant()).Distinct().Count());
        Assert.True(list.Select(d => d.Category).Distinct().Count() <= 9);
    }
}
