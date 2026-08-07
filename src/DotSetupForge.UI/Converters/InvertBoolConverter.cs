using System.Globalization;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>布尔取反：用于 IsEnabled 绑定。</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
