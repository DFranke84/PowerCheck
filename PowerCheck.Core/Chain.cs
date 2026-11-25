namespace PowerCheck.Core;

/// <summary>One thing in the chain between the wall outlet and the devices.</summary>
public sealed record ChainLink
{
    public required string Name { get; init; }
    public SourceKind Kind { get; init; } = SourceKind.PowerStrip;

    /// <summary>Its own amp rating (from the tag). Never counted above the 15 A plug.</summary>
    public double Amps { get; init; } = 15;

    /// <summary>How many outlets it has at the far end.</summary>
    public int Outlets { get; init; } = 1;

    /// <summary>Extension cords only: wire size and length (both 0 when unknown).</summary>
    public int Gauge { get; init; }
    public int LengthFeet { get; init; }

    public double Limit
    {
        get
        {
            double amps = Math.Min(Amps > 0 ? Amps : Electric.PlugLimitAmps, Electric.PlugLimitAmps);
            if (CordGauge.All.FirstOrDefault(g => g.Awg == Gauge) is CordGauge g) amps = Math.Min(amps, g.Amps);
            return amps;
        }
    }

    public static ChainLink ExtensionCord(CordGauge gauge, int feet) => new()
    {
        Name = $"{feet} ft {gauge.Label} extension cord",
        Kind = SourceKind.Cord, Amps = gauge.Amps, Outlets = 1, Gauge = gauge.Awg, LengthFeet = feet,
    };

    public static ChainLink GenericStrip() => new()
    {
        Name = "Power strip (15 A, 6 outlets)", Kind = SourceKind.PowerStrip, Amps = 15, Outlets = 6,
    };

    public static ChainLink From(Gear g) => new()
    {
        Name = g.Name, Kind = g.Kind, Amps = g.Amps, Outlets = g.Outlets, Gauge = g.Gauge, LengthFeet = g.LengthFeet,
    };

    public override string ToString() => Name;
}

public sealed record LinkResult(string Name, double LimitAmps, double Share, Verdict Verdict, string Reason);

public sealed record ChainReport(
    double Watts,
    double Amps,
    IReadOnlyList<LinkResult> Links,
    Verdict Verdict,
    string Headline,
    IReadOnlyList<string> Notes);

/// <summary>
/// Checks a whole chain: wall circuit, then each cord / strip / hub / splitter in order,
/// then the devices at the end. Every link carries the full load, so the weakest one decides.
/// </summary>
public static class ChainCheck
{
    public static ChainReport Check(double breakerAmps, IReadOnlyList<ChainLink> chain, IEnumerable<DeviceUse> uses)
    {
        var list = uses.Where(u => u.Quantity > 0).ToList();
        double watts = list.Sum(u => u.Watts);
        double amps = Electric.WattsToAmps(watts);
        var notes = new List<string>();
        var results = new List<LinkResult>();

        // 1. The wall circuit (breaker).
        double wallShare = amps / breakerAmps;
        results.Add(new LinkResult($"Wall outlet ({breakerAmps:0} A breaker)", breakerAmps, wallShare, Electric.ForShare(wallShare),
            ShareText(amps, breakerAmps)));

        // A 15 A plug is the limit for anything plugged into the wall, even on a 20 A circuit.
        if (chain.Count > 0 || list.Count > 0)
        {
            double plugShare = amps / Electric.PlugLimitAmps;
            if (breakerAmps > Electric.PlugLimitAmps && plugShare > 1)
                notes.Add("A 20 A circuit still can't push more than 15 A through one ordinary plug.");
        }

        // 2. Each link in order.
        double totalDrop = 0;
        foreach (var link in chain)
        {
            double share = amps / link.Limit;
            Verdict v = Electric.ForShare(share);
            string reason = ShareText(amps, link.Limit);

            if (link.Kind == SourceKind.Cord && link.LengthFeet > 0 &&
                CordGauge.All.FirstOrDefault(g => g.Awg == link.Gauge) is CordGauge g)
            {
                double drop = CordSizer.DropPercent(g, amps, link.LengthFeet);
                totalDrop += drop;
                if (drop > CordSizer.MaxDropPercent) { v = Verdict.Red; reason += $" Too long for this load: loses {drop:0.0} % of the power."; }
                else if (drop > CordSizer.GreenDropPercent) { v = Electric.Worst(v, Verdict.Yellow); reason += $" Loses {drop:0.0} % over its length."; }
            }
            results.Add(new LinkResult(link.Name, link.Limit, share, v, reason));
        }

        // 3. Outlets at the end of the chain.
        int plugs = list.Sum(u => u.Quantity);
        int outlets = chain.Count > 0 ? chain[^1].Outlets : 2; // a wall outlet has two sockets
        bool tooManyPlugs = plugs > outlets;
        if (tooManyPlugs)
            notes.Insert(0, $"{plugs} plugs but only {outlets} outlet{(outlets == 1 ? "" : "s")} at the end of the chain.");

        // Safety notes about the order of things.
        for (int i = 1; i < chain.Count; i++)
        {
            var before = chain[i - 1];
            var now = chain[i];
            if (now.Kind == SourceKind.PowerStrip && before.Kind is SourceKind.Cord or SourceKind.PowerStrip)
                notes.Add($"\"{now.Name}\" is plugged into {(before.Kind == SourceKind.Cord ? "an extension cord" : "another strip")}. Strip makers say a strip goes straight into the wall.");
            if (now.Kind == SourceKind.Adapter && before.Kind == SourceKind.Adapter)
                notes.Add($"\"{now.Name}\" is stacked on another adapter. Stacked adapters are a common cause of hot, loose plugs.");
        }
        if (chain.Count(l => l.Kind == SourceKind.Cord) > 1)
            notes.Add("Two extension cords plugged together: their lengths add up, and each connection is another place to get hot. One cord of the right length is better.");

        foreach (var u in list.Where(u => u.Device.WallOnly))
            notes.Add($"{u.Device.Name}: the maker says to plug it straight into a wall outlet, not into a strip or extension cord.");
        if (list.Any(u => u.OnAtSameTime && u.Device.Motor) && results.Max(r => r.Share) > 0.6)
            notes.Add("Motors pull 2-3 times their running power for a second when they start. Start the big tools one at a time.");
        if (list.Any(u => !u.OnAtSameTime))
            notes.Add("Items marked \"not on at the same time\" aren't counted. If they ever do run together, check again.");
        notes.Add("Everything on the same breaker counts, even things in other rooms. To see what's on it, switch the breaker off and see what goes dead.");

        // Overall answer: the worst link decides.
        Verdict worst = results.Select(r => r.Verdict).Aggregate(Verdict.Green, Electric.Worst);
        if (tooManyPlugs) worst = Electric.Worst(worst, Verdict.Yellow);
        if (list.Any(u => u.Device.WallOnly) && chain.Count > 0) worst = Verdict.Red;

        var weakest = results.OrderByDescending(r => r.Verdict).ThenByDescending(r => r.Share).First();
        string headline = list.Count == 0
            ? "Add what you'll plug in at the end of the chain."
            : worst switch
            {
                Verdict.Green => $"OK: {watts:0} W ({amps:0.0} A). Every link is comfortable.",
                Verdict.Yellow => $"Near the limit at: {weakest.Name}. {watts:0} W ({amps:0.0} A).",
                _ => $"Too much for: {weakest.Name}. {watts:0} W ({amps:0.0} A).",
            };
        if (list.Count > 0 && worst != Verdict.Green && weakest.Verdict == Verdict.Green && tooManyPlugs)
            headline = $"Power is OK ({watts:0} W), but there are more plugs than outlets.";
        var wallOnly = list.FirstOrDefault(u => u.Device.WallOnly);
        if (wallOnly is not null && chain.Count > 0)
            headline = $"Plug the {wallOnly.Device.Name} straight into the wall, not through this chain.";

        return new ChainReport(watts, amps, results, worst, headline, notes);
    }

    private static string ShareText(double amps, double limit) =>
        $"{amps:0.0} A of {limit:0.#} A ({amps / limit:P0}).";
}
