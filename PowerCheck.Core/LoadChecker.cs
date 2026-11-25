namespace PowerCheck.Core;

public enum SourceKind
{
    PowerStrip,
    Cord,
    Circuit
}

/// <summary>What everything is plugged into.</summary>
public sealed record PowerSource(string Name, double Amps, SourceKind Kind = SourceKind.Circuit)
{
    public static readonly PowerSource PowerStrip = new("Power strip (15 A)", 15, SourceKind.PowerStrip);
    public static readonly PowerSource Circuit15 = new("Wall circuit, 15 A breaker", 15, SourceKind.Circuit);
    public static readonly PowerSource Circuit20 = new("Wall circuit, 20 A breaker", 20, SourceKind.Circuit);

    public static PowerSource Cord(CordGauge g) =>
        new($"{g.Label} extension cord", Math.Min(g.Amps, Electric.PlugLimitAmps), SourceKind.Cord);

    public double Watts => Electric.AmpsToWatts(Amps);

    public override string ToString() => Name;
}

public sealed record LoadReport(
    PowerSource Source,
    double Watts,
    double Amps,
    double Share,
    Verdict Verdict,
    string Headline,
    IReadOnlyList<string> Notes);

/// <summary>Adds up several devices on one cord, strip or circuit.</summary>
public static class LoadChecker
{
    public static LoadReport Check(IEnumerable<DeviceUse> uses, PowerSource source)
    {
        var list = uses.Where(u => u.Quantity > 0).ToList();
        double watts = list.Sum(u => u.Watts);
        double amps = Electric.WattsToAmps(watts);
        double share = amps / source.Amps;
        Verdict verdict = Electric.ForShare(share);
        var notes = new List<string>();

        string headline = list.Count == 0
            ? "Add what you'll plug in."
            : verdict switch
            {
                Verdict.Green => $"OK: {watts:0} W ({amps:0.0} A) is {share:P0} of the {source.Watts:0} W limit.",
                Verdict.Yellow => $"Near the limit: {watts:0} W is {share:P0} of {source.Watts:0} W. Fine for a short while, but don't add anything.",
                _ => $"Overloaded: {watts:0} W is {share:P0} of the {source.Watts:0} W limit. It may trip the breaker or overheat."
            };

        bool continuous = list.Any(u => u.OnAtSameTime && u.Device.Continuous);
        if (continuous && verdict == Verdict.Yellow)
            notes.Add("Some of these run for hours. Long-running loads should stay under 80 % of the limit.");

        if (verdict != Verdict.Green)
        {
            var biggest = list.Where(u => u.Watts > 0).OrderByDescending(u => u.Watts).FirstOrDefault();
            if (biggest is not null)
                notes.Add($"Move the {biggest.Device.Name} ({biggest.Watts:0} W) to an outlet on a different circuit.");
        }

        var motors = list.Where(u => u.OnAtSameTime && u.Device.Motor).ToList();
        if (motors.Count > 0 && share > 0.6)
            notes.Add("Motors pull 2-3 times their running power for a second when they start. Start the big tools one at a time.");

        foreach (var u in list.Where(u => u.Device.WallOnly))
            notes.Add($"{u.Device.Name}: the maker says to plug it straight into a wall outlet, not into a strip or extension cord.");

        if (list.Any(u => !u.OnAtSameTime))
            notes.Add("Items marked \"not on at the same time\" aren't counted. If they ever do run together, check again.");

        int plugs = list.Sum(u => u.Quantity);
        switch (source.Kind)
        {
            case SourceKind.PowerStrip:
                notes.Add("Plug the strip straight into a wall outlet: not into an extension cord or another strip.");
                if (plugs > 6)
                    notes.Add($"That's {plugs} plugs; most strips have 6 outlets. Use a second strip on a different wall outlet, not one plugged into the other.");
                break;
            case SourceKind.Cord:
                if (plugs > 1)
                    notes.Add("For several plugs on one cord, use a cord with a built-in multi-outlet end or a \"power block\", not stacked plug cubes.");
                break;
            case SourceKind.Circuit:
                notes.Add("Everything on the same breaker counts, even in other rooms. To find out what's on it, turn the breaker off and see which outlets and lights go dead.");
                break;
        }

        return new LoadReport(source, watts, amps, share, verdict, headline, notes);
    }
}
