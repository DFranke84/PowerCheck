using System.IO;
using System.Windows.Media.Imaging;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>
/// The built-in device list plus your own devices, shared by every tab.
/// Your devices live in my-devices.json and their pictures in device-images,
/// both next to the program so it stays portable.
/// </summary>
public sealed class DeviceStore
{
    public static readonly string BaseDir = AppContext.BaseDirectory;
    public static readonly string BuiltInPath = Path.Combine(BaseDir, "devices.json");
    public static readonly string MinePath = Path.Combine(BaseDir, "my-devices.json");
    public static readonly string ImagesDir = Path.Combine(BaseDir, "device-images");

    private readonly List<Device> _builtIn;
    private List<Device> _mine;

    /// <summary>Raised after a device is added, changed or removed. Argument: old name (or null), new device (or null).</summary>
    public event Action<string?, Device?>? Changed;

    public DeviceStore()
    {
        _builtIn = DeviceLibrary.Load(BuiltInPath);
        _mine = DeviceLibrary.Load(MinePath);
    }

    public List<Device> All => DeviceLibrary.Merge(_builtIn, _mine);

    public IEnumerable<string> Categories => All.Select(d => d.Category).Distinct().OrderBy(c => c);

    public bool IsMine(Device d) => _mine.Any(m => Same(m.Name, d.Name));

    public bool IsBuiltIn(string name) => _builtIn.Any(b => Same(b.Name, name));

    /// <summary>"", " (your device)" or " (your edited copy)".</summary>
    public string SourceLabel(Device d) => !IsMine(d) ? "" : IsBuiltIn(d.Name) ? "Edited" : "Yours";

    public void Save(Device result, Device? old)
    {
        // Renaming a built-in device keeps the built-in one and adds yours.
        string? oldName = old is not null && IsMine(old) ? old.Name : null;
        _mine = DeviceLibrary.Upsert(_mine, result, oldName);
        DeviceLibrary.Save(MinePath, _mine);
        Changed?.Invoke(old?.Name, result);
    }

    /// <summary>Deletes your device, or for an edited built-in one, goes back to the built-in values.</summary>
    public void Remove(Device d)
    {
        _mine = _mine.Where(m => !Same(m.Name, d.Name)).ToList();
        DeviceLibrary.Save(MinePath, _mine);
        var restored = _builtIn.FirstOrDefault(b => Same(b.Name, d.Name));
        RecycleImageIfUnused(d.Image);
        Changed?.Invoke(d.Name, restored);
    }

    /// <summary>Moves a picture to the Recycle Bin when no device uses it any more.</summary>
    public void RecycleImageIfUnused(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return;
        string file = Path.GetFileName(image);
        if (All.Any(d => string.Equals(Path.GetFileName(d.Image ?? ""), file, StringComparison.OrdinalIgnoreCase))) return;
        string path = Path.Combine(ImagesDir, file);
        try
        {
            if (File.Exists(path))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        catch
        {
            // Leaving an unused picture behind is harmless.
        }
    }

    // ---- Pictures ----

    public static string? ImagePath(Device d)
    {
        if (string.IsNullOrWhiteSpace(d.Image)) return null;
        string path = Path.Combine(ImagesDir, Path.GetFileName(d.Image));
        return File.Exists(path) ? path : null;
    }

    /// <summary>Copies a picture into device-images and returns its new file name.</summary>
    public static string ImportImage(string sourcePath, string deviceName)
    {
        Directory.CreateDirectory(ImagesDir);
        string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        string file = $"{SafeName(deviceName)}-{DateTime.Now:yyyyMMddHHmmss}{ext}";
        File.Copy(sourcePath, Path.Combine(ImagesDir, file), overwrite: true);
        return file;
    }

    /// <summary>Saves a pasted picture as PNG into device-images and returns its file name.</summary>
    public static string ImportImage(BitmapSource bitmap, string deviceName)
    {
        Directory.CreateDirectory(ImagesDir);
        string file = $"{SafeName(deviceName)}-{DateTime.Now:yyyyMMddHHmmss}.png";
        using var fs = File.Create(Path.Combine(ImagesDir, file));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmap));
        enc.Save(fs);
        return file;
    }

    /// <summary>Loads a picture without locking the file. Returns null if it can't be read.</summary>
    public static BitmapImage? LoadImage(string? path, int decodeWidth = 0)
    {
        if (path is null) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        string s = new string(chars).Trim('-');
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Length == 0 ? "device" : s[..Math.Min(s.Length, 40)];
    }

    private static bool Same(string a, string b) => a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
}
