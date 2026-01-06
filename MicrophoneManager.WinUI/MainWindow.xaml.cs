using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using MicrophoneManager.WinUI.Services;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Main window - hidden, hosts system tray icon
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private Views.MicrophoneWindow? _flyoutWindow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ShowFlyoutCommand { get; }
    public ICommand IconAttributionCommand { get; }
    public ICommand ToggleStartupCommand { get; }
    public ICommand ExitCommand { get; }

    public string StartupMenuText => StartupService.IsStartupEnabled() ? "✓ Start with Windows" : "Start with Windows";

    public MainWindow()
    {
        // Create commands before InitializeComponent (needed for x:Bind)
        ShowFlyoutCommand = new RelayCommand(() => ShowFlyout());
        IconAttributionCommand = new RelayCommand(() => IconAttribution());
        ToggleStartupCommand = new RelayCommand(() => { ToggleStartup(); OnPropertyChanged(nameof(StartupMenuText)); });
        ExitCommand = new RelayCommand(() => ExitApp());

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

    private void ExitApp()
    {
        // Close flyout window
        try
        {
            _flyoutWindow?.Close();
        }
        catch { }

        // Dispose tray icon first (important to remove from system tray)
        try
        {
            TrayIcon?.Dispose();
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

    private void ToggleStartup()
    {
        StartupService.ToggleStartup();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Dispose tray icon
        try
        {
            TrayIcon?.Dispose();
        }
        catch { }

        // Dispose audio service
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
