using System.Globalization;
using System.Windows.Data;

namespace DotSetupForge.UI.Converters;

/// <summary>字符串相等比较：用于 RadioButton/CheckBox 绑定枚举名字符串。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}
