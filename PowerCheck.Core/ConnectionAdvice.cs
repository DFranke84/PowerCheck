namespace PowerCheck.Core;

/// <summary>Plain-language advice on how to hook up several plugs safely.</summary>
public static class ConnectionAdvice
{
    public static List<string> For(int plugs, double amps, bool outdoor)
    {
        var tips = new List<string>();
        if (plugs < 2) return tips;

        tips.Add($"How to connect {plugs} plugs, best first:\n" +
                 "  1. A power strip plugged straight into the wall. Buy one with a long cord (6-15 ft) so you don't need an extension cord at all.\n" +
                 "  2. A multi-outlet extension cord (a cord with a 3-outlet end, or a \"power block\" with 6 outlets) in the size this app recommends.\n" +
                 "  3. Use both halves of the wall outlet: for example, a 3-outlet cord in one and the 4th plug in the other.");

        tips.Add("Avoid: plugging a power strip into an extension cord or into another strip, stacking plug \"cubes\" on each other, and cheap splitters without a UL or ETL mark. Strip makers say their strips go straight into the wall.");

        if (plugs > 3)
            tips.Add("Chargers with bulky plug bodies can block the next outlet. Pick a strip or power block with widely spaced outlets.");

        if (outdoor)
            tips.Add("In a garage or outside: use an outdoor-rated strip or power block, mount it on the wall off the floor (away from water, snow melt and gas cans).");

        if (amps <= 3)
            tips.Add($"Your total is small ({amps:0.0} A), so overloading isn't the worry here. Loose or cheap connections are, so buy good parts and push the plugs all the way in.");

        return tips;
    }
}
