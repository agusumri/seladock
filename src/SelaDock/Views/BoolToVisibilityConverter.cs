using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SelaDock.Views;

/// <summary>true -> Visible, false -> Collapsed (so the layout reclaims the space).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
