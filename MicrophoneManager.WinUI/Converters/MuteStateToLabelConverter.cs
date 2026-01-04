using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

public class MuteStateToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool isMuted && isMuted ? "Unmute" : "Mute";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
