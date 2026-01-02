using System;
using System.Globalization;
using System.Windows.Data;

namespace MicrophoneManager.Converters;

public class MuteStateToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isMuted)
        {
            return isMuted ? "Unmute" : "Mute";
        }

        return "Mute";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
