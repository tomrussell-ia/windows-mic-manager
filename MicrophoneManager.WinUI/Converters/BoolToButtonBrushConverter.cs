using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Converts a boolean value to a Brush for button backgrounds.
/// True returns accent color, False returns hover color.
/// </summary>
public class BoolToButtonBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isActive && isActive)
        {
            // Active state - use accent color
            return new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue); // Similar to #0078D4
        }
        
        // Inactive state - use hover color
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 61, 61, 61)); // #3D3D3D
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
