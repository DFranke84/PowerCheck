namespace PowerCheck.Core;

/// <summary>One extension cord wire size.</summary>
/// <param name="Awg">Gauge number: smaller number = thicker wire.</param>
/// <param name="Amps">How much current the cord can carry without overheating.</param>
/// <param name="OhmsPer1000Ft">Copper wire resistance, one conductor.</param>
public sealed record CordGauge(int Awg, double Amps, double OhmsPer1000Ft)
{
    public static readonly IReadOnlyList<CordGauge> All =
    [
        new(16, 13, 4.016),
        new(14, 15, 2.525),
        new(12, 20, 1.588),
        new(10, 25, 0.999),
    ];

    public string Label => $"{Awg} gauge";
}

/// <summary>How one cord gauge does for a given load and length.</summary>
public sealed record GaugeResult(
    CordGauge Gauge,
    double DropPercent,
    double LoadShare,
    Verdict Verdict,
    int GreenMaxFeet,
    int MaxFeet,
    string Reason);

/// <summary>Everything the extension cord sizer tells you.</summary>
public sealed record CordReport(
    double Amps,
    double Watts,
    int LengthFeet,
    bool Outdoor,
    IReadOnlyList<GaugeResult> Gauges,
    GaugeResult? Recommended,
    Verdict Overall,
    string Headline,
    IReadOnlyList<string> Notes);

/// <summary>
/// Picks the extension cord size. A cord must pass two checks:
/// it must not overheat (amps within its rating), and it must not lose too much
/// voltage over its length (green up to 3 %, yellow up to 5 %, red above).
/// </summary>
public static class CordSizer
{
    public const double GreenDropPercent = 3.0;
    public const double MaxDropPercent = 5.0;

    /// <summary>Volts lost in the cord: current flows out and back, so twice the length.</summary>
    public static double DropVolts(CordGauge gauge, double amps, double lengthFeet) =>
        2 * lengthFeet * amps * gauge.OhmsPer1000Ft / 1000.0;

    public static double DropPercent(CordGauge gauge, double amps, double lengthFeet) =>
        DropVolts(gauge, amps, lengthFeet) / Electric.Volts * 100.0;

    /// <summary>Longest cord that keeps the voltage drop at or under the given percent.</summary>
    public static int MaxFeetFor(CordGauge gauge, double amps, double dropPercent)
    {
        if (amps <= 0) return int.MaxValue;
        double volts = Electric.Volts * dropPercent / 100.0;
        return (int)Math.Floor(volts / (2 * amps * gauge.OhmsPer1000Ft / 1000.0));
    }

    public static GaugeResult Check(CordGauge gauge, double amps, double lengthFeet, bool continuous)
    {
        // The cord's own wire rating. The 15 A plug / outlet is a separate hard limit.
        double limit = gauge.Amps;
        double share = amps / limit;
        bool overPlug = amps > Electric.PlugLimitAmps + 1e-9;
        Verdict heat = overPlug ? Verdict.Red : Electric.ForShare(share);

        double drop = DropPercent(gauge, amps, lengthFeet);
        Verdict dropVerdict = drop <= GreenDropPercent + 1e-9 ? Verdict.Green
            : drop <= MaxDropPercent + 1e-9 ? Verdict.Yellow
            : Verdict.Red;

        Verdict verdict = Electric.Worst(heat, dropVerdict);

        bool heatFails = overPlug || share > 1.0 + 1e-9;
        int greenMax = heatFails || heat != Verdict.Green ? 0 : MaxFeetFor(gauge, amps, GreenDropPercent);
        int max = heatFails ? 0 : MaxFeetFor(gauge, amps, MaxDropPercent);

        string reason;
        if (heatFails)
            reason = overPlug
                ? $"Too much for any household cord: {amps:0.0} A is over the 15 A plug rating."
                : $"Too thin: {amps:0.0} A is over this cord's {gauge.Amps:0} A rating, so it would overheat.";
        else if (heat == Verdict.Yellow)
            reason = continuous
                ? $"At {share:P0} of its {limit:0} A rating. Too close for something that runs for hours."
                : $"At {share:P0} of its {limit:0} A rating. It works, but it's near the limit.";
        else if (dropVerdict == Verdict.Red)
            reason = $"Too long for this size: loses {drop:0.0} % of the power. Max {max} ft.";
        else if (dropVerdict == Verdict.Yellow)
            reason = $"Usable but loses {drop:0.0} % of the power over this length. Fully OK up to {greenMax} ft.";
        else
            reason = $"Good: {share:P0} of its rating, loses only {drop:0.0} %.";

        return new GaugeResult(gauge, drop, share, verdict, greenMax, max, reason);
    }

    public static CordReport Size(IEnumerable<DeviceUse> uses, int lengthFeet, bool outdoor)
    {
        var list = uses.Where(u => u.Quantity > 0).ToList();
        double watts = list.Sum(u => u.Watts);
        bool continuous = list.Any(u => u.OnAtSameTime && u.Device.Continuous);
        var notes = new List<string>();

        foreach (var u in list.Where(u => u.Device.WallOnly))
            notes.Add($"{u.Device.Name}: the maker says to plug it straight into a wall outlet. Don't use an extension cord or power strip with it.");
        if (list.Any(u => u.Device.Motor))
            notes.Add("Motors (saws, compressors, vacuums) pull extra power for a moment when they start. A thicker cord helps them start easily.");

        return Build(Electric.WattsToAmps(watts), lengthFeet, outdoor, continuous, notes,
            forceRed: list.Any(u => u.Device.WallOnly));
    }

    public static CordReport Size(double amps, int lengthFeet, bool outdoor, bool continuous) =>
        Build(amps, lengthFeet, outdoor, continuous, [], forceRed: false);

    private static CordReport Build(double amps, int lengthFeet, bool outdoor, bool continuous,
        List<string> notes, bool forceRed)
    {
        var results = CordGauge.All.Select(g => Check(g, amps, lengthFeet, continuous)).ToList();

        // Thinnest cord that is fully green; otherwise the thinnest yellow one.
        GaugeResult? pick = results.FirstOrDefault(r => r.Verdict == Verdict.Green)
                         ?? results.FirstOrDefault(r => r.Verdict == Verdict.Yellow);

        string rating = outdoor ? ", outdoor-rated (has a \"W\" on the jacket)" : "";
        string headline;
        Verdict overall;
        if (amps <= 0)
        {
            headline = "Add a device or enter what it draws.";
            overall = Verdict.Green;
        }
        else if (pick is null)
        {
            overall = Verdict.Red;
            headline = amps > Electric.PlugLimitAmps
                ? $"{amps:0.0} A is more than one household cord or outlet can carry. Split the load across separate circuits."
                : $"No cord works at {lengthFeet} ft. Use a shorter cord or move closer to an outlet.";
        }
        else
        {
            overall = pick.Verdict;
            headline = pick.Verdict == Verdict.Green
                ? $"Use at least a {pick.Gauge.Label} cord{rating}."
                : $"A {pick.Gauge.Label} cord{rating} will work, but it's near its limit. Shorter is better.";
        }

        if (outdoor)
            notes.Add("Outdoors, plug into a GFCI outlet (the kind with Test/Reset buttons) and keep the plug connections off wet ground.");
        if (amps > Electric.PlugLimitAmps * Electric.ComfortShare && amps <= Electric.PlugLimitAmps)
            notes.Add($"{amps:0.0} A uses most of a normal 15 A wall outlet. Plug nothing else into that circuit while it runs.");
        if (continuous && amps > 0)
            notes.Add("This runs for hours, so the app leaves extra room (the 80 % rule). Feel the plug after an hour: warm means unplug it and use a thicker cord.");

        if (forceRed) overall = Verdict.Red;

        return new CordReport(amps, Electric.AmpsToWatts(amps), lengthFeet, outdoor, results, pick, overall, headline, notes);
    }
}
