using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

/// <summary>
/// Formats a percentage value as "X%"
/// </summary>
public class PercentFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double percent)
        {
            return $"{percent:0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
