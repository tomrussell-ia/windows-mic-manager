using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

public class MuteStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // WinUI 3 Segoe Fluent Icons
        return value is bool isMuted && isMuted
            ? "\uE74F" // MicOff
            : "\uE720"; // Microphone
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
