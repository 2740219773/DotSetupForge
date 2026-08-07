using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>倒置布尔可见性：true → Collapsed，false → Visible。</summary>
public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
