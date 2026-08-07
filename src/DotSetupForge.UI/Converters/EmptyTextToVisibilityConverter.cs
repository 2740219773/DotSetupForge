using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>空字符串 → Collapsed，非空 → Visible。</summary>
public sealed class EmptyTextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
