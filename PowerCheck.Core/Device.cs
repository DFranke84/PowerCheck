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

    public double Amps => Electric.WattsToAmps(Watts);

    public override string ToString() => $"{Name} ({Watts:0} W)";
}

/// <summary>A device plus how many of them are plugged in.</summary>
public sealed record DeviceUse(Device Device, int Quantity = 1, bool OnAtSameTime = true)
{
    public double Watts => OnAtSameTime ? Device.Watts * Quantity : 0;
    public double Amps => Electric.WattsToAmps(Watts);
}
