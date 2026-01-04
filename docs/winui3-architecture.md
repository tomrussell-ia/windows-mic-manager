# WinUI 3 Target Architecture

**Date**: 2026-01-04
**Status**: Design Complete
**Target Framework**: .NET 8 with Windows App SDK (WinUI 3)

## Executive Summary

This document defines the target architecture for the WinUI 3 migration of the Windows Microphone Manager application. The design preserves the MVVM pattern, reuses business logic layers, and adapts UI components to WinUI 3's modern app model while maintaining feature parity with the WPF version.

## 1. Solution Structure

### 1.1 Proposed Project Layout

```
MicrophoneManager.sln
├── MicrophoneManager.Core/              (New - Shared .NET 8 class library)
│   ├── Models/
│   ├── Services/
│   ├── ViewModels/
│   └── Abstractions/
│
├── MicrophoneManager.WinUI/             (New - WinUI 3 application)
│   ├── App.xaml/cs
│   ├── MainWindow.xaml/cs
│   ├── Views/
│   ├── Converters/
│   ├── Assets/
│   └── Package.appxmanifest (optional, for packaged)
│
├── MicrophoneManager/                   (Existing - WPF, keep for reference)
│   └── [Keep temporarily for side-by-side comparison]
│
└── MicrophoneManager.Tests/             (Update tests for Core)
    └── [Extend to test WinUI-specific code]
```

### 1.2 Project Dependencies

```
MicrophoneManager.WinUI
  └─> MicrophoneManager.Core
       └─> NAudio
       └─> CommunityToolkit.Mvvm

MicrophoneManager.Tests
  └─> MicrophoneManager.Core
```

### 1.3 Alternative: In-Place Migration

For simplicity, could convert `MicrophoneManager` project directly to WinUI 3:
- Change SDK from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk` with WinUI support
- Replace `<UseWPF>true</UseWPF>` with Windows App SDK references
- Keep existing namespace structure

**Decision**: Use **separate projects** approach for cleaner separation and ability to maintain WPF version during transition.

## 2. Project Configuration

### 2.1 MicrophoneManager.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>x64;AnyCPU</Platforms>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NAudio" Version="2.2.1" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  </ItemGroup>
</Project>
```

### 2.2 MicrophoneManager.WinUI.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <Platforms>x64;AnyCPU</Platforms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>false</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <!-- Publish settings for portable EXE -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.0" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.0" />
    <PackageReference Include="CommunityToolkit.WinUI.Controls.DataGrid" Version="8.0.0" />
    <!-- System tray - research which package works best -->
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.1.0" />
    <!-- Alternative if above doesn't exist -->
    <!-- <PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" /> -->
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MicrophoneManager.Core\MicrophoneManager.Core.csproj" />
  </ItemGroup>
</Project>
```

**Key Settings**:
- `EnableMsixTooling>false` — Unpackaged deployment
- `WindowsAppSDKSelfContained>true` — Bundles Windows App SDK runtime
- `PublishSingleFile>true` — Maintains single-EXE deployment model

## 3. Application Lifecycle & Initialization

### 3.1 App.xaml (WinUI 3)

```xml
<Application
    x:Class="MicrophoneManager.WinUI.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:MicrophoneManager.WinUI">

    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
                <ResourceDictionary Source="/Styles/Colors.xaml"/>
                <ResourceDictionary Source="/Styles/Converters.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### 3.2 App.xaml.cs (WinUI 3)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MicrophoneManager.Core.Services;
using MicrophoneManager.Core.ViewModels;

namespace MicrophoneManager.WinUI;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static DispatcherQueue DispatcherQueue { get; private set; } = null!;

    // Legacy static access (refactor incrementally)
    public static TrayViewModel? TrayViewModel { get; set; }
    public static IAudioDeviceService? AudioService { get; set; }
    public static Window? DockedWindow { get; set; }

    public App()
    {
        InitializeComponent();

        // Windows App SDK initialization
        Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += OnActivated;

        // Build DI container
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IIconGeneratorService, IconGeneratorService>();
        services.AddSingleton<IStartupService, StartupService>();

        // ViewModels
        services.AddSingleton<TrayViewModel>();
        services.AddTransient<MicrophoneListViewModel>();
        services.AddTransient<MicrophoneEntryViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.FlyoutWindow>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Get dispatcher for UI thread access
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Initialize services
        AudioService = Host.Services.GetRequiredService<IAudioDeviceService>();
        TrayViewModel = Host.Services.GetRequiredService<TrayViewModel>();

        // Create main window (hidden, hosts tray icon)
        m_window = Host.Services.GetRequiredService<MainWindow>();
        m_window.Activate();
    }

    private void OnActivated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments args)
    {
        // Handle activation (e.g., from startup)
    }

    private Window? m_window;
}
```

**Key Changes from WPF**:
- Uses `Microsoft.Extensions.Hosting.Host` for DI (modern pattern)
- `DispatcherQueue` instead of `Dispatcher`
- `OnLaunched` instead of `Startup` event
- Windows App SDK bootstrap for unpackaged apps

### 3.3 Dependency Injection Strategy

**Service Lifetimes**:
- **Singleton**: Services, TrayViewModel (lives entire app lifetime)
- **Transient**: MicrophoneListViewModel, Views (created per-flyout-open)

**ViewModel Construction**:
- Use constructor injection for services
- Factory pattern for MicrophoneEntryViewModel (created dynamically per device)

## 4. Threading & Dispatcher Abstraction

### 4.1 Problem

ViewModels currently reference:
- `System.Windows.Threading.Dispatcher`
- `System.Windows.Threading.DispatcherTimer`
- `System.Windows.Application.Current`

### 4.2 Solution: Direct Replacement (Recommended)

Replace WPF APIs with WinUI equivalents directly in Core project:

**Before (WPF)**:
```csharp
_dispatcher = Application.Current.Dispatcher;
_dispatcher.BeginInvoke(UpdateState);

var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
```

**After (WinUI 3)**:
```csharp
_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
_dispatcherQueue.TryEnqueue(UpdateState);

var timer = DispatcherQueue.CreateTimer();
timer.Interval = TimeSpan.FromMilliseconds(16);
```

**Impact**: Core project requires `net8.0-windows10.0.19041.0` target (acceptable for Windows-only app)

### 4.3 Alternative: Abstraction Layer

Create interface if targeting multiple UI frameworks:

```csharp
// MicrophoneManager.Core/Abstractions/IDispatcherService.cs
public interface IDispatcherService
{
    void Invoke(Action action);
    void InvokeAsync(Action action);
    IDisposable CreateTimer(TimeSpan interval, Action callback);
}

// MicrophoneManager.WinUI/Services/WinUIDispatcherService.cs
public class WinUIDispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _queue;

    public WinUIDispatcherService(DispatcherQueue queue)
    {
        _queue = queue;
    }

    public void InvokeAsync(Action action) => _queue.TryEnqueue(() => action());
    // ... rest of implementation
}
```

**Decision**: Use **direct replacement** for simplicity (only targeting WinUI 3).

### 4.4 Updated ViewModels

**TrayViewModel.cs Changes**:
```csharp
// Replace
using System.Windows.Threading;
private readonly Dispatcher _dispatcher;

// With
using Microsoft.UI.Dispatching;
private readonly DispatcherQueue _dispatcherQueue;

// In constructor
_dispatcherQueue = DispatcherQueue.GetForCurrentThread();

// In event handlers
_dispatcherQueue.TryEnqueue(UpdateState);
```

**MicrophoneListViewModel.cs Changes**:
```csharp
// Replace
using System.Windows.Threading;
private readonly DispatcherTimer _peakHoldTimer;

// With
using Microsoft.UI.Dispatching;
private readonly DispatcherQueueTimer _peakHoldTimer;

// In constructor
_peakHoldTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
_peakHoldTimer.Interval = TimeSpan.FromMilliseconds(16);
_peakHoldTimer.Tick += (s, e) => TickPeakHold();
_peakHoldTimer.Start();

// Helper method
private static void InvokeOnUiThread(Action action)
{
    var queue = DispatcherQueue.GetForCurrentThread();
    if (queue != null)
    {
        queue.TryEnqueue(() => action());
    }
    else
    {
        action(); // Unit tests fallback
    }
}
```

## 5. System Tray Integration

### 5.1 Options Analysis

| Option | Pros | Cons | Maturity |
|--------|------|------|----------|
| **H.NotifyIcon.WinUI** | Drop-in WinUI port of WPF lib | May not exist/maintained | Unknown |
| **Microsoft.Toolkit.Uwp.Notifications** | Official toolkit | Toast only, needs custom tray | Mature |
| **Win32 P/Invoke** | Full control | Manual implementation | DIY |

### 5.2 Recommended Approach: H.NotifyIcon.WinUI

**If package exists** (verify on NuGet):

```xml
<tb:TaskbarIcon x:Name="TrayIcon"
                ToolTipText="{x:Bind ViewModel.TooltipText, Mode=OneWay}">
    <tb:TaskbarIcon.TrayPopup>
        <local:MicrophoneFlyout/>
    </tb:TaskbarIcon.TrayPopup>

    <tb:TaskbarIcon.ContextMenu>
        <MenuFlyout>
            <MenuFlyoutItem Text="{x:Bind ViewModel.StartupMenuText, Mode=OneWay}"
                           Command="{x:Bind ViewModel.ToggleStartupCommand}"/>
            <MenuFlyoutSeparator/>
            <MenuFlyoutItem Text="Exit" Command="{x:Bind ViewModel.ExitCommand}"/>
        </MenuFlyout>
    </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

### 5.3 Fallback: Win32 Interop Wrapper

If H.NotifyIcon.WinUI unavailable, create wrapper:

```csharp
// MicrophoneManager.Core/Services/ITrayIconService.cs
public interface ITrayIconService
{
    void SetIcon(Icon icon);
    void SetTooltip(string tooltip);
    void ShowContextMenu(IEnumerable<MenuItem> items);
    event EventHandler LeftClick;
    event EventHandler RightClick;
}

// MicrophoneManager.WinUI/Services/Win32TrayIconService.cs
// Implement using Shell_NotifyIcon Win32 APIs
```

## 6. Main Window (Hidden Tray Host)

### 6.1 MainWindow.xaml (WinUI 3)

```xml
<Window
    x:Class="MicrophoneManager.WinUI.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:tb="using:H.NotifyIcon"
    xmlns:local="using:MicrophoneManager.WinUI"
    xmlns:views="using:MicrophoneManager.WinUI.Views">

    <Grid>
        <tb:TaskbarIcon x:Name="TrayIcon"
                        ToolTipText="{x:Bind ViewModel.TooltipText, Mode=OneWay}">

            <tb:TaskbarIcon.TrayPopup>
                <views:MicrophoneFlyout/>
            </tb:TaskbarIcon.TrayPopup>

            <tb:TaskbarIcon.ContextMenu>
                <MenuFlyout>
                    <MenuFlyoutItem Text="{x:Bind ViewModel.StartupMenuText, Mode=OneWay}"
                                   Command="{x:Bind ViewModel.ToggleStartupCommand}"/>
                    <MenuFlyoutSeparator/>
                    <MenuFlyoutItem Text="Exit" Command="{x:Bind ViewModel.ExitCommand}"/>
                </MenuFlyout>
            </tb:TaskbarIcon.ContextMenu>
        </tb:TaskbarIcon>
    </Grid>
</Window>
```

### 6.2 MainWindow.xaml.cs (WinUI 3)

```csharp
using Microsoft.UI.Xaml;
using MicrophoneManager.Core.Services;
using MicrophoneManager.Core.ViewModels;

namespace MicrophoneManager.WinUI;

public sealed partial class MainWindow : Window
{
    public TrayViewModel ViewModel { get; }

    private readonly IAudioDeviceService _audioService;
    private readonly IIconGeneratorService _iconGenerator;

    public MainWindow(
        TrayViewModel viewModel,
        IAudioDeviceService audioService,
        IIconGeneratorService iconGenerator)
    {
        InitializeComponent();

        ViewModel = viewModel;
        _audioService = audioService;
        _iconGenerator = iconGenerator;

        // Hide window (WinUI 3 approach)
        var appWindow = AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(0, 0));
        appWindow.IsShownInSwitchers = false;

        // Set initial icon
        UpdateTrayIcon(ViewModel.IsMuted);

        // Subscribe to mute state changes
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsMuted))
                UpdateTrayIcon(ViewModel.IsMuted);
        };

        Closed += MainWindow_Closed;
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
            System.Diagnostics.Debug.WriteLine($"Icon error: {ex.Message}");
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        TrayIcon?.Dispose();
        _audioService?.Dispose();
    }
}
```

**Key Changes**:
- `AppWindow` APIs for window manipulation (no `Width="0"`)
- `x:Bind` instead of `{Binding}` (compiled bindings, better performance)
- Constructor injection of ViewModels and Services
- `Closed` event instead of separate handler

## 7. Views & UI Components

### 7.1 FlyoutWindow.xaml (WinUI 3)

**Design Approach**: Borderless window with custom chrome and Mica backdrop

```xml
<Window
    x:Class="MicrophoneManager.WinUI.Views.FlyoutWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:MicrophoneManager.WinUI.Views">

    <Window.SystemBackdrop>
        <MicaBackdrop Kind="Base"/>
    </Window.SystemBackdrop>

    <Grid Background="{ThemeResource LayerFillColorDefaultBrush}"
          CornerRadius="8"
          BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
          BorderThickness="1">

        <Grid.Shadow>
            <ThemeShadow />
        </Grid.Shadow>

        <local:MicrophoneFlyout x:Name="Flyout" IsDockedMode="True"/>
    </Grid>
</Window>
```

**Code-Behind Changes**:
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;

public sealed partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Configure AppWindow
        var appWindow = AppWindow;
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        // Set presenter for borderless window
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        appWindow.SetPresenter(presenter);

        // Max height based on display area
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        appWindow.ResizeClient(new Windows.Graphics.SizeInt32(390,
            Math.Max(200, workArea.Height - 24)));
    }

    // Drag-to-move: Implement using PointerPressed/Moved handlers
    // (WinUI 3 doesn't have DragMove(), need custom implementation)
}
```

**Migration Notes**:
- Replace `DropShadowEffect` with `ThemeShadow` (WinUI 3 built-in)
- Replace `SystemParameters.WorkArea` with `DisplayArea` APIs
- Implement drag-to-move manually (no `DragMove()`)
- Use `MicaBackdrop` or `DesktopAcrylicBackdrop` for modern Windows 11 look

### 7.2 MicrophoneFlyout.xaml (WinUI 3)

**Key Migrations**:

1. **ListBox → ListView** (or keep ListBox, both exist in WinUI 3)
2. **Triggers → VisualStates**
3. **Custom Styles → WinUI 3 patterns**
4. **Converters → WinUI 3 interface**

**Example: Modern Button Style with VisualStates**

```xml
<Style x:Key="ModernButton" TargetType="Button">
    <Setter Property="Background" Value="#3D3D3D"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Padding" Value="14,8"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Grid x:Name="RootGrid" Background="{TemplateBinding Background}"
                      CornerRadius="{TemplateBinding CornerRadius}">

                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                            <VisualState x:Name="Normal"/>

                            <VisualState x:Name="PointerOver">
                                <VisualState.Setters>
                                    <Setter Target="RootGrid.Background" Value="#4D4D4D"/>
                                </VisualState.Setters>
                            </VisualState>

                            <VisualState x:Name="Pressed">
                                <VisualState.Setters>
                                    <Setter Target="RootGrid.Background" Value="#5D5D5D"/>
                                </VisualState.Setters>
                            </VisualState>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>

                    <ContentPresenter HorizontalAlignment="Center"
                                     VerticalAlignment="Center"
                                     Padding="{TemplateBinding Padding}"/>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Example: Microphone Card DataTemplate**

```xml
<DataTemplate x:DataType="viewmodels:MicrophoneEntryViewModel">
    <Grid Background="#3D3D3D" CornerRadius="6" Padding="6" Margin="3,2,3,4">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header Row -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Icon -->
            <FontIcon Grid.Column="0" Glyph="&#xE720;" FontSize="18" Margin="0,0,6,0"/>

            <!-- Name and Format -->
            <StackPanel Grid.Column="1">
                <TextBlock Text="{x:Bind Name, Mode=OneWay}" FontWeight="SemiBold"/>
                <TextBlock Text="{x:Bind FormatTag, Mode=OneWay}"
                          Foreground="#AAAAAA" FontSize="11"/>
            </StackPanel>

            <!-- Action Buttons -->
            <StackPanel Grid.Column="2" Orientation="Horizontal">
                <Button Command="{x:Bind SetDefaultCommand}"
                       ToolTipService.ToolTip="Set Default"
                       Style="{StaticResource IconActionButton}">
                    <FontIcon Glyph="&#xE720;"/>

                    <!-- Visual state for IsDefault -->
                    <Button.Resources>
                        <StaticResource x:Key="ButtonBackground"
                                       ResourceKey="{x:Bind IsDefault, Mode=OneWay,
                                       Converter={StaticResource BoolToBackgroundConverter}}"/>
                    </Button.Resources>
                </Button>

                <Button Command="{x:Bind SetDefaultCommunicationCommand}"
                       ToolTipService.ToolTip="Set Communications"
                       Style="{StaticResource IconActionButton}">
                    <FontIcon Glyph="&#xE8BD;"/>
                </Button>
            </StackPanel>
        </Grid>

        <!-- Meter Row -->
        <Grid Grid.Row="1" Margin="0,4,0,0">
            <!-- Input level meter (gradient, peak indicator) -->
            <!-- Implementation similar to WPF but with WinUI semantics -->
        </Grid>

        <!-- Volume Control Row -->
        <Grid Grid.Row="3" Margin="0,4,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <Button Grid.Column="0" Command="{x:Bind ToggleMuteCommand}"
                   Style="{StaticResource IconActionButton}">
                <FontIcon Glyph="{x:Bind IsMuted, Mode=OneWay,
                                 Converter={StaticResource MuteStateToIconConverter}}"/>
            </Button>

            <Slider Grid.Column="1" Minimum="0" Maximum="100"
                   Value="{x:Bind VolumePercent, Mode=TwoWay}"
                   Style="{StaticResource CompactSlider}"/>
        </Grid>
    </Grid>
</DataTemplate>
```

**Key WinUI 3 XAML Differences**:
- `x:Bind` instead of `{Binding}` (compiled, faster, requires `x:DataType`)
- `Mode=OneWay` explicit (default is OneTime in x:Bind)
- `FontIcon` instead of TextBlock with FontFamily
- `ToolTipService.ToolTip` instead of `ToolTip` property directly
- `MenuFlyout` instead of `ContextMenu`

## 8. Converters

### 8.1 WinUI 3 IValueConverter Interface

```csharp
using Microsoft.UI.Xaml.Data;
using System.Globalization;

namespace MicrophoneManager.WinUI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class MuteStateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool isMuted && isMuted ? "\uE74F" : "\uE720"; // Segoe Fluent Icons
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PercentToWidthConverter : IValueConverter
{
    // MultiBinding support requires CommunityToolkit.WinUI
    // Or implement as custom markup extension
}
```

**Register in App.xaml**:
```xml
<Application.Resources>
    <ResourceDictionary>
        <converters:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
        <converters:MuteStateToIconConverter x:Key="MuteStateToIcon"/>
    </ResourceDictionary>
</Application.Resources>
```

### 8.2 Alternative: x:Bind Functions

WinUI 3 allows calling functions in x:Bind:

```xml
<!-- In code-behind or ViewModel -->
public string GetMuteIcon(bool isMuted) => isMuted ? "\uE74F" : "\uE720";

<!-- In XAML -->
<FontIcon Glyph="{x:Bind GetMuteIcon(ViewModel.IsMuted), Mode=OneWay}"/>
```

## 9. Theming & Resources

### 9.1 Colors.xaml Resource Dictionary

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- App-specific colors (dark theme) -->
    <Color x:Key="AccentColor">#0078D4</Color>
    <Color x:Key="BackgroundColor">#2D2D2D</Color>
    <Color x:Key="ForegroundColor">#FFFFFF</Color>
    <Color x:Key="HoverColor">#3D3D3D</Color>

    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}"/>
    <SolidColorBrush x:Key="HoverBrush" Color="{StaticResource HoverColor}"/>

    <!-- Meter gradient (green → yellow → red) -->
    <LinearGradientBrush x:Key="MeterGradientBrush" StartPoint="0,0" EndPoint="1,0">
        <GradientStop Color="#3CCB5C" Offset="0.00"/>
        <GradientStop Color="#3CCB5C" Offset="0.70"/>
        <GradientStop Color="#E6C84A" Offset="0.85"/>
        <GradientStop Color="#E45B5B" Offset="1.00"/>
    </LinearGradientBrush>
</ResourceDictionary>
```

### 9.2 Light/Dark Theme Support

WinUI 3 built-in theme support:

```csharp
// In MainWindow constructor or settings
if (Content is FrameworkElement rootElement)
{
    rootElement.RequestedTheme = ElementTheme.Dark; // Force dark theme
    // Or ElementTheme.Default for system preference
}
```

**Theme-Aware Resources**:
```xml
<ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Dark">
            <SolidColorBrush x:Key="CustomBackgroundBrush" Color="#2D2D2D"/>
        </ResourceDictionary>
        <ResourceDictionary x:Key="Light">
            <SolidColorBrush x:Key="CustomBackgroundBrush" Color="#FFFFFF"/>
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

## 10. Icon Generation Service

### 10.1 Interface (Core)

```csharp
// MicrophoneManager.Core/Services/IIconGeneratorService.cs
using System.Drawing;

namespace MicrophoneManager.Core.Services;

public interface IIconGeneratorService
{
    Icon CreateMicrophoneIcon(bool isMuted);
}
```

### 10.2 Implementation (WinUI)

```csharp
// MicrophoneManager.WinUI/Services/IconGeneratorService.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MicrophoneManager.WinUI.Services;

public class IconGeneratorService : IIconGeneratorService
{
    public Icon CreateMicrophoneIcon(bool isMuted)
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // Draw microphone icon
        var micColor = isMuted ? Color.FromArgb(255, 200, 50, 50) : Color.FromArgb(255, 50, 200, 50);
        using var brush = new SolidBrush(micColor);

        // Simple mic shape (refine as needed)
        graphics.FillEllipse(brush, 5, 3, 6, 8);
        graphics.FillRectangle(brush, 7, 11, 2, 3);

        if (isMuted)
        {
            // Draw slash for muted
            using var pen = new Pen(Color.FromArgb(255, 200, 50, 50), 2);
            graphics.DrawLine(pen, 2, 2, 14, 14);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
```

**Note**: Uses `System.Drawing` (available on Windows). Alternative: Generate PNG and convert to icon format.

## 11. Build & Packaging

### 11.1 Unpackaged Deployment (Recommended)

**Bootstrap**: Windows App SDK requires bootstrapper for unpackaged apps

**Program.cs** (optional explicit entry point):
```csharp
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Dispatching;
using WinRT;

namespace MicrophoneManager.WinUI;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        bool isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }

    private static bool DecideRedirection()
    {
        // Single-instance logic if needed
        return false;
    }
}
```

**Publish Command**:
```powershell
dotnet publish MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:WindowsAppSDKSelfContained=true
```

### 11.2 Packaged Deployment (Optional)

If MSIX packaging desired:
1. Set `<EnableMsixTooling>true</EnableMsixTooling>`
2. Add `Package.appxmanifest`
3. Configure identity, capabilities, visual assets
4. Publish via `dotnet publish` or Visual Studio MSIX packaging

## 12. Testing Strategy

### 12.1 Unit Tests (Core)

```csharp
// MicrophoneManager.Tests/ViewModels/TrayViewModelTests.cs
[Fact]
public void ToggleMute_UpdatesIsMutedProperty()
{
    // Arrange
    var mockAudioService = new Mock<IAudioDeviceService>();
    mockAudioService.Setup(x => x.ToggleDefaultMicrophoneMute()).Returns(true);

    var viewModel = new TrayViewModel(mockAudioService.Object, _ => { });

    // Act
    viewModel.ToggleMuteCommand.Execute(null);

    // Assert
    Assert.True(viewModel.IsMuted);
}
```

**Note**: DispatcherQueue in ViewModels may require mocking or testing with `[WinUITestMethod]` (WinUI test framework).

### 12.2 UI Tests

**WinAppDriver** (Windows Application Driver):
- Automate WinUI 3 app UI testing
- Verify controls, layout, interactions

**Manual Testing Checklist**:
- System tray icon shows/updates correctly
- Left-click opens flyout
- Right-click shows context menu
- Device list populates
- Volume sliders respond
- Mute toggles work
- Default/communication role buttons work
- Input level meters animate
- Peak hold indicators decay correctly
- Hotplug (connect/disconnect USB mic)
- Multi-monitor window positioning

## 13. Migration Checklist

### 13.1 Phase 1: Core Setup
- [ ] Create MicrophoneManager.Core project
- [ ] Move Models to Core (no changes needed)
- [ ] Move Services to Core (verify NAudio compatibility)
- [ ] Update ViewModels Dispatcher usage
- [ ] Add unit tests for Core

### 13.2 Phase 2: WinUI Shell
- [ ] Create MicrophoneManager.WinUI project
- [ ] Configure Windows App SDK (unpackaged)
- [ ] Implement App.xaml/cs with DI
- [ ] Implement MainWindow (hidden, tray host)
- [ ] Verify system tray icon works
- [ ] Test application lifecycle (startup, shutdown)

### 13.3 Phase 3: Views & UI
- [ ] Migrate converters to WinUI interface
- [ ] Create Colors.xaml resource dictionary
- [ ] Implement FlyoutWindow with modern styling
- [ ] Implement MicrophoneFlyout UserControl
- [ ] Migrate custom control templates (Triggers → VisualStates)
- [ ] Test UI responsiveness

### 13.4 Phase 4: Integration & Testing
- [ ] Connect ViewModels to Views
- [ ] Test audio device enumeration
- [ ] Test volume/mute controls
- [ ] Test input level meters (16ms updates)
- [ ] Test device hotplug
- [ ] Test multi-monitor scenarios
- [ ] Test startup integration
- [ ] Performance profiling

### 13.5 Phase 5: Polish & Deployment
- [ ] Add Mica/Acrylic backdrop
- [ ] Refine icon generation
- [ ] Test light/dark theme switching
- [ ] Create installer (if needed)
- [ ] Update README with WinUI 3 instructions
- [ ] Document breaking changes

## 14. Success Criteria

- ✅ WinUI 3 app compiles and runs
- ✅ System tray icon appears and updates
- ✅ Flyout shows microphone list
- ✅ Volume sliders control device volume
- ✅ Mute buttons toggle device mute state
- ✅ Input level meters animate smoothly (16ms)
- ✅ Peak hold indicators work correctly
- ✅ Default/communication role switching works
- ✅ Device hotplug handled gracefully
- ✅ All unit tests pass
- ✅ Visual appearance matches WPF version
- ✅ No performance regressions
- ✅ Single-file EXE deployment works

---

**Document Version**: 1.0
**Last Updated**: 2026-01-04
**Reviewed By**: Migration Agent
