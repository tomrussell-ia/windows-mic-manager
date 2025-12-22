using System;
using System.Globalization;
using System.Windows.Data;

namespace MicrophoneManager.Converters;

public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;

        if (values[0] is not double percent) return 0.0;
        if (values[1] is not double totalWidth) return 0.0;

        if (double.IsNaN(totalWidth) || double.IsInfinity(totalWidth) || totalWidth <= 0)
        {
            return 0.0;
        }

        var clamped = Math.Max(0.0, Math.Min(100.0, percent));
        var width = totalWidth * (clamped / 100.0);

        if (parameter == null)
        {
            return width;
        }

        var paramText = parameter.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(paramText))
        {
            return width;
        }

        // Special mode: remaining width (total - filled), optionally subtracting a constant.
        // Examples:
        // - ConverterParameter="remaining"
        // - ConverterParameter="remaining:2"  (subtract 2px)
        if (paramText.StartsWith("remaining", StringComparison.OrdinalIgnoreCase))
        {
            var subtract = 0.0;
            var parts = paramText.Split(':');
            if (parts.Length >= 2)
            {
                _ = double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out subtract);
            }

            var remaining = totalWidth - width - subtract;
            return Math.Max(0.0, remaining);
        }

        // Default mode: treat parameter as a "subtract" value (e.g., indicator width)
        // and clamp to [0 .. totalWidth - subtract].
        if (double.TryParse(paramText, NumberStyles.Float, CultureInfo.InvariantCulture, out var subtractPixels))
        {
            var max = Math.Max(0.0, totalWidth - subtractPixels);
            return Math.Max(0.0, Math.Min(max, width));
        }

        return width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
