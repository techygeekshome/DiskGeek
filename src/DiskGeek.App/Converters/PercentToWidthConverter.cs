using System.Globalization;
using Avalonia.Data.Converters;

namespace DiskGeek.App.Converters;

/// <summary>Maps a 0-100 percentage to a pixel width for the little size bars in the tree/list views.</summary>
public sealed class PercentToWidthConverter : IValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    /// <summary>Pixel width that represents 100%.</summary>
    public double MaxWidth { get; set; } = 60;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
            return Math.Clamp(percent, 0, 100) / 100.0 * MaxWidth;

        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
