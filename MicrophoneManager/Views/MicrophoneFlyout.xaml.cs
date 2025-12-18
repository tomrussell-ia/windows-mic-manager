using System.Windows;
using System.Windows.Controls;
using MicrophoneManager.Models;
using MicrophoneManager.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MicrophoneManager.Views;

public partial class MicrophoneFlyout : UserControl
{
    public MicrophoneListViewModel ViewModel { get; }

    public MicrophoneFlyout()
    {
        InitializeComponent();

        // Initialize ViewModel with the shared AudioService
        if (App.AudioService != null)
        {
            ViewModel = new MicrophoneListViewModel(App.AudioService);
        }
        else
        {
            ViewModel = new MicrophoneListViewModel(new Services.AudioDeviceService());
        }

        DataContext = ViewModel;

        // Subscribe to mute changes to update UI
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsMuted))
            {
                UpdateMuteButton();
            }
        };

        // Refresh devices and update mute button when flyout becomes visible
        this.IsVisibleChanged += (s, e) =>
        {
            if (this.IsVisible)
            {
                ViewModel.RefreshDevices();
                UpdateMuteButton();
            }
        };

        // Initial mute button state
        UpdateMuteButton();
    }

    private void UpdateMuteButton()
    {
        MuteIcon.Text = ViewModel.IsMuted ? "\uE74F" : "\uE720";
        MuteText.Text = ViewModel.IsMuted ? "Unmute Microphone" : "Mute Microphone";
    }

    private void MicrophoneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicrophoneList.SelectedItem is MicrophoneDevice device && !device.IsDefault)
        {
            ViewModel.SelectMicrophoneCommand.Execute(device);
            MicrophoneList.SelectedItem = null; // Clear selection
        }
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleMuteCommand.Execute(null);
    }
}
