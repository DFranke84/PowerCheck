namespace PowerCheck.Core;

/// <summary>Traffic-light result used everywhere in the app.</summary>
public enum Verdict
{
    Green,
    Yellow,
    Red
}

/// <summary>Basic numbers for US household power.</summary>
public static class Electric
{
    /// <summary>Standard US outlet voltage.</summary>
    public const double Volts = 120.0;

    /// <summary>Most household plugs, outlets and power strips are rated 15 amps.</summary>
    public const double PlugLimitAmps = 15.0;

    /// <summary>Loads at or below this share of a rating are green; above it, yellow.</summary>
    public const double ComfortShare = 0.80;

    public static double WattsToAmps(double watts) => watts / Volts;

    public static double AmpsToWatts(double amps) => amps * Volts;

    public static Verdict Worst(Verdict a, Verdict b) => (Verdict)Math.Max((int)a, (int)b);

    /// <summary>Green up to 80 % of a rating, yellow up to 100 %, red over.</summary>
    public static Verdict ForShare(double share) =>
        share <= ComfortShare + 1e-9 ? Verdict.Green
        : share <= 1.0 + 1e-9 ? Verdict.Yellow
        : Verdict.Red;
}
