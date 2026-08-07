using System.Globalization;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>bool → 透明度（true=1，false=0.5），用于禁用区域置灰。</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1.0 : 0.45;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
