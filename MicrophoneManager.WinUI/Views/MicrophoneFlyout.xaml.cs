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

    public Action? RequestClose { get; set; }

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
            RequestClose?.Invoke();
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
}
