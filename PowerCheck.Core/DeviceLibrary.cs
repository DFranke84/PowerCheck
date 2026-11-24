using System.Text.Json;

namespace PowerCheck.Core;

/// <summary>Loads the built-in device list and the user's own devices from JSON files.</summary>
public static class DeviceLibrary
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static List<Device> Load(string path)
    {
        if (!File.Exists(path)) return [];
        var list = JsonSerializer.Deserialize<List<Device>>(File.ReadAllText(path), Options) ?? [];
        return list.Where(d => !string.IsNullOrWhiteSpace(d.Name) && d.Watts > 0).ToList();
    }

    public static void Save(string path, IEnumerable<Device> devices) =>
        File.WriteAllText(path, JsonSerializer.Serialize(devices.ToList(), Options));

    /// <summary>
    /// Built-in devices plus your own. Your own device with the same name as a
    /// built-in one replaces it (that's how editing a built-in device works).
    /// </summary>
    public static List<Device> Merge(IEnumerable<Device> builtIn, IEnumerable<Device> mine)
    {
        var byName = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in builtIn) byName[d.Name.Trim()] = d;
        foreach (var d in mine) byName[d.Name.Trim()] = d;
        return byName.Values.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Adds or replaces a device by name in the user's list.</summary>
    public static List<Device> Upsert(IEnumerable<Device> mine, Device device, string? oldName = null)
    {
        var list = mine.Where(d =>
            !d.Name.Equals(device.Name, StringComparison.OrdinalIgnoreCase) &&
            (oldName is null || !d.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))).ToList();
        list.Add(device);
        return list;
    }
}
