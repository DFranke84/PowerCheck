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
    public void Shipped_device_list_loads()
    {
        var list = DeviceLibrary.Load(Path.Combine(AppContext.BaseDirectory, "devices.json"));
        Assert.NotEmpty(list);
        Assert.All(list, d => Assert.True(d.Watts > 0));
    }
}
