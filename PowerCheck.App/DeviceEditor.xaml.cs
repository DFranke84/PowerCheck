using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>Small window to add a new device or change an existing one.</summary>
public partial class DeviceEditor : Window
{
    public Device? Result { get; private set; }

    /// <summary>Picture file this device no longer uses (so it can be cleaned up).</summary>
    public string? ReplacedImage { get; private set; }

    // Picture state: keep the old file, or a new file / pasted picture to copy in on Save.
    private readonly string? _oldImage;
    private string? _keepImage;
    private string? _newImageFile;
    private BitmapSource? _newImageBitmap;

    public DeviceEditor(Device? device, IEnumerable<string> categories)
    {
        InitializeComponent();
        Title = device is null ? "New device" : $"Edit: {device.Name}";
        CategoryBox.ItemsSource = categories.Distinct().OrderBy(c => c).ToList();

        if (device is not null)
        {
            NameBox.Text = device.Name;
            ValueBox.Text = device.Watts.ToString("0.##", CultureInfo.CurrentCulture);
            CategoryBox.Text = device.Category;
            ContinuousBox.IsChecked = device.Continuous;
            MotorBox.IsChecked = device.Motor;
            WallOnlyBox.IsChecked = device.WallOnly;
            NoteBox.Text = device.Note;
            LinkBox.Text = device.Link ?? "";
            _oldImage = _keepImage = device.Image;
            ShowPreview(DeviceStore.LoadImage(DeviceStore.ImagePath(device), 200));
        }
        else
        {
            CategoryBox.Text = "Other";
        }
        Loaded += (_, _) => NameBox.Focus();
    }

    private double? Watts()
    {
        if (!double.TryParse(ValueBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) || v <= 0)
            return null;
        return UnitBox.SelectedIndex == 1 ? Electric.AmpsToWatts(v) : v;
    }

    private void Value_Changed(object sender, RoutedEventArgs e)
    {
        if (ConvertedText is null) return;
        var w = Watts();
        ConvertedText.Text = w is null ? "" : $"= {w:0} W, {Electric.WattsToAmps(w.Value):0.0} A";
    }

    private void ShowPreview(BitmapSource? image)
    {
        PicturePreview.Source = image;
        NoPictureText.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UseNewPicture(string? file, BitmapSource? bitmap, BitmapSource preview)
    {
        _newImageFile = file;
        _newImageBitmap = bitmap;
        _keepImage = null;
        ShowPreview(preview);
    }

    private void ChoosePicture_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose a picture",
            Filter = "Pictures (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
        };
        if (dlg.ShowDialog(this) != true) return;
        var preview = DeviceStore.LoadImage(dlg.FileName, 200);
        if (preview is null)
        {
            MessageBox.Show(this, "That file couldn't be opened as a picture.", "PowerCheck");
            return;
        }
        UseNewPicture(dlg.FileName, null, preview);
    }

    private void PastePicture_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsFileDropList())
        {
            var file = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault();
            var loaded = DeviceStore.LoadImage(file, 200);
            if (file is not null && loaded is not null)
            {
                UseNewPicture(file, null, loaded);
                return;
            }
        }
        var bmp = Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
        if (bmp is null)
        {
            MessageBox.Show(this, "There's no picture on the clipboard. Right-click a picture and choose Copy image first.", "PowerCheck");
            return;
        }
        UseNewPicture(null, bmp, bmp);
    }

    private void RemovePicture_Click(object sender, RoutedEventArgs e)
    {
        _newImageFile = null;
        _newImageBitmap = null;
        _keepImage = null;
        ShowPreview(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        var w = Watts();
        if (name.Length == 0 || w is null)
        {
            MessageBox.Show(this, "Give it a name and the number from its label.", "PowerCheck");
            return;
        }
        if (w > 5000)
        {
            MessageBox.Show(this, "That's more than any 120 V household outlet can supply. Check the number (a 240 V device can't go on an extension cord).", "PowerCheck");
            return;
        }

        string link = LinkBox.Text.Trim();
        if (link.Length > 0 && !(Uri.TryCreate(link, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")))
        {
            MessageBox.Show(this, "The product link should be a web address starting with https://", "PowerCheck");
            return;
        }

        string? image = _keepImage;
        try
        {
            if (_newImageFile is not null) image = DeviceStore.ImportImage(_newImageFile, name);
            else if (_newImageBitmap is not null) image = DeviceStore.ImportImage(_newImageBitmap, name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Couldn't save the picture.\n\n" + ex.Message, "PowerCheck");
            return;
        }
        ReplacedImage = _oldImage is not null && _oldImage != image ? _oldImage : null;

        Result = new Device
        {
            Name = name,
            Watts = Math.Round(w.Value, 1),
            Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "Other" : CategoryBox.Text.Trim(),
            Continuous = ContinuousBox.IsChecked == true,
            Motor = MotorBox.IsChecked == true,
            WallOnly = WallOnlyBox.IsChecked == true,
            Note = NoteBox.Text.Trim(),
            Image = image,
            Link = link.Length > 0 ? link : null,
        };
        DialogResult = true;
    }
}
