using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MicrophoneManager.Core.ViewModels;
using Windows.UI;

namespace MicrophoneManager.WinUI.Views;

public sealed partial class MicrophoneFlyout : UserControl
{
    public MicrophoneListViewModel ViewModel { get; }

    public bool IsDockedMode { get; set; }

    public MicrophoneFlyout()
    {
        // Get ViewModel from DI
        var audioService = App.Host.Services.GetRequiredService<MicrophoneManager.Core.Services.IAudioDeviceService>();
        ViewModel = new MicrophoneListViewModel(audioService);

        InitializeComponent();
    }

    private void DockButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsDockedMode)
        {
            // Already docked, undock
            App.DockedWindow?.Close();
            App.DockedWindow = null;
        }
        else
        {
            // Create docked window
            var dockedWindow = new FlyoutWindow
            {
                Title = "Microphone Manager"
            };

            // Mark flyout as docked mode
            var flyout = new MicrophoneFlyout { IsDockedMode = true };
            dockedWindow.Content = flyout;

            App.DockedWindow = dockedWindow;
            dockedWindow.Activate();

            // Close the current popup (if opened from tray)
            var parentWindow = this.XamlRoot?.Content as Window;
            parentWindow?.Close();
        }
    }
}

// Extension methods for MicrophoneEntryViewModel to add helper functions
public static class MicrophoneViewModelExtensions
{
    // Helper functions for x:Bind in DataTemplate
    public static string GetMuteIcon(this MicrophoneEntryViewModel vm, bool isMuted)
    {
        return isMuted ? "\uE74F" : "\uE720";
    }

    public static string GetMuteTooltip(this MicrophoneEntryViewModel vm, bool isMuted)
    {
        return isMuted ? "Unmute" : "Mute";
    }

    public static Color GetDefaultButtonColor(this MicrophoneEntryViewModel vm, bool isDefault)
    {
        return isDefault
            ? Color.FromArgb(255, 30, 136, 229) // Blue
            : Color.FromArgb(255, 61, 61, 61);   // Gray
    }

    public static Color GetCommButtonColor(this MicrophoneEntryViewModel vm, bool isDefaultComm)
    {
        return isDefaultComm
            ? Color.FromArgb(255, 106, 27, 154) // Purple
            : Color.FromArgb(255, 61, 61, 61);   // Gray
    }

    // Meter calculation helpers
    public static double CalculateRemainingWidth(this MicrophoneEntryViewModel vm, double inputPercent, double trackWidth)
    {
        if (trackWidth <= 0) return 0;
        var percent = Math.Max(0, Math.Min(100.0, inputPercent));
        var remaining = (100.0 - percent) / 100.0 * trackWidth;
        return Math.Max(0, remaining);
    }

    public static double CalculateTranslateX(this MicrophoneEntryViewModel vm, double inputPercent, double trackWidth)
    {
        if (trackWidth <= 0) return 0;
        var percent = Math.Max(0, Math.Min(100.0, inputPercent));
        return percent / 100.0 * trackWidth;
    }

    public static double CalculatePeakX(this MicrophoneEntryViewModel vm, double peakPercent, double trackWidth)
    {
        if (trackWidth <= 0) return 0;
        var percent = Math.Max(0, Math.Min(100.0, peakPercent));
        var x = percent / 100.0 * trackWidth;
        // Offset by 1px (half the width of the 2px line) to center it
        return Math.Max(0, x - 1);
    }

    public static double CalculateInputX(this MicrophoneEntryViewModel vm, double inputPercent, double trackWidth)
    {
        if (trackWidth <= 0) return 0;
        var percent = Math.Max(0, Math.Min(100.0, inputPercent));
        return percent / 100.0 * trackWidth;
    }

    // Progressive meter gradient based on dB thresholds (NOT percentage)
    public static Brush GetMeterBrush(this MicrophoneEntryViewModel vm, double inputPercent)
    {
        var percent = Math.Max(0, Math.Min(100.0, inputPercent));
        var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 0) };

        // Standard broadcast metering dB thresholds:
        // Green: -∞ to -20 dBFS (safe zone)
        // Yellow: -20 to -9 dBFS (caution zone)
        // Red: -9 to 0 dBFS (danger/clipping zone)
        const double yellowThresholdDb = -20.0;
        const double redThresholdDb = -9.0;

        // Convert dB thresholds to bar positions (0-1 scale)
        var yellowThresholdPercent = MicrophoneManager.Core.Services.ObsMeterMath.DbToPercent(yellowThresholdDb);
        var redThresholdPercent = MicrophoneManager.Core.Services.ObsMeterMath.DbToPercent(redThresholdDb);
        var yellowOffset = yellowThresholdPercent / 100.0;
        var redOffset = redThresholdPercent / 100.0;

        // Convert current input to dB to determine which zone we're in
        var inputDbFs = MicrophoneManager.Core.Services.ObsMeterMath.PercentToDb(percent);

        if (inputDbFs < yellowThresholdDb)
        {
            // Below -20 dBFS: Solid green only
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = 0.0 }); // #3CCB5C
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = 1.0 });
        }
        else if (inputDbFs < redThresholdDb)
        {
            // Between -20 and -9 dBFS: Green → Yellow gradient
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = 0.0 });  // Green
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = yellowOffset }); // Green until -20dB
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 230, 200, 74), Offset = redOffset }); // Yellow #E6C84A at -9dB
        }
        else
        {
            // Above -9 dBFS: Green → Yellow → Red gradient
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = 0.0 });  // Green #3CCB5C
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 60, 203, 92), Offset = yellowOffset }); // Green until -20dB
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 230, 200, 74), Offset = redOffset }); // Yellow #E6C84A at -9dB
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 228, 91, 91), Offset = 1.0 }); // Red #E45B5B at 0dB
        }

        return brush;
    }
}
