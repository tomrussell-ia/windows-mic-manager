# WinUI 3 Migration Execution Plan

**Date**: 2026-01-04
**Status**: Ready for Execution
**Estimated Duration**: Incremental (Stage A: 1-2 days, Stage B: 2-3 days, Stage C: 3-5 days)

## Overview

This document outlines the **incremental migration strategy** from WPF to WinUI 3. Each stage produces a **runnable, testable application** to validate progress and minimize risk.

## Migration Philosophy

1. **Incremental Delivery**: Each stage builds on the previous, always maintaining a working app
2. **Risk Mitigation**: Validate critical components (system tray, audio APIs, meters) early
3. **Parallel Development**: Keep WPF version running until WinUI 3 achieves parity
4. **Preserve Business Logic**: Maximize reuse of Services, ViewModels, Models

## Stage Progression

```
Stage A (Skeleton)
    ↓ Validates: App lifecycle, DI, tray icon, build system
Stage B (Vertical Slice)
    ↓ Validates: Audio APIs, UI controls, meters, real-time updates
Stage C (Full Parity)
    ↓ Validates: All workflows, hotplug, multi-device, edge cases
```

---

## Critical Implementation Notes (Verified 2026-01-04)

### Hidden Window Pattern - What Works and What Doesn't

**❌ Does NOT Work:**
- `AppWindow.Hide()` - Causes the WinUI 3 message loop to exit immediately
- `AppWindow.MoveAndResize(0, 0, 0, 0)` - Also causes app exit
- Setting window size to 0x0 in any form - Causes app exit

**✅ WORKS:**
- Off-screen positioning: `AppWindow.MoveAndResize(new RectInt32(-32000, -32000, 1, 1))`
- This keeps the window "visible" to the system but off-screen
- Combined with `AppWindow.IsShownInSwitchers = false` to hide from Alt+Tab

**Why:** WinUI 3's message loop exits when all windows are hidden. The off-screen approach keeps a valid HWND with WS_VISIBLE style, maintaining the message loop.

### Project Configuration That Works

```xml
<PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
    <WindowsPackageType>None</WindowsPackageType>
    <EnableMsixTooling>true</EnableMsixTooling>
    <Platforms>x64</Platforms>
    <DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>
</PropertyGroup>
```

### Custom Entry Point Requirement

For unpackaged WinUI 3 apps, a custom `Program.cs` with `Main` method is required:
- Call `WinRT.ComWrappersSupport.InitializeComWrappers()` before anything else
- Use `Application.Start()` to create the XAML app instance
- This provides better error diagnostics than auto-generated entry point

---

## Stage A: Skeleton App with System Tray

**Goal**: Create functional WinUI 3 application with system tray icon and basic shell

**Success Criteria**:
- ✅ WinUI 3 project builds and runs (VERIFIED 2026-01-04)
- ✅ System tray icon appears (VERIFIED - using H.NotifyIcon.WinUI 2.1.3 with GeneratedIconSource)
- ✅ Left-click shows flyout window (VERIFIED)
- ✅ Right-click shows context menu (Exit command works) (VERIFIED)
- ✅ Dependency injection container initialized (VERIFIED - Microsoft.Extensions.Hosting)
- ✅ Application lifecycle (startup, shutdown) works (VERIFIED - off-screen window pattern keeps message loop alive)
- [ ] Single-file EXE publishes successfully (not yet tested)

### A.1 Project Setup

**Tasks**:
1. Create `MicrophoneManager.Core` class library project
   - Target: `net8.0-windows10.0.19041.0`
   - Add NuGet: `NAudio`, `CommunityToolkit.Mvvm`

2. Create `MicrophoneManager.WinUI` application project
   - Target: `net8.0-windows10.0.19041.0`
   - Add NuGet: `Microsoft.WindowsAppSDK`, `H.NotifyIcon.WinUI` (or fallback)
   - Reference: `MicrophoneManager.Core`

3. Configure build settings
   - Unpackaged deployment: `<EnableMsixTooling>false</EnableMsixTooling>`
   - Self-contained: `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`
   - Single-file publish settings

**Files to Create**:
```
MicrophoneManager.Core/
├── MicrophoneManager.Core.csproj
└── (empty, ready for Stage B)

MicrophoneManager.WinUI/
├── MicrophoneManager.WinUI.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── app.manifest
└── Assets/ (app icons)
```

**Validation**:
```powershell
# Build
dotnet build MicrophoneManager.sln

# Run (should open and minimize to tray)
dotnet run --project MicrophoneManager.WinUI

# Publish single-file EXE
dotnet publish MicrophoneManager.WinUI -c Release
```

### A.2 System Tray Icon Implementation

**Research Task** (do first):
- [ ] Check if `H.NotifyIcon.WinUI` package exists and is maintained
- [ ] If not, evaluate Win32 `Shell_NotifyIcon` interop wrapper approach
- [ ] Test icon rendering (static icon first, dynamic mute/unmute later)

**Implementation** (based on research outcome):

**Option 1: H.NotifyIcon.WinUI**
```xml
<!-- MainWindow.xaml -->
<tb:TaskbarIcon x:Name="TrayIcon"
                ToolTipText="Microphone Manager">
    <tb:TaskbarIcon.ContextMenu>
        <MenuFlyout>
            <MenuFlyoutItem Text="Exit" Click="Exit_Click"/>
        </MenuFlyout>
    </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

**Option 2: Win32 Wrapper**
```csharp
// MicrophoneManager.Core/Services/ITrayIconService.cs
public interface ITrayIconService : IDisposable
{
    void Initialize(IntPtr windowHandle);
    void SetIcon(Icon icon);
    void SetTooltip(string tooltip);
    void ShowContextMenu(Point position, IEnumerable<TrayMenuItem> items);
    event EventHandler? LeftClick;
    event EventHandler? RightClick;
}

// MicrophoneManager.WinUI/Services/Win32TrayIconService.cs
// Implement using Shell_NotifyIcon, NOTIFYICONDATA, etc.
```

**Files to Create**:
- `MicrophoneManager.Core/Services/ITrayIconService.cs` (if Option 2)
- `MicrophoneManager.WinUI/Services/Win32TrayIconService.cs` (if Option 2)

**Validation**:
- Run app, verify tray icon appears
- Right-click, select Exit, verify app closes

### A.3 Dependency Injection Setup

**Files to Create**:
- `MicrophoneManager.WinUI/App.xaml.cs` (with Host builder)

**Code**:
```csharp
public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Placeholder registrations (will expand in Stage B)
        services.AddSingleton<MainWindow>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = Host.Services.GetRequiredService<MainWindow>();
        m_window.Activate();
    }

    private Window? m_window;
}
```

**Validation**:
- Run app, verify DI container initializes without errors
- Check debug output for Host startup

### A.4 Basic Flyout Shell

**Files to Create**:
- `MicrophoneManager.WinUI/Views/FlyoutWindow.xaml`
- `MicrophoneManager.WinUI/Views/FlyoutWindow.xaml.cs`

**Content**: Empty window with "Coming Soon" placeholder text

**Wiring**:
```csharp
// MainWindow: Left-click handler
private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
{
    var flyout = new Views.FlyoutWindow();
    flyout.Activate();
}
```

**Validation**:
- Left-click tray icon, verify flyout window appears
- Press Escape, verify window closes

### A.5 Stage A Deliverables

**Checklist**:
- [ ] Projects created and building
- [ ] NuGet packages installed
- [ ] System tray icon visible
- [ ] Context menu works (Exit command)
- [ ] Flyout window shows/hides
- [ ] DI container initialized
- [ ] Single-file publish succeeds
- [ ] No crashes on startup/shutdown

**Git Commit**: `feat: Stage A - WinUI 3 skeleton app with system tray`

---

## Stage B: Vertical Slice (Single Microphone Card with Meter)

**Goal**: Implement ONE fully functional microphone card with all features

**Why Vertical Slice?**
- Validates audio API integration (NAudio)
- Tests real-time meter updates (16ms timer)
- Verifies MVVM pattern in WinUI 3
- Identifies UI control mapping issues
- Proves DispatcherQueue performance
- Tests volume/mute control roundtrip

**Success Criteria**:
- ✅ Default microphone displays in flyout
- ✅ Real-time input level meter animates smoothly
- ✅ Peak hold indicator works with 5s decay
- ✅ Volume slider controls device volume
- ✅ Mute button toggles device mute state
- ✅ "Set Default" and "Set Communication" buttons work
- ✅ Device name, format tag, and status display correctly
- ✅ Tray icon updates on mute state change

### B.1 Move Core Services

**Tasks**:
1. Copy Services from WPF project to Core:
   - `IAudioDeviceService.cs`
   - `AudioDeviceService.cs`
   - `PolicyConfigService.cs`
   - `StartupService.cs`
   - `ObsMeterMath.cs`

2. Create icon service interface:
   - `IIconGeneratorService.cs` (interface in Core)
   - `IconGeneratorService.cs` (implementation in WinUI project)

**Validation**:
```csharp
// Quick console test
var service = new AudioDeviceService();
var mics = service.GetMicrophones();
Console.WriteLine($"Found {mics.Count} microphones");
service.Dispose();
```

**Files Modified**:
- `MicrophoneManager.Core/Services/*` (new files)

**Git Commit**: `refactor: move audio services to Core project`

### B.2 Move and Adapt ViewModels

**Tasks**:
1. Copy Models to Core:
   - `MicrophoneDevice.cs` (no changes needed)

2. Copy ViewModels to Core:
   - `MicrophoneEntryViewModel.cs` (no Dispatcher usage, reuse as-is)
   - `TrayViewModel.cs` (adapt Dispatcher → DispatcherQueue)
   - `MicrophoneListViewModel.cs` (adapt DispatcherTimer → DispatcherQueueTimer)

**Code Changes for TrayViewModel**:
```csharp
// Before (WPF)
using System.Windows.Threading;
private readonly Dispatcher _dispatcher;

// After (WinUI 3)
using Microsoft.UI.Dispatching;
private readonly DispatcherQueue _dispatcherQueue;

// In constructor
_dispatcherQueue = DispatcherQueue.GetForCurrentThread();

// Event handler
_dispatcherQueue.TryEnqueue(UpdateState);
```

**Code Changes for MicrophoneListViewModel**:
```csharp
// Before (WPF)
_peakHoldTimer = new DispatcherTimer(DispatcherPriority.Background)
{
    Interval = TimeSpan.FromMilliseconds(16)
};
_peakHoldTimer.Tick += (_, _) => TickPeakHold();
_peakHoldTimer.Start();

// After (WinUI 3)
_peakHoldTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
_peakHoldTimer.Interval = TimeSpan.FromMilliseconds(16);
_peakHoldTimer.Tick += (s, e) => TickPeakHold();
_peakHoldTimer.Start();

// InvokeOnUiThread helper
private static void InvokeOnUiThread(Action action)
{
    var queue = DispatcherQueue.GetForCurrentThread();
    if (queue != null)
        queue.TryEnqueue(() => action());
    else
        action(); // Unit tests fallback
}
```

**Files Modified**:
- `MicrophoneManager.Core/Models/MicrophoneDevice.cs`
- `MicrophoneManager.Core/ViewModels/TrayViewModel.cs`
- `MicrophoneManager.Core/ViewModels/MicrophoneListViewModel.cs`
- `MicrophoneManager.Core/ViewModels/MicrophoneEntryViewModel.cs`

**Validation**:
```csharp
// Unit test
var service = new AudioDeviceService();
var vm = new MicrophoneListViewModel(service);
Assert.NotEmpty(vm.Microphones);
```

**Git Commit**: `refactor: migrate ViewModels to Core with DispatcherQueue`

### B.3 Implement Converters

**Files to Create**:
- `MicrophoneManager.WinUI/Converters/BoolToVisibilityConverter.cs`
- `MicrophoneManager.WinUI/Converters/MuteStateToIconConverter.cs`
- `MicrophoneManager.WinUI/Converters/MuteStateToLabelConverter.cs`
- `MicrophoneManager.WinUI/Converters/PercentToWidthConverter.cs`

**Example**:
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MicrophoneManager.WinUI.Converters;

public class MuteStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Segoe Fluent Icons (WinUI 3)
        return value is bool isMuted && isMuted
            ? "\uE74F" // MicOff
            : "\uE720"; // Microphone
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
```

**Register in App.xaml**:
```xml
<Application.Resources>
    <ResourceDictionary>
        <converters:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
        <converters:MuteStateToIconConverter x:Key="MuteStateToIcon"/>
        <converters:MuteStateToLabelConverter x:Key="MuteStateToLabel"/>
        <converters:PercentToWidthConverter x:Key="PercentToWidth"/>
    </ResourceDictionary>
</Application.Resources>
```

**Git Commit**: `feat: implement WinUI 3 value converters`

### B.4 Create MicrophoneFlyout View (Simplified for Vertical Slice)

**Files to Create**:
- `MicrophoneManager.WinUI/Views/MicrophoneFlyout.xaml`
- `MicrophoneManager.WinUI/Views/MicrophoneFlyout.xaml.cs`

**Content**: Display **only the default microphone** (not a list yet)

```xml
<UserControl x:Class="MicrophoneManager.WinUI.Views.MicrophoneFlyout"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:viewmodels="using:MicrophoneManager.Core.ViewModels"
             Background="#2D2D2D"
             MinWidth="370"
             Padding="12">

    <StackPanel Spacing="12">
        <TextBlock Text="Default Microphone" FontSize="14" FontWeight="SemiBold" Foreground="#999999"/>

        <!-- Single microphone card (DataContext = MicrophoneEntryViewModel) -->
        <Grid x:Name="MicCard" Background="#3D3D3D" CornerRadius="6" Padding="8">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/> <!-- Header -->
                <RowDefinition Height="Auto"/> <!-- Meter -->
                <RowDefinition Height="Auto"/> <!-- Volume -->
            </Grid.RowDefinitions>

            <!-- Header: Name + Action Buttons -->
            <Grid Grid.Row="0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <FontIcon Grid.Column="0" Glyph="&#xE720;" FontSize="18" Margin="0,0,8,0"/>

                <StackPanel Grid.Column="1">
                    <TextBlock Text="{x:Bind ViewModel.Name, Mode=OneWay}"
                              FontWeight="SemiBold" Foreground="White"/>
                    <TextBlock Text="{x:Bind ViewModel.FormatTag, Mode=OneWay}"
                              FontSize="11" Foreground="#AAAAAA"/>
                </StackPanel>

                <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="6">
                    <Button Command="{x:Bind ViewModel.SetDefaultCommand}"
                           ToolTipService.ToolTip="Set Default"
                           Width="32" Height="24" Padding="0">
                        <FontIcon Glyph="&#xE720;" FontSize="13"/>
                    </Button>

                    <Button Command="{x:Bind ViewModel.SetDefaultCommunicationCommand}"
                           ToolTipService.ToolTip="Set Communication"
                           Width="32" Height="24" Padding="0">
                        <FontIcon Glyph="&#xE8BD;" FontSize="13"/>
                    </Button>
                </StackPanel>
            </Grid>

            <!-- Meter: Input level with gradient and peak indicator -->
            <Grid Grid.Row="1" Margin="0,8,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <Grid Grid.Row="0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Input" FontSize="11" Foreground="#AAAAAA"/>
                    <TextBlock Grid.Column="1"
                              Text="{x:Bind ViewModel.InputLevelPercent, Mode=OneWay, Converter={StaticResource PercentFormatter}}"
                              FontSize="11" Foreground="#AAAAAA"/>
                </Grid>

                <!-- Meter bar (simplified, refine rendering later) -->
                <Border Grid.Row="1" Height="8" CornerRadius="4" Background="#444444" Margin="0,4,0,0">
                    <Border HorizontalAlignment="Left" CornerRadius="4">
                        <Border.Width>
                            <Binding Path="InputLevelPercent" Mode="OneWay">
                                <Binding.Converter>
                                    <converters:PercentToWidthConverter ActualWidth="{Binding ElementName=MeterTrack, Path=ActualWidth}"/>
                                </Binding.Converter>
                            </Binding>
                        </Border.Width>
                        <Border.Background>
                            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                <GradientStop Color="#3CCB5C" Offset="0.0"/>
                                <GradientStop Color="#3CCB5C" Offset="0.7"/>
                                <GradientStop Color="#E6C84A" Offset="0.85"/>
                                <GradientStop Color="#E45B5B" Offset="1.0"/>
                            </LinearGradientBrush>
                        </Border.Background>
                    </Border>
                </Border>
            </Grid>

            <!-- Volume: Mute button + Slider -->
            <Grid Grid.Row="2" Margin="0,8,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <Button Grid.Column="0" Command="{x:Bind ViewModel.ToggleMuteCommand}"
                       Width="32" Height="24" Padding="0" Margin="0,0,8,0">
                    <FontIcon Glyph="{x:Bind ViewModel.IsMuted, Mode=OneWay, Converter={StaticResource MuteStateToIcon}}"/>
                </Button>

                <Slider Grid.Column="1" Minimum="0" Maximum="100"
                       Value="{x:Bind ViewModel.VolumePercent, Mode=TwoWay}"/>
            </Grid>
        </Grid>
    </StackPanel>
</UserControl>
```

**Code-Behind**:
```csharp
using Microsoft.UI.Xaml.Controls;
using MicrophoneManager.Core.ViewModels;

namespace MicrophoneManager.WinUI.Views;

public sealed partial class MicrophoneFlyout : UserControl
{
    public MicrophoneEntryViewModel? ViewModel { get; private set; }

    public MicrophoneFlyout()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Get default microphone from service
        var audioService = App.AudioService;
        if (audioService != null)
        {
            var defaultMic = audioService.GetDefaultMicrophone();
            if (defaultMic != null)
            {
                ViewModel = new MicrophoneEntryViewModel(defaultMic, audioService);
                MicCard.DataContext = ViewModel;
            }
        }
    }
}
```

**Simplification Notes**:
- Only default microphone (not full list)
- Simplified meter rendering (refine visuals later)
- No scrolling (single card)
- No dock/undock button yet

**Git Commit**: `feat: implement vertical slice - single microphone card`

### B.5 Wire Up FlyoutWindow

**Modify**:
- `MicrophoneManager.WinUI/Views/FlyoutWindow.xaml`

**Content**:
```xml
<Window x:Class="MicrophoneManager.WinUI.Views.FlyoutWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="using:MicrophoneManager.WinUI.Views">

    <Window.SystemBackdrop>
        <MicaBackdrop Kind="Base"/>
    </Window.SystemBackdrop>

    <Grid Background="{ThemeResource LayerFillColorDefaultBrush}"
          CornerRadius="8"
          Padding="4"
          BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
          BorderThickness="1">
        <local:MicrophoneFlyout/>
    </Grid>
</Window>
```

**Code-Behind**:
```csharp
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace MicrophoneManager.WinUI.Views;

public sealed partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Configure borderless, always-on-top window
        var appWindow = AppWindow;
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        appWindow.SetPresenter(presenter);

        // Size and position
        appWindow.ResizeClient(new Windows.Graphics.SizeInt32(390, 300));
        CenterOnScreen();
    }

    private void CenterOnScreen()
    {
        var displayArea = DisplayArea.Primary;
        var workArea = displayArea.WorkArea;
        var appWindow = AppWindow;

        var x = (workArea.Width - 390) / 2;
        var y = (workArea.Height - 300) / 2;
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }
}
```

**Git Commit**: `feat: wire up FlyoutWindow with Mica backdrop`

### B.6 Update TrayViewModel and MainWindow

**Modify**:
- `MicrophoneManager.WinUI/App.xaml.cs` (register services in DI)
- `MicrophoneManager.WinUI/MainWindow.xaml.cs` (inject TrayViewModel, AudioService)

**App.xaml.cs ConfigureServices**:
```csharp
private void ConfigureServices(IServiceCollection services)
{
    // Services
    services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
    services.AddSingleton<IIconGeneratorService, IconGeneratorService>();

    // ViewModels
    services.AddSingleton<TrayViewModel>();

    // Views
    services.AddSingleton<MainWindow>();
}
```

**MainWindow.xaml.cs**:
```csharp
public sealed partial class MainWindow : Window
{
    public TrayViewModel ViewModel { get; }
    private readonly IIconGeneratorService _iconGenerator;

    public MainWindow(TrayViewModel viewModel, IIconGeneratorService iconGenerator)
    {
        InitializeComponent();

        ViewModel = viewModel;
        _iconGenerator = iconGenerator;

        // Set DataContext for bindings
        TrayIcon.DataContext = ViewModel;

        // Update icon on mute state change
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsMuted))
                UpdateTrayIcon(ViewModel.IsMuted);
        };

        UpdateTrayIcon(ViewModel.IsMuted);
    }

    private void UpdateTrayIcon(bool isMuted)
    {
        try
        {
            var icon = _iconGenerator.CreateMicrophoneIcon(isMuted);
            TrayIcon.Icon = icon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Icon update error: {ex.Message}");
        }
    }
}
```

**Git Commit**: `feat: integrate TrayViewModel with MainWindow`

### B.7 Testing & Validation (Stage B)

**Test Cases**:
1. **Audio Service Integration**
   - [ ] Run app, verify default microphone displays
   - [ ] Check device name and format tag are correct

2. **Real-Time Meter**
   - [ ] Speak into microphone, verify meter animates
   - [ ] Check 16ms timer fires (no lag/stutter)
   - [ ] Verify peak hold indicator appears and decays after 5s

3. **Volume Control**
   - [ ] Move slider, verify Windows volume changes (check Sound settings)
   - [ ] Change volume in Windows, verify slider updates in flyout

4. **Mute Control**
   - [ ] Click mute button, verify device mutes (check Sound settings)
   - [ ] Mute in Windows, verify button state updates in flyout
   - [ ] Verify tray icon changes (muted vs unmuted icon)

5. **Role Switching**
   - [ ] Click "Set Default", verify device becomes default in Sound settings
   - [ ] Click "Set Communication", verify device becomes communication default

6. **Performance**
   - [ ] Profile CPU usage (should be <1% idle, <5% with meter active)
   - [ ] Check memory usage (no leaks over 5 minutes)

**Debugging Tips**:
- Use DispatcherQueue timer debugger to verify 16ms interval
- Add Debug.WriteLine in AudioDeviceService events
- Test with USB mic hotplug (plug/unplug while app running)

### B.8 Stage B Deliverables

**Checklist**:
- [ ] Core services moved and working (NAudio integration)
- [ ] ViewModels adapted to DispatcherQueue
- [ ] Converters implemented (WinUI 3 IValueConverter)
- [ ] Single microphone card displays correctly
- [ ] Real-time input level meter animates smoothly
- [ ] Volume slider controls device volume (bidirectional)
- [ ] Mute button toggles mute state (bidirectional)
- [ ] Default/communication role buttons work
- [ ] Tray icon updates on mute change
- [ ] No performance regressions vs WPF

**Git Commit**: `feat: Stage B complete - vertical slice with working audio controls`

---

## Stage C: Full Parity Migration

**Goal**: Expand vertical slice to full feature parity with WPF version

**Success Criteria**:
- ✅ All microphones display in scrollable list
- ✅ Per-device meters, volume, mute controls
- ✅ Device hotplug handled gracefully
- ✅ Dock/undock button works
- ✅ Multi-monitor window positioning
- ✅ Startup integration toggle
- ✅ All visual polish (hover states, animations, colors)
- ✅ Keyboard shortcuts (Escape to close)
- ✅ Edge cases handled (no devices, device errors)

### C.1 Expand to Full Microphone List

**Modify**:
- `MicrophoneManager.WinUI/Views/MicrophoneFlyout.xaml`

**Changes**:
1. Replace single card with `ListView` bound to `MicrophoneListViewModel.Microphones`
2. Use `DataTemplate` for microphone cards (same design as vertical slice)
3. Add `ScrollViewer` with max height
4. Add "No microphones detected" message when list empty

**Example**:
```xml
<ListView ItemsSource="{x:Bind ViewModel.Microphones, Mode=OneWay}"
          SelectionMode="None"
          Background="Transparent">
    <ListView.ItemTemplate>
        <DataTemplate x:DataType="viewmodels:MicrophoneEntryViewModel">
            <!-- Microphone card from Stage B -->
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

**Code-Behind**:
```csharp
public MicrophoneListViewModel ViewModel { get; }

public MicrophoneFlyout()
{
    InitializeComponent();

    var audioService = App.AudioService;
    ViewModel = new MicrophoneListViewModel(audioService);
}
```

**Validation**:
- Connect multiple USB microphones
- Verify all devices display
- Verify scrolling works with many devices

**Git Commit**: `feat: expand to full microphone list with scrolling`

### C.2 Advanced Meter Rendering

**Tasks**:
1. Implement full gradient meter with mask overlay
2. Add peak indicator (white line at peak position)
3. Add scale ticks (0, 20, 40, 60, 80, 100)
4. Optimize rendering (use Composition APIs if needed)

**Reference**: WPF version `MicrophoneFlyout.xaml` lines 281-391

**Challenges**:
- WinUI 3 doesn't have `MultiBinding` built-in (use CommunityToolkit.WinUI or x:Bind functions)
- TranslateTransform for meter overlay positioning

**Git Commit**: `feat: implement advanced meter rendering with peak indicators`

### C.3 Dock/Undock Functionality

**Files to Modify**:
- `MicrophoneManager.WinUI/Views/MicrophoneFlyout.xaml` (add dock button)
- `MicrophoneManager.WinUI/Views/MicrophoneFlyout.xaml.cs` (implement toggle logic)
- `MicrophoneManager.WinUI/Views/FlyoutWindow.xaml.cs` (window positioning)

**Features**:
- Dock button in flyout header (Segoe Fluent Icons: `\uE718`)
- Toggle between popup (from tray) and standalone window
- Remember last docked position
- Implement drag-to-move for docked window (PointerPressed/Moved handlers)

**Git Commit**: `feat: implement dock/undock window mode`

### C.4 Device Hotplug Handling

**Already Implemented**: `AudioDeviceService` raises `DevicesChanged` event

**Validation**:
1. Run app with flyout open
2. Plug in USB microphone
3. Verify device appears in list immediately
4. Unplug microphone
5. Verify device disappears from list

**Edge Case Testing**:
- Unplug default microphone (verify graceful fallback)
- Plug in 10+ devices (verify performance)

**Git Commit**: `test: validate device hotplug handling`

### C.5 Multi-Monitor Support

**Tasks**:
1. Position flyout near tray icon (if possible to get tray icon screen position)
2. Fallback: Center on primary display
3. Ensure docked window respects work area (no overlap with taskbar)

**Code**:
```csharp
private void PositionNearTray()
{
    // Get cursor position (approximate tray icon location)
    var cursorPos = Microsoft.UI.Input.PointerPoint.GetCurrentPoint(0).Position;

    // Get display area for that position
    // ... (use DisplayArea APIs)

    // Position flyout near cursor, within work area bounds
}
```

**Git Commit**: `feat: multi-monitor window positioning`

### C.6 Startup Integration

**Already Implemented**: `StartupService.cs` (registry-based)

**UI Integration**:
- Context menu item bound to `TrayViewModel.StartupMenuText`
- `ToggleStartupCommand` already implemented

**Validation**:
- Enable startup, restart Windows, verify app launches
- Disable startup, restart Windows, verify app doesn't launch

**Git Commit**: `test: validate Windows startup integration`

### C.7 Visual Polish

**Tasks**:
1. Refine custom control templates (buttons, slider)
2. Implement all VisualStates (Normal, PointerOver, Pressed, Disabled)
3. Add animations (e.g., meter smoothing, button press feedback)
4. Test light/dark theme switching
5. Apply Mica/Acrylic backdrop consistently

**Custom Slider Style**:
- Port WPF `CompactSlider` style to WinUI 3
- Use VisualStates for thumb hover/pressed states

**Git Commit**: `style: refine visual polish and animations`

### C.8 Keyboard Shortcuts

**Tasks**:
- Escape key closes flyout (already in FlyoutWindow.xaml.cs)
- Tab navigation through controls
- Accessibility: ensure all controls have automation names

**Validation**:
- Test keyboard-only navigation
- Run Accessibility Insights to check WCAG compliance

**Git Commit**: `a11y: keyboard shortcuts and accessibility improvements`

### C.9 Edge Case Handling

**Scenarios to Test**:
1. **No Microphones**:
   - Disable all audio inputs
   - Verify "No microphones detected" message
   - Verify no crashes

2. **Audio Service Errors**:
   - Simulate NAudio exceptions
   - Verify graceful error handling (MessageBox or silent fallback)

3. **Rapid Device Changes**:
   - Plug/unplug multiple devices quickly
   - Verify no race conditions or crashes

4. **Low Resources**:
   - Test on low-end hardware (meter should still be smooth)

5. **Concurrent Volume Changes**:
   - Change volume in app and Windows Sound settings simultaneously
   - Verify no feedback loops or flickering

**Git Commit**: `fix: edge case handling and error resilience`

### C.10 Stage C Deliverables

**Checklist**:
- [ ] All microphones display in list
- [ ] Scrolling works with many devices
- [ ] Advanced meter rendering (gradient, peak, scale)
- [ ] Dock/undock button works
- [ ] Drag-to-move for docked window
- [ ] Device hotplug handled gracefully
- [ ] Multi-monitor positioning correct
- [ ] Startup integration toggle works
- [ ] All custom styles implemented
- [ ] Keyboard shortcuts work
- [ ] Accessibility validated
- [ ] Edge cases tested (no devices, errors, etc.)
- [ ] Performance profiled (CPU, memory)
- [ ] Visual parity with WPF version

**Git Commit**: `feat: Stage C complete - full feature parity achieved`

---

## Post-Migration Tasks

### Documentation Updates

**Files to Update**:
1. **README.md**
   - Add "WinUI 3 Build and Run" section
   - Update screenshots (if any)
   - Document prerequisites (Windows SDK, .NET 8)

2. **AGENT.md**
   - Update architecture section to reflect WinUI 3
   - Document DispatcherQueue usage
   - Note WPF version deprecation timeline

3. **CLAUDE.md**
   - Update "Active Technologies" to WinUI 3
   - Update build commands

**Git Commit**: `docs: update README and AGENT.md for WinUI 3`

### CI/CD Pipeline

**Tasks** (if CI exists):
1. Add WinUI 3 build job
2. Configure Windows App SDK installation
3. Add publish step for single-file EXE
4. Add artifact upload
5. Test on clean Windows VM

**Example GitHub Actions**:
```yaml
jobs:
  build-winui:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build MicrophoneManager.WinUI
      - run: dotnet publish MicrophoneManager.WinUI -c Release
      - uses: actions/upload-artifact@v4
        with:
          name: MicrophoneManager-WinUI
          path: MicrophoneManager.WinUI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/
```

**Git Commit**: `ci: add WinUI 3 build pipeline`

### Deprecation Plan for WPF Version

**Options**:
1. **Keep Both** (during transition period):
   - Maintain WPF version for 1-2 releases
   - Mark WPF as deprecated in README
   - Direct new users to WinUI 3 version

2. **Archive WPF**:
   - Move WPF project to `Archive/` folder
   - Update solution to only include WinUI project
   - Keep for reference only

3. **Delete WPF** (after full validation):
   - Remove WPF project entirely
   - Clean up solution file

**Recommendation**: Keep WPF for 1 release cycle as safety net, then archive.

---

## Risk Mitigation & Rollback Plan

### Known Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| System tray library unavailable | Medium | High | Win32 wrapper ready as fallback |
| Meter performance issues | Low | Medium | Profile early in Stage B, optimize rendering |
| NAudio WinUI incompatibility | Low | High | Validate in Stage B immediately |
| DispatcherQueue timer imprecision | Low | Medium | Measure actual interval, adjust if needed |
| Visual regression | Medium | Low | Side-by-side comparison screenshots |

### Rollback Strategy

**If critical blocker found**:
1. Keep WPF version as production build
2. Continue WinUI 3 development in parallel
3. Document blocker in GitHub issue
4. Seek community/Microsoft support

**When to Rollback**:
- System tray icon fundamentally broken
- Audio APIs don't work in WinUI 3 process model
- Performance >50% worse than WPF
- Unfixable visual bugs in meters

---

## Success Metrics

### Functional Metrics
- ✅ 100% feature parity with WPF version
- ✅ Zero crashes in 1-hour stress test (device hotplug, volume changes)
- ✅ All unit tests passing

### Performance Metrics
- ✅ CPU usage <1% idle (WPF baseline: ~0.5%)
- ✅ CPU usage <5% with meters active (WPF baseline: ~2-3%)
- ✅ Memory usage <50MB (WPF baseline: ~40MB)
- ✅ Meter update latency <20ms (target: 16ms)

### Quality Metrics
- ✅ Accessibility score >80 (Accessibility Insights)
- ✅ No high-severity code analysis warnings
- ✅ Single-file EXE size <15MB (compressed)

### User Experience Metrics
- ✅ Visual appearance matches WPF (subjective review)
- ✅ Flyout opens in <200ms from tray click
- ✅ Volume slider feels responsive (no lag)

---

## Appendix: View-by-View Migration Checklist

### MainWindow
- [x] Hidden window setup (off-screen positioning at -32000,-32000 with 1x1 size - NOT Hide() which causes app exit)
- [x] System tray icon host (H.NotifyIcon.WinUI v2.1.3)
- [x] Context menu (Show, Exit commands working)
- [x] AppWindow.IsShownInSwitchers = false (hides from Alt+Tab)
- [ ] Icon update on mute change (Stage B)
- [ ] Startup toggle menu item (Stage B)
- [ ] Test with Win11 taskbar overflow

### FlyoutWindow
- [x] Borderless window with custom chrome
- [ ] Mica/Acrylic backdrop
- [ ] Rounded corners (8px)
- [ ] Drop shadow (ThemeShadow)
- [ ] Drag-to-move (Alt + drag)
- [ ] Escape key closes
- [ ] Max height based on display work area
- [ ] Position near tray icon

### MicrophoneFlyout (UserControl)
- [x] Header with "Microphones" label
- [ ] Dock/undock button
- [ ] ListView with microphone cards
- [ ] ScrollViewer (max 360px height)
- [ ] "No microphones detected" message
- [ ] Empty state handling

### Microphone Card (DataTemplate)
- [x] Device icon (FontIcon)
- [x] Device name (TextBlock)
- [x] Format tag (TextBlock, 11pt, gray)
- [x] Set Default button (icon, hover state)
- [x] Set Communication button (icon, hover state)
- [ ] Input level meter (gradient, peak, scale)
- [x] Mute button (icon, toggle state)
- [x] Volume slider (custom style)
- [ ] Hover effects (card highlight)
- [ ] Selected state (accent color)

### Styles & Resources
- [ ] ModernButton style (rounded, hover states)
- [ ] IconActionButton style (32x24, icon-only)
- [ ] CompactSlider style (custom track/thumb)
- [ ] MicListItem style (card with hover/selection)
- [ ] Color palette (dark theme)
- [ ] Meter gradient brush
- [ ] Converters (WinUI 3 interface)

---

**Document Version**: 1.0
**Last Updated**: 2026-01-04
**Reviewed By**: Migration Agent
