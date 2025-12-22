using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MicrophoneManager.Models;
using MicrophoneManager.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace MicrophoneManager.Views;

public partial class MicrophoneFlyout : UserControl
{
    public static readonly DependencyProperty ShowAppsSectionProperty = DependencyProperty.Register(
        nameof(ShowAppsSection),
        typeof(bool),
        typeof(MicrophoneFlyout),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsDockedModeProperty = DependencyProperty.Register(
        nameof(IsDockedMode),
        typeof(bool),
        typeof(MicrophoneFlyout),
        new PropertyMetadata(false));

    public bool ShowAppsSection
    {
        get => (bool)GetValue(ShowAppsSectionProperty);
        set => SetValue(ShowAppsSectionProperty, value);
    }

    public bool IsDockedMode
    {
        get => (bool)GetValue(IsDockedModeProperty);
        set => SetValue(IsDockedModeProperty, value);
    }

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
                UpdateDockButton();
            }
        };

        // Initial mute button state
        UpdateMuteButton();

        // Initial dock button state
        UpdateDockButton();
    }

    private void UpdateDockButton()
    {
        // E718 = Pin, E77A = Unpin (Segoe MDL2 Assets)
        DockIcon.Text = App.DockedWindow != null ? "\uE77A" : "\uE718";
        DockButton.ToolTip = App.DockedWindow != null ? "Undock" : "Dock";
    }

    private void DockButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle a persistent Topmost window that hosts the same flyout UI.
        if (App.DockedWindow != null)
        {
            try
            {
                App.DockedWindow.Close();
            }
            catch { }

            App.DockedWindow = null;
            UpdateDockButton();
            return;
        }

        var dockedWindow = new FlyoutWindow();

        // Position it near the top-right of the primary work area.
        dockedWindow.Loaded += (_, __) =>
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 12;
            dockedWindow.Left = workArea.Right - dockedWindow.Width - margin;
            dockedWindow.Top = workArea.Top + margin;
        };

        dockedWindow.Closed += (_, __) =>
        {
            if (ReferenceEquals(App.DockedWindow, dockedWindow))
            {
                App.DockedWindow = null;
            }

            // If the tray popup is open, refresh the button state.
            Dispatcher.BeginInvoke(UpdateDockButton);
        };

        // Allow quick close via Escape even when docked.
        dockedWindow.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                dockedWindow.Close();
                args.Handled = true;
            }
        };

        App.DockedWindow = dockedWindow;
        dockedWindow.Show();
        dockedWindow.Activate();

        UpdateDockButton();
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
