using System.Text.Json.Serialization;

namespace PowerCheck.Core;

/// <summary>Something you plug in, with what it typically draws.</summary>
public sealed record Device
{
    public required string Name { get; init; }

    /// <summary>Typical running watts (from the label, or a common value).</summary>
    public required double Watts { get; init; }

    public string Category { get; init; } = "Other";

    /// <summary>Runs 3+ hours at a time (heaters, chargers, lights, fridges).</summary>
    public bool Continuous { get; init; }

    /// <summary>Maker says plug straight into a wall outlet, not an extension cord or strip.</summary>
    public bool WallOnly { get; init; }

    /// <summary>Has a motor, so it pulls extra for a moment when it starts.</summary>
    public bool Motor { get; init; }

    /// <summary>Short plain-language note shown with the device.</summary>
    public string Note { get; init; } = "";

    /// <summary>Picture file name inside the device-images folder (optional).</summary>
    public string? Image { get; init; }

    /// <summary>Product page link (optional). Blank means an Amazon search for the name.</summary>
    public string? Link { get; init; }

    [JsonIgnore]
    public double Amps => Electric.WattsToAmps(Watts);

    /// <summary>The link to open: your own http(s) link, or an Amazon search for the device.</summary>
    [JsonIgnore]
    public string ShopUrl =>
        Uri.TryCreate(Link?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? u.AbsoluteUri
            : "https://www.amazon.com/s?k=" + Uri.EscapeDataString(Name);

    [JsonIgnore]
    public bool HasOwnLink => !ShopUrl.StartsWith("https://www.amazon.com/s?k=", StringComparison.Ordinal);

    public override string ToString() => $"{Name} ({Watts:0} W)";
}

/// <summary>A device plus how many of them are plugged in.</summary>
public sealed record DeviceUse(Device Device, int Quantity = 1, bool OnAtSameTime = true)
{
    public double Watts => OnAtSameTime ? Device.Watts * Quantity : 0;
    public double Amps => Electric.WattsToAmps(Watts);
}
