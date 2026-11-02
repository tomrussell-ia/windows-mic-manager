using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.WinUI.Services;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Main window - hidden, hosts system tray icon
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private Views.MicrophoneWindow? _flyoutWindow;
    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StartupMenuText => StartupService.IsStartupEnabled() ? "✓ Start with Windows" : "Start with Windows";

    public MainWindow()
    {
        InitializeComponent();

        // Don't show in taskbar/switchers
        AppWindow.IsShownInSwitchers = false;

        // Subscribe to Activated event to hide the window after it's shown
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    [RelayCommand]
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

    [RelayCommand]
    private void ExitApp()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Close flyout window
        try
        {
            _flyoutWindow?.Close();
        }
        catch { }

        DisposeServices();

        // Close this window
        try
        {
            this.Close();
        }
        catch { }

        // Try graceful exit first
        try
        {
            Application.Current.Exit();
        }
        catch { }

        // Force exit if graceful exit didn't work
        Environment.Exit(0);
    }

    [RelayCommand]
    private void IconAttribution()
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

    [RelayCommand]
    private void ToggleStartup()
    {
        StartupService.ToggleStartup();
        OnPropertyChanged(nameof(StartupMenuText));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        DisposeServices();
    }

    /// <summary>
    /// Disposes tray icon, ViewModels, audio service, and DI host.
    /// Guarded by _isDisposed to ensure this runs at most once.
    /// </summary>
    private void DisposeServices()
    {
        // Dispose tray icon first (important to remove from system tray)
        try
        {
            TrayIcon?.Dispose();
        }
        catch { }

        // Dispose TrayViewModel (unsubscribes service events)
        try
        {
            if (App.TrayViewModel is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }
        }
        catch { }

        // Dispose audio service (stops background threads and releases COM objects)
        try
        {
            if (App.AudioService is IDisposable disposableService)
            {
                disposableService.Dispose();
            }
        }
        catch { }

        // Dispose DI host
        try
        {
            App.Host?.Dispose();
        }
        catch { }
    }
}
