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
}
