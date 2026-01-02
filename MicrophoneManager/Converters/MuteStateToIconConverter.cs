using System;
using System.Globalization;
using System.Windows.Data;

namespace MicrophoneManager.Converters;

public class MuteStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isMuted)
        {
            return isMuted ? "\uE74F" : "\uE720";
        }

        return "\uE720";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
