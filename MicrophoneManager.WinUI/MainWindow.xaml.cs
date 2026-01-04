using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Main window - hidden, hosts system tray icon
/// </summary>
public sealed partial class MainWindow : Window
{
    private Views.FlyoutWindow? _flyoutWindow;

    public MainWindow()
    {
        Debug.WriteLine("MainWindow constructor starting");
        InitializeComponent();
        Debug.WriteLine("MainWindow InitializeComponent completed");

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

        // Minimize the window to taskbar area but keep it alive
        // Using ShowMinimized keeps the message loop running
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            Debug.WriteLine("Minimizing MainWindow");
            var presenter = AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.Minimize();
            }
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
            _flyoutWindow = new Views.FlyoutWindow();
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

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        TrayIcon?.Dispose();
    }
}
