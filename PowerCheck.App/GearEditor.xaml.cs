using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>Add or change a power strip, hub, splitter or extension cord you own.</summary>
public partial class GearEditor : Window
{
    private static readonly int[] Gauges = [0, 16, 14, 12, 10];

    public Gear? Result { get; private set; }

    /// <summary>Picture file this item no longer uses (so it can be cleaned up).</summary>
    public string? ReplacedImage { get; private set; }

    private readonly string? _oldImage;
    private string? _keepImage;
    private string? _newImageFile;
    private BitmapSource? _newImageBitmap;

    public GearEditor(Gear? gear)
    {
        InitializeComponent();
        Title = gear is null ? "New strip, hub or cord" : $"Edit: {gear.Name}";
        TypeBox.ItemsSource = Gear.Types;
        TypeBox.SelectedItem = gear?.Type ?? Gear.Types[0];

        if (gear is not null)
        {
            NameBox.Text = gear.Name;
            AmpsBox.Text = gear.Amps.ToString("0.#", CultureInfo.CurrentCulture);
            OutletsBox.Text = gear.Outlets.ToString(CultureInfo.CurrentCulture);
            GaugeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Gauges, gear.Gauge));
            LengthBox.Text = gear.LengthFeet.ToString(CultureInfo.CurrentCulture);
            NoteBox.Text = gear.Note;
            LinkBox.Text = gear.Link ?? "";
            _oldImage = _keepImage = gear.Image;
            ShowPreview(DeviceStore.LoadImage(DeviceStore.ImagePath(new Device { Name = gear.Name, Watts = 1, Image = gear.Image }), 200));
        }
        Loaded += (_, _) => NameBox.Focus();
    }

    private void TypeBox_Changed(object sender, RoutedEventArgs e)
    {
        if (CordFields is null) return;
        string type = TypeBox.SelectedItem as string ?? "";
        CordFields.Visibility = type is "Extension cord" or "Splitter cord" ? Visibility.Visible : Visibility.Collapsed;
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
        bool ampsOk = double.TryParse(AmpsBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var amps) && amps > 0 && amps <= 30;
        bool outletsOk = int.TryParse(OutletsBox.Text, out var outlets) && outlets is > 0 and <= 24;
        if (name.Length == 0 || !ampsOk || !outletsOk)
        {
            MessageBox.Show(this, "Give it a name, its amp rating (for example 13 or 15) and how many outlets it has.", "PowerCheck");
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

        int.TryParse(LengthBox.Text, out var length);
        Result = new Gear
        {
            Name = name,
            Type = TypeBox.SelectedItem as string ?? Gear.Types[0],
            Amps = amps,
            Outlets = outlets,
            Gauge = Gauges[Math.Max(0, GaugeBox.SelectedIndex)],
            LengthFeet = Math.Max(0, length),
            Note = NoteBox.Text.Trim(),
            Image = image,
            Link = link.Length > 0 ? link : null,
        };
        DialogResult = true;
    }
}
