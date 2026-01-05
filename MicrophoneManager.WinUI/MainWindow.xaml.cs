using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Main window - hidden, hosts system tray icon
/// </summary>
public sealed partial class MainWindow : Window
{
    private Views.MicrophoneWindow? _flyoutWindow;

    public ICommand ShowFlyoutCommand { get; }

    public MainWindow()
    {
        // Create command before InitializeComponent (needed for x:Bind)
        ShowFlyoutCommand = new RelayCommand(() => ShowFlyout());

        InitializeComponent();

        // Don't show in taskbar/switchers
        AppWindow.IsShownInSwitchers = false;

        // Subscribe to Activated event to hide the window after it's shown
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Only process on first activation
        Activated -= MainWindow_Activated;

        // Move window off-screen to keep message loop alive
        // IMPORTANT: Hide() causes app exit in WinUI 3 - must use off-screen positioning instead
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, 1, 1));
        });
    }

    private void ShowFlyout_Click(object sender, RoutedEventArgs e)
    {
        ShowFlyout();
    }

    private void ShowFlyout()
    {
        if (_flyoutWindow == null || !IsWindowVisible(_flyoutWindow))
        {
            _flyoutWindow = new Views.MicrophoneWindow(isDocked: false);
            _flyoutWindow.Activate();
        }
        else
        {
            _flyoutWindow.Activate();
        }
    }

    private bool IsWindowVisible(Window window)
    {
        try
        {
            return window.AppWindow.IsVisible;
        }
        catch
        {
            return false;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _flyoutWindow?.Close();
        }
        catch { }

        TrayIcon?.Dispose();
        Application.Current.Exit();
    }

    private void IconAttribution_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://www.flaticon.com/free-icons/radio";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        TrayIcon?.Dispose();
    }
}
