using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Converts a boolean value to a Brush for button backgrounds.
/// True returns accent color, False returns hover color.
/// </summary>
public class BoolToButtonBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isActive = value is bool active && active;
        var resourceKey = isActive ? "AccentBrush" : "HoverBrush";

        try
        {
            if (Application.Current?.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }
        }
        catch
        {
        }

        return new SolidColorBrush(isActive
            ? Microsoft.UI.Colors.DodgerBlue
            : Windows.UI.Color.FromArgb(255, 61, 61, 61));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
