using System.Globalization;
using System.Windows;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>Small window to add a new device or change an existing one.</summary>
public partial class DeviceEditor : Window
{
    public Device? Result { get; private set; }

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

        Result = new Device
        {
            Name = name,
            Watts = Math.Round(w.Value, 1),
            Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "Other" : CategoryBox.Text.Trim(),
            Continuous = ContinuousBox.IsChecked == true,
            Motor = MotorBox.IsChecked == true,
            WallOnly = WallOnlyBox.IsChecked == true,
            Note = NoteBox.Text.Trim(),
        };
        DialogResult = true;
    }
}
