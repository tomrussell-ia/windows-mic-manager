using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Returns 1.0 when true, 0.0 when false.
/// Use instead of Visibility.Hidden (not available in WinUI 3) when an element must
/// occupy layout space even when "invisible" — e.g. a warning row in horizontally-aligned
/// cards that would otherwise misalign peers if it collapsed.
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && b ? 1.0 : 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is double d && d > 0.5;
    }
}
