using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerCheck.Core;

/// <summary>Something you plug devices INTO: a power strip, hub, splitter or extension cord you own.</summary>
public sealed record Gear
{
    public static readonly IReadOnlyList<string> Types = ["Power strip", "Power hub / outlet adapter", "Splitter cord", "Extension cord"];

    public required string Name { get; init; }

    /// <summary>One of <see cref="Types"/>.</summary>
    public string Type { get; init; } = "Power strip";

    /// <summary>Amp rating from its tag. Household plug-in gear is never more than 15 A.</summary>
    public double Amps { get; init; } = 15;

    public int Outlets { get; init; } = 1;

    /// <summary>For extension cords: wire gauge (16, 14, 12, 10), or 0 if unknown.</summary>
    public int Gauge { get; init; }

    /// <summary>For extension cords: length in feet, or 0 if unknown.</summary>
    public int LengthFeet { get; init; }

    public string Note { get; init; } = "";
    public string? Image { get; init; }
    public string? Link { get; init; }

    [JsonIgnore]
    public SourceKind Kind => Type switch
    {
        "Extension cord" => SourceKind.Cord,
        "Power strip" => SourceKind.PowerStrip,
        _ => SourceKind.Adapter,
    };

    /// <summary>What the load check measures against: its own rating, never above the 15 A plug.</summary>
    public PowerSource ToSource()
    {
        double amps = Math.Min(Amps > 0 ? Amps : Electric.PlugLimitAmps, Electric.PlugLimitAmps);
        if (Kind == SourceKind.Cord && CordGauge.All.FirstOrDefault(g => g.Awg == Gauge) is CordGauge g)
            amps = Math.Min(amps, g.Amps);
        return new PowerSource($"{Name} ({amps:0} A, {Outlets} outlet{(Outlets == 1 ? "" : "s")})", amps, Kind, Outlets);
    }

    public override string ToString() => Name;
}

/// <summary>Loads and saves gear lists, same idea as the device lists.</summary>
public static class GearLibrary
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static List<Gear> Load(string path)
    {
        if (!File.Exists(path)) return [];
        var list = JsonSerializer.Deserialize<List<Gear>>(File.ReadAllText(path), Options) ?? [];
        return list.Where(g => !string.IsNullOrWhiteSpace(g.Name)).ToList();
    }

    public static void Save(string path, IEnumerable<Gear> gear) =>
        File.WriteAllText(path, JsonSerializer.Serialize(gear.ToList(), Options));

    /// <summary>Built-in gear plus yours; yours wins when names match.</summary>
    public static List<Gear> Merge(IEnumerable<Gear> builtIn, IEnumerable<Gear> mine)
    {
        var byName = new Dictionary<string, Gear>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in builtIn) byName[g.Name.Trim()] = g;
        foreach (var g in mine) byName[g.Name.Trim()] = g;
        return byName.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
