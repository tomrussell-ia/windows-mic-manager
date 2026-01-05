using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Chooses a solid meter color (green/yellow/red) based on dBFS.
/// </summary>
public class DbToMeterBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var db = value is double d ? d : -96.0;
        if (double.IsNaN(db) || double.IsNegativeInfinity(db)) db = -96.0;

        // OBS-style (sample peak) thresholds:
        // warning ~ -20 dBFS (yellow), error ~ -9 dBFS (red).
        var key = db >= -9.0
            ? "MeterRedBrush"
            : (db >= -20.0 ? "MeterYellowBrush" : "MeterGreenBrush");

        if (Application.Current?.Resources?.TryGetValue(key, out var resource) == true && resource is Brush brush)
        {
            return brush;
        }

        // Fallback (shouldn't happen): return default Foreground.
        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
