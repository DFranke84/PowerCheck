using System.IO;
using System.Text.Json;
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
    public static readonly string FavoritesPath = Path.Combine(BaseDir, "favorites.json");
    public static readonly string GearPath = Path.Combine(BaseDir, "gear.json");
    public static readonly string MyGearPath = Path.Combine(BaseDir, "my-gear.json");

    /// <summary>The one store the app uses (for display helpers in XAML).</summary>
    public static DeviceStore? Current { get; private set; }

    private readonly List<Device> _builtIn;
    private List<Device> _mine;
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Gear> _builtInGear;
    private List<Gear> _myGear;

    /// <summary>Raised after a strip, hub or cord is added, changed or removed. Argument: the one to select (or null).</summary>
    public event Action<string?>? GearChanged;

    /// <summary>Raised after a device is added, changed or removed. Argument: old name (or null), new device (or null).</summary>
    public event Action<string?, Device?>? Changed;

    public DeviceStore()
    {
        _builtIn = DeviceLibrary.Load(BuiltInPath);
        _mine = DeviceLibrary.Load(MinePath);
        _builtInGear = GearLibrary.Load(GearPath);
        _myGear = GearLibrary.Load(MyGearPath);
        try
        {
            if (File.Exists(FavoritesPath))
                foreach (var n in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FavoritesPath)) ?? [])
                    _favorites.Add(n.Trim());
        }
        catch
        {
            // A damaged favorites file just means no favorites.
        }
        Current = this;
    }

    public List<Device> All => DeviceLibrary.Merge(_builtIn, _mine);

    /// <summary>Favorites first, then the rest, each alphabetical. Used by the device pickers.</summary>
    public List<Device> FavoritesFirst => All.OrderBy(d => IsFavorite(d.Name) ? 0 : 1)
        .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    // ---- Your strips, hubs and cords (gear.json built in, my-gear.json yours) ----

    public List<Gear> AllGear => GearLibrary.Merge(_builtInGear, _myGear);

    public bool IsMyGear(Gear g) => _myGear.Any(m => Same(m.Name, g.Name));

    public bool IsBuiltInGear(string name) => _builtInGear.Any(b => Same(b.Name, name));

    public void SaveGear(Gear result, Gear? old)
    {
        _myGear = _myGear.Where(m => !Same(m.Name, result.Name)
                                     && (old is null || !IsMyGear(old) || !Same(m.Name, old.Name))).ToList();
        _myGear.Add(result);
        GearLibrary.Save(MyGearPath, _myGear);
        GearChanged?.Invoke(result.Name);
    }

    /// <summary>Deletes your gear, or for edited built-in gear, goes back to the built-in values.</summary>
    public void RemoveGear(Gear g)
    {
        _myGear = _myGear.Where(m => !Same(m.Name, g.Name)).ToList();
        GearLibrary.Save(MyGearPath, _myGear);
        RecycleImageIfUnused(g.Image);
        GearChanged?.Invoke(IsBuiltInGear(g.Name) ? g.Name : null);
    }

    // ---- Favorites (saved in favorites.json next to the program) ----

    public bool IsFavorite(string name) => _favorites.Contains(name.Trim());

    public void ToggleFavorite(Device d)
    {
        if (!_favorites.Remove(d.Name.Trim())) _favorites.Add(d.Name.Trim());
        SaveFavorites();
        Changed?.Invoke(d.Name, d);
    }

    private void SaveFavorites() =>
        File.WriteAllText(FavoritesPath, JsonSerializer.Serialize(_favorites.OrderBy(n => n).ToList(),
            new JsonSerializerOptions { WriteIndented = true }));

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
        // A renamed favorite stays a favorite.
        if (old is not null && !Same(old.Name, result.Name) && IsFavorite(old.Name))
        {
            if (oldName is not null) _favorites.Remove(old.Name.Trim());
            _favorites.Add(result.Name.Trim());
            SaveFavorites();
        }
        Changed?.Invoke(old?.Name, result);
    }

    /// <summary>Deletes your device, or for an edited built-in one, goes back to the built-in values.</summary>
    public void Remove(Device d)
    {
        _mine = _mine.Where(m => !Same(m.Name, d.Name)).ToList();
        DeviceLibrary.Save(MinePath, _mine);
        var restored = _builtIn.FirstOrDefault(b => Same(b.Name, d.Name));
        if (restored is null && _favorites.Remove(d.Name.Trim())) SaveFavorites();
        RecycleImageIfUnused(d.Image);
        Changed?.Invoke(d.Name, restored);
    }

    /// <summary>Moves a picture to the Recycle Bin when no device uses it any more.</summary>
    public void RecycleImageIfUnused(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return;
        string file = Path.GetFileName(image);
        if (All.Select(d => d.Image).Concat(AllGear.Select(g => g.Image))
            .Any(i => string.Equals(Path.GetFileName(i ?? ""), file, StringComparison.OrdinalIgnoreCase))) return;
        string path = Path.Combine(ImagesDir, file);
        try
        {
            string thumb = Path.Combine(ThumbsDir, Path.GetFileNameWithoutExtension(file) + ".thumb.png");
            if (File.Exists(thumb)) File.Delete(thumb); // made by the app, rebuilt when needed
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

    public const int ThumbSize = 160;
    public static readonly string ThumbsDir = Path.Combine(ImagesDir, "thumbs");

    /// <summary>
    /// Small copy of the device picture for the list. Made once, the first time it's
    /// needed (and again if the picture changes), and kept in device-images\thumbs.
    /// </summary>
    public static BitmapSource? LoadThumb(Device d)
    {
        string? source = ImagePath(d);
        if (source is null) return null;
        string thumb = Path.Combine(ThumbsDir, Path.GetFileNameWithoutExtension(source) + ".thumb.png");
        try
        {
            if (!File.Exists(thumb) || File.GetLastWriteTimeUtc(thumb) < File.GetLastWriteTimeUtc(source))
            {
                var small = LoadImage(source, ThumbSize);
                if (small is null) return null;
                Directory.CreateDirectory(ThumbsDir);
                using var fs = File.Create(thumb);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(small));
                enc.Save(fs);
                return small;
            }
            return LoadImage(thumb);
        }
        catch
        {
            return LoadImage(source, ThumbSize);
        }
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
