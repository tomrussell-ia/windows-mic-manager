using System;
using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Formats a dBFS value as "-12 dB".
/// </summary>
public class DbFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double db)
        {
            if (double.IsNaN(db)) db = -96.0;
            if (double.IsNegativeInfinity(db)) return "-∞ dB";

            // Clamp to a reasonable meter range for display.
            db = Math.Max(-96.0, Math.Min(0.0, db));
            return $"{db:0} dB";
        }

        return "-96 dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
