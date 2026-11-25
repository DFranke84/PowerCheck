using System.Globalization;
using System.Windows.Data;
using PowerCheck.Core;

namespace PowerCheck.App;

/// <summary>Shows a device's name with a star in front when it is a favorite.</summary>
public sealed class DeviceLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Device d
            ? (DeviceStore.Current?.IsFavorite(d.Name) == true ? "★ " : "     ") + d.Name
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
