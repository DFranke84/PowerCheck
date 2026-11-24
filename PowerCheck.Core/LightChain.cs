namespace PowerCheck.Core;

public sealed record LightType(string Name, double WattsPerString, int TypicalMaxConnect)
{
    public static readonly IReadOnlyList<LightType> All =
    [
        new("LED mini lights (100 bulbs)", 6, 40),
        new("Incandescent mini lights (100 bulbs)", 40, 3),
        new("LED C9 (25 bulbs)", 15, 20),
        new("Incandescent C7 (25 bulbs)", 125, 3),
        new("Incandescent C9 (25 bulbs)", 175, 3),
    ];

    public override string ToString() => Name;
}

public sealed record LightReport(
    int Strings,
    int MaxConnect,
    int RunsNeeded,
    double Watts,
    Verdict ChainVerdict,
    string ChainAdvice,
    CordReport Cord);

/// <summary>
/// Christmas lights have two separate limits: how many strings may be plugged
/// end to end (printed on the tag, set by the small fuse in the plug), and what
/// the extension cord feeding them can carry.
/// </summary>
public static class LightChain
{
    public static LightReport Check(double wattsPerString, int strings, int maxConnect, int cordFeet, bool outdoor = true)
    {
        maxConnect = Math.Max(1, maxConnect);
        strings = Math.Max(0, strings);
        double watts = wattsPerString * strings;
        int runs = strings == 0 ? 0 : (int)Math.Ceiling(strings / (double)maxConnect);

        Verdict chain;
        string advice;
        if (strings <= maxConnect)
        {
            chain = Verdict.Green;
            advice = $"All {strings} strings can be plugged end to end (the tag allows {maxConnect}).";
        }
        else
        {
            chain = Verdict.Yellow;
            advice = $"Too many for one chain: the tag allows {maxConnect} end to end. " +
                     $"Split them into {runs} separate chains, each starting at the extension cord or a power strip " +
                     $"(not plugged into each other).";
        }

        var cord = CordSizer.Size(
            [new DeviceUse(new Device { Name = "Light strings", Watts = watts, Continuous = true }, 1)],
            cordFeet, outdoor);

        return new LightReport(strings, maxConnect, runs, watts, chain, advice, cord);
    }

    /// <summary>True when the strings as actually connected go over the tag limit.</summary>
    public static Verdict ChainAsConnected(int stringsInOneChain, int maxConnect) =>
        stringsInOneChain <= maxConnect ? Verdict.Green : Verdict.Red;
}
