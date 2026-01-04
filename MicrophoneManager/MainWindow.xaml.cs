using System.Windows;
using MicrophoneManager.Services;
using MicrophoneManager.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MicrophoneManager;

public partial class MainWindow : Window
{
    public TrayViewModel ViewModel { get; } = null!;
    private System.Drawing.Icon? _currentTrayIcon;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            // Initialize services
            var audioService = new AudioDeviceService();
            App.AudioService = audioService;

            // Initialize ViewModel with callback to update icon
            ViewModel = new TrayViewModel(audioService, UpdateTrayIcon);
            App.TrayViewModel = ViewModel;

            // Set DataContext for bindings
            DataContext = ViewModel;
            TrayIcon.DataContext = ViewModel;

            // Set initial icon
            UpdateTrayIcon(ViewModel.IsMuted);

            // Handle window close to clean up
            this.Closed += MainWindow_Closed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing: {ex.Message}", "Microphone Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Hide the window after it's loaded
        this.Hide();
    }

    private void UpdateTrayIcon(bool isMuted)
    {
        try
        {
            var newIcon = IconGenerator.CreateMicrophoneIcon(isMuted);

            // Dispose old icon to prevent memory/GDI handle leaks
            var oldIcon = _currentTrayIcon;
            _currentTrayIcon = newIcon;
            TrayIcon.Icon = newIcon;

            // Dispose after setting new icon to avoid flicker
            oldIcon?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Icon error: {ex.Message}");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Dispose current icon
        _currentTrayIcon?.Dispose();
        _currentTrayIcon = null;

        TrayIcon?.Dispose();
        App.AudioService?.Dispose();
    }
}
