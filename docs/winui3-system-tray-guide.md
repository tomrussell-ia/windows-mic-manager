# WinUI 3 System Tray Application Guide

**Date**: 2026-01-04
**Status**: Implementation Guide
**Target Framework**: .NET 8 with Windows App SDK 1.8+

## Executive Summary

This document provides comprehensive guidance for building WinUI 3 applications with system tray (NotifyIcon) functionality. It addresses the unique challenges of keeping a WinUI 3 app running with a hidden window while displaying a system tray icon.

## Table of Contents

1. [Core Concepts](#1-core-concepts)
2. [The Hidden Window Problem](#2-the-hidden-window-problem)
3. [H.NotifyIcon Library](#3-hnotifyicon-library)
4. [Implementation Patterns](#4-implementation-patterns)
5. [Application Lifecycle](#5-application-lifecycle)
6. [Best Practices](#6-best-practices)
7. [Troubleshooting](#7-troubleshooting)

---

## 1. Core Concepts

### 1.1 WinUI 3 vs WPF Differences

| Aspect | WPF | WinUI 3 |
|--------|-----|---------|
| Window hiding | `ShowInTaskbar=false`, `WindowState=Minimized` | `AppWindow.Hide()` or off-screen positioning |
| Message loop | Continues with hidden window | May exit when all windows hidden |
| System tray | `System.Windows.Forms.NotifyIcon` | `H.NotifyIcon.WinUI` or Win32 interop |
| Window visibility | `Visibility` property | `AppWindow.IsVisible`, `Show()`, `Hide()` |
| Dispatcher | `System.Windows.Threading.Dispatcher` | `Microsoft.UI.Dispatching.DispatcherQueue` |

### 1.2 Key WinUI 3 APIs

```csharp
// Window management
Microsoft.UI.Xaml.Window           // XAML window class
Microsoft.UI.Windowing.AppWindow   // Low-level window control
Microsoft.UI.Windowing.OverlappedPresenter  // Window behavior/chrome

// Lifecycle
Microsoft.UI.Dispatching.DispatcherQueue  // UI thread access
Microsoft.UI.Dispatching.DispatcherQueueTimer  // Timer for UI updates
```

### 1.3 AppWindow API Reference

The `AppWindow` class (from `Microsoft.UI.Windowing`) provides control over window behavior:

| Method/Property | Description |
|----------------|-------------|
| `Hide()` | Hides window from all UX representations (taskbar, Alt+Tab) |
| `Show()` / `Show(bool)` | Shows window, optionally with/without activation |
| `IsVisible` | Returns whether window is currently shown |
| `IsShownInSwitchers` | Controls visibility in Alt+Tab and taskbar |
| `MoveAndResize()` | Positions and sizes the window |
| `SetPresenter()` | Sets window presenter (overlapped, compact, fullscreen) |

---

## 2. The Hidden Window Problem

### 2.1 Problem Statement

In WinUI 3 (Windows App SDK), when all windows are hidden or closed, the application's message loop may exit, causing the app to terminate. This is different from WPF where a hidden window with `ShowInTaskbar=false` keeps the app running.

**Key Finding**: Calling `AppWindow.Hide()` immediately in the constructor or before the window is fully activated can cause the WinUI message loop to exit.

### 2.2 Why This Happens

From Microsoft documentation:
> "If this is the last window to be closed, usually the app's MainWindow, the application will be terminated."
>
> Source: [Windows App SDK app lifecycle](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle)

WinUI 3's default behavior is:
1. When the last XAML window closes, the DispatcherQueue shuts down
2. When the DispatcherQueue shuts down, the message loop exits
3. The app terminates

### 2.3 Solutions

#### Solution 1: Off-Screen Window (Recommended)

Position the window off-screen instead of hiding it. This keeps the window "visible" to the system while invisible to the user.

```csharp
public MainWindow()
{
    InitializeComponent();

    // Don't show in taskbar/Alt+Tab
    AppWindow.IsShownInSwitchers = false;

    // Subscribe to Activated to move off-screen after window is shown
    Activated += MainWindow_Activated;
}

private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
{
    // Only do this once
    Activated -= MainWindow_Activated;

    // Move window off-screen with minimal size
    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
    {
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, 1, 1));
    });
}
```

**Why this works**: The window remains technically visible (has a valid HWND with WS_VISIBLE), so the message loop continues running.

#### Solution 2: Minimize (Alternative)

Use the OverlappedPresenter to minimize the window:

```csharp
private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
{
    Activated -= MainWindow_Activated;

    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
    {
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter?.Minimize();
    });
}
```

**Caveat**: Window may briefly appear in taskbar even with `IsShownInSwitchers = false`.

#### Solution 3: DispatcherShutdownMode (Windows App SDK 1.5+)

Windows App SDK 1.5 introduced `Application.DispatcherShutdownMode` to control when the message loop exits:

```csharp
// In App.xaml.cs
public App()
{
    InitializeComponent();

    // Prevent auto-exit when windows close
    DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
}
```

**Important**: This requires Windows App SDK 1.5 or later.

### 2.4 What NOT To Do

1. **Don't call `Hide()` in constructor**: The HWND may not exist yet
2. **Don't set window size to 0,0**: May cause immediate app exit
3. **Don't call `Hide()` synchronously after `Activate()`**: May race with window creation
4. **Don't assume WPF patterns work**: WinUI 3 has different lifecycle semantics

---

## 3. H.NotifyIcon Library

### 3.1 Overview

[H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) is a cross-platform NotifyIcon library supporting:
- .NET 6+ WPF
- WinUI 3 (Windows App SDK)
- Uno Platform
- Console applications

**NuGet Package**: `H.NotifyIcon.WinUI`

### 3.2 Installation

```xml
<PackageReference Include="H.NotifyIcon.WinUI" Version="2.1.3" />
```

### 3.3 Basic Usage (XAML)

```xml
<Window
    xmlns:tb="using:H.NotifyIcon">

    <Grid>
        <tb:TaskbarIcon x:Name="TrayIcon"
                        ToolTipText="My Application">

            <tb:TaskbarIcon.IconSource>
                <!-- Option 1: Generated icon -->
                <tb:GeneratedIconSource Text="M"
                                        Foreground="White"
                                        Background="DodgerBlue"
                                        FontSize="48"/>

                <!-- Option 2: Icon file
                <BitmapIcon UriSource="ms-appx:///Assets/tray.ico"/> -->
            </tb:TaskbarIcon.IconSource>

            <tb:TaskbarIcon.ContextFlyout>
                <MenuFlyout>
                    <MenuFlyoutItem Text="Show" Click="ShowFlyout_Click"/>
                    <MenuFlyoutSeparator/>
                    <MenuFlyoutItem Text="Exit" Click="Exit_Click"/>
                </MenuFlyout>
            </tb:TaskbarIcon.ContextFlyout>
        </tb:TaskbarIcon>
    </Grid>
</Window>
```

### 3.4 Key Features

| Feature | Description |
|---------|-------------|
| `IconSource` | Supports BitmapIcon, custom sources, or GeneratedIconSource |
| `ContextFlyout` | MenuFlyout for right-click context menu |
| `TrayPopup` | Custom popup for left-click |
| `TrayToolTip` | Custom tooltip control |
| `ToolTipText` | Simple text tooltip |
| `MenuActivation` | LeftClick, RightClick, LeftOrRightClick, DoubleClick |
| `PopupActivation` | When to show TrayPopup |

### 3.5 Efficiency Mode (Windows 11)

H.NotifyIcon provides Windows 11 Efficiency Mode integration:

```csharp
// Enable efficiency mode when window is hidden
WindowExtensions.Hide(window, enableEfficiencyMode: true);

// Disable efficiency mode when window is shown
WindowExtensions.Show(window, disableEfficiencyMode: true);

// Force-create tray icon with efficiency mode
TaskbarIcon.ForceCreate(enablesEfficiencyMode: true);
```

### 3.6 Event Handling

```csharp
// Left-click handler
private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
{
    // Show flyout or main window
    ShowFlyout();
}

// Double-click handler
TrayIcon.TrayLeftMouseDoubleClick += (s, e) =>
{
    // Open main interface
};
```

---

## 4. Implementation Patterns

### 4.1 Pattern A: Hidden Main Window with Tray Icon

The main window hosts the tray icon but is never shown to the user.

**MainWindow.xaml.cs**:
```csharp
public sealed partial class MainWindow : Window
{
    private Views.FlyoutWindow? _flyoutWindow;

    public MainWindow()
    {
        InitializeComponent();

        // Prevent taskbar/Alt+Tab visibility
        AppWindow.IsShownInSwitchers = false;

        // Hide after activation
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;

        // Move off-screen (keeps message loop alive)
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            AppWindow.MoveAndResize(new RectInt32(-32000, -32000, 1, 1));
        });
    }

    private void ShowFlyout_Click(object sender, RoutedEventArgs e)
    {
        _flyoutWindow ??= new Views.FlyoutWindow();
        _flyoutWindow.Activate();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _flyoutWindow?.Close();
        TrayIcon?.Dispose();
        Application.Current.Exit();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        TrayIcon?.Dispose();
    }
}
```

### 4.2 Pattern B: Popup Flyout from Tray

Show a flyout popup directly from the tray icon click.

```xml
<tb:TaskbarIcon x:Name="TrayIcon">
    <tb:TaskbarIcon.TrayPopup>
        <Border Background="{ThemeResource LayerFillColorDefaultBrush}"
                CornerRadius="8"
                Padding="12">
            <local:MicrophoneFlyout/>
        </Border>
    </tb:TaskbarIcon.TrayPopup>
</tb:TaskbarIcon>
```

**Note**: TrayPopup positioning near the tray icon is handled automatically by H.NotifyIcon.

### 4.3 Pattern C: Separate Flyout Window

Create a separate window for more control over positioning and behavior.

**FlyoutWindow.xaml.cs**:
```csharp
public sealed partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Configure borderless popup-style window
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        AppWindow.SetPresenter(presenter);

        // Extend content into title bar (hide title bar)
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        // Handle ESC to close
        if (Content is UIElement element)
        {
            element.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                    Close();
            };
        }
    }
}
```

---

## 5. Application Lifecycle

### 5.1 Startup Flow

```
Program.Main()
    └─> WinRT.ComWrappersSupport.InitializeComWrappers()
    └─> Application.Start()
        └─> App() constructor
            └─> InitializeComponent()
            └─> Build DI container
        └─> App.OnLaunched()
            └─> DispatcherQueue.GetForCurrentThread()
            └─> Create MainWindow (from DI)
            └─> MainWindow.Activate()
                └─> HWND created
                └─> Activated event fires
                    └─> Move window off-screen
                    └─> Tray icon now visible
```

### 5.2 Custom Entry Point (Program.cs)

For unpackaged apps, a custom entry point provides better error handling:

```csharp
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MicrophoneManager.WinUI;

public static class Program
{
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "startup_error.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        };

        try
        {
            Log("=== Application starting ===");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);

                var app = new App();
                app.UnhandledException += (sender, args) =>
                {
                    Log($"APP EXCEPTION: {args.Exception}");
                    args.Handled = true;
                };
            });
        }
        catch (Exception ex)
        {
            Log($"FATAL ERROR: {ex}");
            MessageBox(IntPtr.Zero, $"Failed to start:\n\n{ex.Message}", "Error", 0x10);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
```

**Project Configuration**:
```xml
<PropertyGroup>
    <DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>
</PropertyGroup>
```

### 5.3 Shutdown Flow

```
Exit menu clicked
    └─> Close flyout windows
    └─> TrayIcon.Dispose()
    └─> Application.Current.Exit()
        └─> Window.Closed event
        └─> DispatcherQueue shutdown
        └─> Process exit
```

---

## 6. Best Practices

### 6.1 Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
    <DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.250907003" />
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.1.3" />
  </ItemGroup>
</Project>
```

### 6.2 Dependency Injection Setup

```csharp
public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static DispatcherQueue MainDispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Services
                services.AddSingleton<IAudioDeviceService, AudioDeviceService>();

                // ViewModels
                services.AddSingleton<TrayViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
                services.AddTransient<Views.FlyoutWindow>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        mainWindow.Activate();
    }
}
```

### 6.3 Thread Safety

Always use DispatcherQueue for UI updates from background threads:

```csharp
// In ViewModel or Service
private void OnDeviceChanged(object? sender, EventArgs e)
{
    App.MainDispatcherQueue.TryEnqueue(() =>
    {
        // Update UI-bound properties here
        UpdateDeviceList();
    });
}
```

### 6.4 Resource Cleanup

```csharp
private void MainWindow_Closed(object sender, WindowEventArgs args)
{
    // Dispose tray icon
    TrayIcon?.Dispose();

    // Dispose services
    if (App.Host != null)
    {
        var audioService = App.Host.Services.GetService<IAudioDeviceService>();
        audioService?.Dispose();
    }
}
```

---

## 7. Troubleshooting

### 7.1 App Exits Immediately After Start

**Symptoms**: App starts, tray icon may flash briefly, then exits.

**Causes**:
1. Calling `AppWindow.Hide()` too early
2. Setting window size to 0,0
3. No visible windows keeping message loop alive

**Solutions**:
- Use off-screen positioning instead of `Hide()`
- Defer hiding until after `Activated` event
- Use `DispatcherShutdownMode.OnExplicitShutdown` (SDK 1.5+)

### 7.2 Tray Icon Not Appearing

**Symptoms**: App runs but no icon in system tray.

**Causes**:
1. H.NotifyIcon not finding parent window
2. Icon source not valid
3. Window closed before icon could initialize

**Solutions**:
- Ensure TaskbarIcon is in the XAML tree before window activation
- Use `GeneratedIconSource` for testing
- Check Windows "Hidden Icons" overflow area

### 7.3 Context Menu Not Showing

**Symptoms**: Right-click on tray icon does nothing.

**Causes**:
1. `ContextFlyout` not set correctly
2. MenuFlyout items missing Click handlers

**Solutions**:
- Verify XAML namespace: `xmlns:tb="using:H.NotifyIcon"`
- Check MenuFlyoutItem has Click or Command

### 7.4 Window Flashes Before Hiding

**Symptoms**: Window briefly visible when app starts.

**Solutions**:
- Set `AppWindow.IsShownInSwitchers = false` in constructor
- Move off-screen immediately in Activated handler
- Don't set explicit window size in XAML

### 7.5 Build Errors

**Error**: `NETSDK1005: Assets file ... doesn't have a target for 'net8.0-windows10.0.19041.0'`

**Solution**: Run `dotnet restore` with explicit platform:
```powershell
dotnet restore -p:Platform=x64
dotnet build -p:Platform=x64
```

**Error**: `MSB4062: Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent`

**Solution**: Add to project file:
```xml
<EnableMsixTooling>true</EnableMsixTooling>
```

---

## Appendix A: Complete Example

See the implementation in:
- [MainWindow.xaml](../MicrophoneManager.WinUI/MainWindow.xaml)
- [MainWindow.xaml.cs](../MicrophoneManager.WinUI/MainWindow.xaml.cs)
- [Program.cs](../MicrophoneManager.WinUI/Program.cs)
- [App.xaml.cs](../MicrophoneManager.WinUI/App.xaml.cs)

## Appendix B: References

1. [Microsoft Docs: Windowing Overview](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview)
2. [Microsoft Docs: Windows App SDK App Lifecycle](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle)
3. [Microsoft Docs: AppWindow.Hide](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.hide)
4. [H.NotifyIcon GitHub](https://github.com/HavenDV/H.NotifyIcon)
5. [Windows App SDK 1.5 Release Notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-5)

---

**Document Version**: 1.0
**Last Updated**: 2026-01-04
