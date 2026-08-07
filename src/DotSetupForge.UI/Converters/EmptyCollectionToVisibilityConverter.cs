using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>集合/数量可见性：Count == 0 时显示（用于"空状态"提示）。</summary>
public sealed class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Collections.ICollection collection)
        {
            return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
