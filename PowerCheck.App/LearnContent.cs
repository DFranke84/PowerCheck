namespace PowerCheck.App;

/// <summary>One topic on the Learn tab: a heading, a one-line summary and a few plain-language points.</summary>
public sealed record LearnTopic(string Icon, string Title, string Summary, IReadOnlyList<string> Points);

/// <summary>The text shown on the Learn tab.</summary>
public static class LearnContent
{
    public static readonly IReadOnlyList<LearnTopic> Topics =
    [
        new("⚡", "Watts, amps and volts",
            "Three numbers on every label; you only need one of them.",
            [
                "Watts (W) = how much power something uses. Amps (A) = how much current flows. Volts (V) = the push; US outlets are 120 V.",
                "Watts = amps × 120. A 12.5 A heater uses 12.5 × 120 = 1500 W.",
                "VA on a label counts the same as watts for this app.",
                "Chargers list an input and an output. Use the INPUT line (the 120 V side). A \"2 A\" battery charger may only draw 0.5 A from the wall.",
                "A normal outlet and its breaker are good for 15 A = 1800 W in total, from everything on that circuit.",
            ]),

        new("〰", "Extension cords",
            "Thicker wire carries more, and longer cords lose more.",
            [
                "The gauge number is printed on the cord jacket, for example \"16/3\" or \"14 AWG\". The SMALLER the number, the THICKER the wire.",
                "Roughly: 16 gauge = 13 A, 14 gauge = 15 A, 12 gauge = 20 A, 10 gauge = 25 A. The plug end is still a 15 A plug.",
                "Long cords lose power along the way, so motors run hot and lights dim. Go one size thicker for 100 ft.",
                "Outdoors, use a cord marked \"W\" (like SJTW or SJOW). Indoor-only cords crack in cold and sun.",
                "Unroll the whole cord while using it. A coiled cord under load can get hot.",
                "Don't run cords under rugs, through doorways that pinch them, or where cars drive over them.",
                "One cord of the right length beats two cords plugged together.",
            ]),

        new("☰", "Power strips, hubs and splitters",
            "They add outlets, not power: everything still comes out of one wall outlet.",
            [
                "A strip, hub or splitter shares the one wall outlet behind it. Six outlets doesn't mean six times the power.",
                "Plug a power strip straight into a wall outlet. Strip makers say not to plug it into an extension cord or another strip.",
                "Don't stack adapters, cubes or splitters on top of each other. Each joint is a place to get loose and hot.",
                "A surge protector protects electronics from spikes. It does NOT let you plug in more.",
                "Check the strip's own rating (on the back). Cheap thin strips may only be rated 10 or 13 A.",
            ]),

        new("⌂", "Circuits and breakers",
            "A breaker protects the wires in the wall, and everything on it adds up.",
            [
                "Most rooms have 15 A breakers. Kitchens, garages and shops often have 20 A. The number is on the breaker switch.",
                "Outlets in different rooms are often on the same breaker. To find out, switch one breaker off and see what goes dead.",
                "Things that run for hours (heaters, chargers, lights) should stay under 80 % of the limit: 1440 W on a 15 A circuit.",
                "A breaker that trips is doing its job. Don't keep resetting it; move something to another circuit.",
                "Never replace a breaker with a bigger one to stop it tripping. That's how wires overheat inside walls.",
            ]),

        new("☖", "Plug straight into the wall",
            "Some things should never go on an extension cord or power strip.",
            [
                "Space heaters and oil-filled radiators.",
                "Refrigerators, freezers, window and portable air conditioners, dehumidifiers.",
                "Microwaves and big shop air compressors (a 27 gallon 120 V compressor uses a whole 15 A outlet).",
                "Sump pumps and car (EV) chargers.",
                "If the manual says \"do not use an extension cord\", that's why: these pull a lot, for a long time.",
            ]),

        new("⚙", "Motors and big tools",
            "Motors pull extra for a moment when they start.",
            [
                "Saws, vacuums, compressors and pumps can pull 2–3 times their running amps for a second at start-up.",
                "Start big tools one at a time, especially on the same circuit.",
                "A long or thin cord makes motors start slowly and run hot. Use 12 gauge for big tools on long runs.",
                "Air compressor manuals usually say: use a longer air hose, not a longer cord.",
            ]),

        new("☔", "Outdoors, garage and wet places",
            "Water and electricity: keep connections dry and use GFCI.",
            [
                "Use GFCI outlets outdoors, in garages, and near water. They have TEST and RESET buttons.",
                "Press TEST once a month: the power should cut off. Press RESET to turn it back on.",
                "Keep plug connections off wet ground and out of snow. A covered connection box or a bag tied over the joint helps.",
                "Use an \"in-use\" outlet cover (the deep kind) so the cover closes with things plugged in.",
                "In cold weather, cords get stiff and crack. Check them each season.",
            ]),

        new("★", "Christmas lights",
            "Two limits: the tag on the string, and the cord feeding them.",
            [
                "The tag near the plug says how many strings can be connected end to end. Never go over it; if you mix brands, use the smallest number.",
                "LED strings use a tiny fraction of old-style bulbs: 5–12 W vs 40–175 W per string.",
                "Need more strings than the tag allows? Start a new chain from the extension cord, splitter or strip.",
                "Use outdoor-rated lights and cords outside, on a GFCI outlet, with connections off the ground.",
                "A timer saves power and means nothing runs all night unattended.",
            ]),
    ];

    /// <summary>Warning signs: if any of these happen, unplug and get it checked.</summary>
    public static readonly IReadOnlyList<string> WarningSigns =
    [
        "A plug, cord or outlet feels warm or hot",
        "A burning or hot plastic smell",
        "Buzzing, crackling or sizzling from an outlet or switch",
        "Brown or black marks around an outlet or plug",
        "Lights dim or flicker when something turns on",
        "A breaker trips again and again",
        "A plug falls out or fits loosely in the outlet",
        "A cord is cracked, cut, pinched or has tape on it",
        "You got a tingle or small shock from something",
    ];
}
