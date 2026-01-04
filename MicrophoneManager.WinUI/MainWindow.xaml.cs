using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
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
        InitializeComponent();

        // Hide the main window (it only hosts the tray icon)
        var appWindow = AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(0, 0));
        appWindow.IsShownInSwitchers = false;

        // Set a placeholder tray icon (Stage B will add proper icon generation)
        try
        {
            // TODO Stage B: Use proper icon generator service
            // For now, set a simple icon or handle missing icon gracefully
            Debug.WriteLine("MainWindow initialized - tray icon placeholder");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tray icon setup warning: {ex.Message}");
        }

        Closed += MainWindow_Closed;
    }

    /// <summary>
    /// Command to show flyout window (left-click tray icon)
    /// </summary>
    public ICommand ShowFlyoutCommand => new RelayCommand(ShowFlyout);

    private void ShowFlyout()
    {
        if (_flyoutWindow == null || !IsWindowVisible(_flyoutWindow))
        {
            _flyoutWindow = new Views.FlyoutWindow();
            _flyoutWindow.Activate();
        }
        else
        {
            // Bring existing window to front
            _flyoutWindow.Activate();
        }
    }

    private bool IsWindowVisible(Window window)
    {
        // Simple check - in production, would track window state more carefully
        try
        {
            return window.AppWindow.IsVisible;
        }
        catch
        {
            return false;
        }
    }

    private void ToggleStartup_Click(object sender, RoutedEventArgs e)
    {
        // TODO Stage B: Implement with StartupService
        Debug.WriteLine("Toggle startup clicked (not yet implemented)");
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        // Close all windows and exit
        try
        {
            _flyoutWindow?.Close();
        }
        catch { }

        TrayIcon?.Dispose();

        // TODO Stage B: Cleanup audio service
        // App.AudioService?.Dispose();

        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        TrayIcon?.Dispose();
        // TODO Stage B: Cleanup services
    }
}

/// <summary>
/// Simple relay command implementation for Stage A
/// TODO Stage B: Use CommunityToolkit.Mvvm.Input.RelayCommand from Core ViewModels
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
