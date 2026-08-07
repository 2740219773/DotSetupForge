using System.Globalization;
using System.Windows.Data;
using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.UI.Converters;

/// <summary>枚举相等比较：用于 RadioButton 绑定当前页枚举。</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string text
            ? Enum.TryParse(text, out PageKind page) ? page : Binding.DoNothing
            : Binding.DoNothing;
}
