using System.Windows;
using MicrophoneManager.Services;
using MicrophoneManager.ViewModels;
using MicrophoneManager.Views;
using Application = System.Windows.Application;

namespace MicrophoneManager;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public static TrayViewModel? TrayViewModel { get; set; }
    public static AudioDeviceService? AudioService { get; set; }
    public static FlyoutWindow? DockedWindow { get; set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Create and show the main window (hosts tray icon)
        // The window will hide itself after loading
        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DockedWindow?.Close();
        }
        catch { }

        AudioService?.Dispose();
        base.OnExit(e);
    }
}
