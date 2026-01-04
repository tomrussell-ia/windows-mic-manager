using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MicrophoneManager.Core.ViewModels;

namespace MicrophoneManager.WinUI.Views;

public sealed partial class MicrophoneFlyout : UserControl
{
    public MicrophoneListViewModel ViewModel { get; }

    public MicrophoneFlyout()
    {
        // Get ViewModel from DI
        var audioService = App.Host.Services.GetRequiredService<MicrophoneManager.Core.Services.IAudioDeviceService>();
        ViewModel = new MicrophoneListViewModel(audioService);

        InitializeComponent();
    }

    // Helper functions for x:Bind
    public string FormatPercent(double value) => $"{value:0}%";

    public double CalculateMeterWidth(double percent)
    {
        // For Stage B, return a fixed percentage of 370px width (card width)
        // Stage C will use proper MultiBinding with actual control width
        var maxWidth = 354.0; // ~370 minus padding
        return Math.Max(0, Math.Min(maxWidth, maxWidth * percent / 100.0));
    }

    public string GetMuteIcon(bool isMuted) => isMuted ? "\uE74F" : "\uE720";

    public string GetMuteTooltip(bool isMuted) => isMuted ? "Unmute" : "Mute";

    public Brush GetDefaultButtonBackground(bool isDefault)
    {
        return isDefault
            ? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 61, 61, 61));
    }

    public Brush GetCommButtonBackground(bool isDefaultComm)
    {
        return isDefaultComm
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 106, 27, 154)) // Purple
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 61, 61, 61));
    }
}
