using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Converts a percentage (0-100) and track width to pixel width for meter visualization.
/// Parameter can be "remaining" to invert, or "1"/"2" for offset pixel widths.
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not double percent) return 0.0;

        // For now, return a simple percentage calculation
        // In full implementation, this would use MultiBinding with track width
        // For Stage B, we'll use a simpler approach with x:Bind functions
        return Math.Max(0.0, Math.Min(100.0, percent));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
