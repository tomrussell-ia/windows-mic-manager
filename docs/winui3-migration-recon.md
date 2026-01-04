# WinUI 3 Migration Reconnaissance

**Date**: 2026-01-04
**Status**: Complete
**WPF Version**: .NET 8, Windows Desktop

## Executive Summary

This document provides a comprehensive analysis of the existing WPF application architecture to inform the WinUI 3 migration strategy.

**Application Type**: Windows system tray utility for microphone management
**Core Technology Stack**: C# WPF on .NET 8 with NAudio audio library
**Architecture Pattern**: MVVM using CommunityToolkit.Mvvm
**Target Platform**: Windows 10/11

## 1. Solution Structure

### 1.1 Projects

```
MicrophoneManager.sln
├── MicrophoneManager/                  (Main WPF application)
│   └── MicrophoneManager.csproj        (.NET 8, WPF + WindowsForms)
└── MicrophoneManager.Tests/            (Unit tests)
    └── MicrophoneManager.Tests.csproj  (.NET 8)
```

**Platform Configuration**: x64 primary (for native audio APIs), AnyCPU supported
**Build Type**: Self-contained single-file executable in Release mode
**High DPI**: PerMonitorV2 awareness enabled

### 1.2 Project Configuration Analysis

**MicrophoneManager.csproj Key Settings**:
- `<UseWPF>true</UseWPF>` — Must migrate to WinUI 3
- `<UseWindowsForms>true</UseWindowsForms>` — Used for `System.Windows.Forms.Screen` class (multi-monitor support)
- `PublishSingleFile=true` — WinUI 3 supports this
- `ApplicationManifest>app.manifest` — May need adjustments for WinUI 3

## 2. WPF Entry Points & Application Lifecycle

### 2.1 Application Entry

**App.xaml** (`MicrophoneManager/App.xaml`)
- Entry point: `Startup="Application_Startup"`
- Lifecycle: `ShutdownMode="OnExplicitShutdown"` (system tray keeps app alive)
- Resources: Converters, color brushes, gradient definitions

**App.xaml.cs** (`MicrophoneManager/App.xaml.cs`)
- Creates hidden MainWindow hosting system tray icon
- Static service references: `TrayViewModel`, `AudioService`, `DockedWindow`
- Cleanup in `OnExit`: Disposes services and closes windows

**Migration Impact**: WinUI 3 uses different application initialization pattern. Must adapt `Application` class lifecycle.

### 2.2 Main Window Structure

**MainWindow.xaml** (`MicrophoneManager/MainWindow.xaml`)
- **Type**: Hidden window (Width="0" Height="0", WindowStyle="None")
- **Purpose**: Hosts H.NotifyIcon.Wpf `TaskbarIcon` control
- **Features**:
  - Left-click: Shows TrayPopup (MicrophoneFlyout)
  - Right-click: Context menu (startup toggle, exit)
  - Bindings to TrayViewModel

**MainWindow.xaml.cs**
- Initializes `AudioDeviceService`
- Creates `TrayViewModel` with icon update callback
- Hides window after loading (`Window_Loaded`)
- Handles cleanup on close

**Migration Challenge**: H.NotifyIcon.Wpf is WPF-specific. WinUI 3 alternatives:
1. H.NotifyIcon.WinUI (if available)
2. Microsoft.Toolkit.Uwp.Notifications + custom tray handling
3. Win32 API interop for system tray

## 3. UI Architecture & Navigation

### 3.1 Shell Pattern

**No traditional navigation shell** — This is a **system tray application** with:
- Hidden main window hosting tray icon
- Flyout window (popup) for microphone list
- No NavigationView, Frame, or page-based navigation

### 3.2 View Hierarchy

```
MainWindow (hidden)
└── TaskbarIcon (H.NotifyIcon.Wpf)
    ├── TrayPopup → MicrophoneFlyout (inline popup)
    └── ContextMenu → Startup toggle, Exit

FlyoutWindow (docked mode)
└── MicrophoneFlyout (UserControl)
    └── ListBox (microphone cards)
        └── DataTemplate → MicrophoneEntryViewModel
```

### 3.3 Primary Views

#### MainWindow.xaml
- **Purpose**: System tray icon host
- **Controls**: `tb:TaskbarIcon` (H.NotifyIcon.Wpf)
- **DataContext**: TrayViewModel
- **WPF-Specific**: TaskbarIcon, TrayPopup, ContextMenu integration

#### FlyoutWindow.xaml (`Views/FlyoutWindow.xaml`)
- **Purpose**: Standalone docked/floating window
- **Features**:
  - Borderless (`WindowStyle="None"`, `AllowsTransparency="True"`)
  - Rounded corners (8px CornerRadius)
  - Drop shadow effect
  - Drag-to-move with Alt override
  - Escape to close
  - MaxHeight based on `SystemParameters.WorkArea`
- **Content**: Hosts MicrophoneFlyout UserControl
- **WPF Dependencies**:
  - `System.Windows.Input.MouseButtonEventArgs`
  - `VisualTreeHelper` for control hit testing
  - `SystemParameters.WorkArea` for screen bounds

#### MicrophoneFlyout.xaml (`Views/MicrophoneFlyout.xaml`)
- **Purpose**: Main microphone list UI
- **Layout**:
  - Header with dock/undock button (Segoe MDL2 Assets icons)
  - ScrollViewer with ListBox (max 360px height)
  - Custom ListBoxItem template (dark theme cards)
  - "No microphones detected" message
- **Per-Device Card Features**:
  - Device name, format tag (e.g., "48 kHz, 24-bit")
  - Set Default / Set Communication buttons (icon buttons)
  - Real-time input level meter (gradient bar with peak hold indicator)
  - Scale overlay (0-100 with tick marks)
  - Mute button + volume slider
- **Custom Styles**:
  - `MicListItem`: Rounded cards with hover (#3D3D3D) and selection (#0078D4)
  - `ModernButton`: Rounded buttons with hover states
  - `IconActionButton`: Small 32x24 icon buttons
  - `CompactSlider`: Custom track/thumb template
- **WPF-Specific**:
  - ListBox with ItemContainerStyle
  - Custom ControlTemplates with Triggers
  - MultiBinding with custom converters
  - TranslateTransform for meter animations

#### MicrophoneFlyout.xaml.cs (`Views/MicrophoneFlyout.xaml.cs`)
- Dock/undock logic
- Creates/shows FlyoutWindow for docked mode
- Positions window at cursor or screen edges
- DataContext: MicrophoneListViewModel

### 3.4 Theming & Resources

**App.xaml Resources**:
```xml
<!-- Color Palette (Windows 11 dark theme) -->
<SolidColorBrush x:Key="AccentBrush" Color="#0078D4"/>
<SolidColorBrush x:Key="BackgroundBrush" Color="#2D2D2D"/>
<SolidColorBrush x:Key="ForegroundBrush" Color="#FFFFFF"/>
<SolidColorBrush x:Key="HoverBrush" Color="#3D3D3D"/>

<!-- Meter Gradient (green → yellow → red) -->
<LinearGradientBrush x:Key="MeterGradientBrush" .../>
```

**Icons**: Segoe MDL2 Assets font (e.g., &#xE720; for microphone)

**Migration Notes**:
- WinUI 3 uses Segoe Fluent Icons (different glyph codes)
- WinUI ResourceDictionary syntax similar but with differences
- Acrylic/Mica material available in WinUI 3 for modern backdrop

## 4. MVVM Implementation

### 4.1 Framework

**CommunityToolkit.Mvvm** (v8.3.2)
- Source generators: `[ObservableProperty]`, `[RelayCommand]`
- Base class: `ObservableObject`
- No WPF dependencies in toolkit itself

**Migration Impact**: CommunityToolkit.Mvvm is framework-agnostic ✅ — Can reuse as-is

### 4.2 ViewModels

#### TrayViewModel.cs (`ViewModels/TrayViewModel.cs`)
- **Purpose**: System tray icon state and commands
- **Properties**:
  - `TooltipText` (device name or "No microphone")
  - `IsMuted` (drives icon visual)
  - `IsStartupEnabled` (Windows startup toggle)
- **Commands**: ToggleMute, ToggleStartup, Exit
- **WPF Dependencies**:
  - `System.Windows.Threading.Dispatcher` — Used for thread marshaling
  - `System.Windows.Application.Current` — Used for Dispatcher access and Shutdown
- **Event Subscriptions**: AudioService device change events

**Migration**: Replace `Dispatcher` with `DispatcherQueue`, `Application.Current` with WinUI equivalent

#### MicrophoneListViewModel.cs (`ViewModels/MicrophoneListViewModel.cs`)
- **Purpose**: Manages microphone device list and global state
- **Properties**:
  - `ObservableCollection<MicrophoneEntryViewModel> Microphones`
  - `SelectedMicrophone` (default device)
  - `IsMuted` (global mute state)
  - `CurrentMicLevelPercent` (default mic volume)
  - `CurrentMicInputLevelPercent` (real-time meter)
  - Peak hold properties with OBS-style decay
- **Timers**:
  - `DispatcherTimer` for peak hold decay (16ms interval)
  - `DispatcherTimer` for meter polling (16ms interval)
- **Commands**: ToggleMute
- **WPF Dependencies**:
  - `System.Windows.Threading.DispatcherTimer` ⚠️
  - `System.Windows.Threading.DispatcherPriority` ⚠️
  - `System.Windows.Application.Current.Dispatcher` ⚠️
- **Logic Patterns**:
  - `_suppressVolumeWrite` flag to prevent feedback loops
  - `InvokeOnUiThread` helper for safe UI updates
  - Device list diffing on refresh (update existing VMs, remove stale, add new)

**Migration**: Replace DispatcherTimer with WinUI equivalent (DispatcherQueueTimer)

#### MicrophoneEntryViewModel.cs (`ViewModels/MicrophoneEntryViewModel.cs`)
- **Purpose**: Represents a single microphone device
- **Properties**:
  - `Id`, `Name`, `FormatTag`
  - `IsDefault`, `IsDefaultCommunication`
  - `IsMuted`, `VolumePercent`
  - `InputLevelPercent`, `PeakLevelPercent` (OBS-style ballistics)
- **Commands**: SetDefault, SetDefaultCommunication, SetBoth, ToggleMute
- **Logic**:
  - OBS-style meter ballistics (instant attack, exponential release ~300ms)
  - Peak hold with 5s hold time, 20 dB/s decay
  - Suppresses volume write-back during external updates
- **WPF Dependencies**: None ✅

**Migration**: Can reuse as-is ✅

### 4.3 Dispatcher Usage Summary

| ViewModel | WPF Dispatcher Usage | Frequency |
|-----------|---------------------|-----------|
| TrayViewModel | `_dispatcher.BeginInvoke()` | Event callbacks |
| MicrophoneListViewModel | `Application.Current.Dispatcher` | Event callbacks |
| MicrophoneListViewModel | `DispatcherTimer` x2 | 16ms timers |
| MicrophoneEntryViewModel | None | N/A |

**Migration Strategy**: Create abstraction layer or replace with `DispatcherQueue` APIs

## 5. Services & Domain Logic (Reusable)

### 5.1 IAudioDeviceService Interface

**File**: `Services/IAudioDeviceService.cs`

**Contract**:
```csharp
public interface IAudioDeviceService : IDisposable
{
    // Events
    event EventHandler? DevicesChanged;
    event EventHandler? DefaultDeviceChanged;
    event EventHandler<...>? DefaultMicrophoneVolumeChanged;
    event EventHandler<...>? DefaultMicrophoneInputLevelChanged;

    // Device enumeration
    List<MicrophoneDevice> GetMicrophones();
    MicrophoneDevice? GetDefaultMicrophone();
    string? GetDefaultDeviceId(Role role);

    // Device control
    void SetDefaultMicrophone(string deviceId);
    void SetMicrophoneForRole(string deviceId, Role role);
    void SetDefaultMicrophoneVolumePercent(double volumePercent);
    void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar);
    bool ToggleMute(string deviceId);
    bool IsMuted(string deviceId);
    bool ToggleDefaultMicrophoneMute();
    bool IsDefaultMicrophoneMuted();
}
```

**Migration**: Interface is framework-agnostic ✅ — Can reuse as-is

### 5.2 AudioDeviceService.cs

**File**: `Services/AudioDeviceService.cs` (~780 lines per AGENT.md)

**Dependencies**:
- `NAudio.CoreAudioApi` (MMDeviceEnumerator, MMDevice, AudioEndpointVolume)
- `NAudio.Wave` (WasapiCapture for real-time input metering)
- Custom COM interop: `IMMNotificationClient` for device change events

**Key Features**:
- Device enumeration (capture devices, active state)
- Real-time volume/mute notification via `AudioEndpointVolume.OnVolumeNotification`
- Real-time input level capture via `WasapiCapture.DataAvailable`
- Peak detection and meter math (see ObsMeterMath.cs)
- Dual subscription pattern (volume notifications, input metering)

**Framework Dependencies**: None — Pure NAudio + Win32 COM ✅

**Migration**: Can reuse as-is ✅

### 5.3 PolicyConfigService.cs

**File**: `Services/PolicyConfigService.cs`

**Purpose**: COM interop for undocumented Windows `IPolicyConfig` interface to set default audio devices

**Critical Details** (from AGENT.md):
- Must maintain exact vtable order (Reserved1-10 placeholders)
- Sets default for `eConsole` vs `eCommunications` roles independently
- Standard approach used by utilities like EarTrumpet

**Framework Dependencies**: None — Pure COM interop ✅

**Migration**: Can reuse as-is ✅

### 5.4 Other Services

#### StartupService.cs
- **Purpose**: Registry-based Windows startup management
- **Dependencies**: `Microsoft.Win32.Registry`
- **Migration**: Can reuse as-is ✅

#### IconGenerator.cs
- **Purpose**: Generates system tray icon bitmap (muted/unmuted)
- **Dependencies**: Likely `System.Drawing` or WPF imaging
- **Migration**: May need adjustment for WinUI 3 icon format

#### ObsMeterMath.cs
- **Purpose**: OBS-style dB ↔ percent conversion for meters
- **Dependencies**: None (pure math)
- **Migration**: Can reuse as-is ✅

### 5.5 Service Summary

| Service | Framework Deps | Reusable? | Notes |
|---------|---------------|-----------|-------|
| IAudioDeviceService | None | ✅ | Interface only |
| AudioDeviceService | None | ✅ | Pure NAudio |
| PolicyConfigService | None | ✅ | COM interop |
| StartupService | None | ✅ | Registry API |
| IconGenerator | Possibly | ⚠️ | Check imaging API |
| ObsMeterMath | None | ✅ | Pure math |

## 6. Models

### 6.1 MicrophoneDevice.cs

**File**: `Models/MicrophoneDevice.cs`

**Type**: Immutable record-style DTO (required init properties)

```csharp
public class MicrophoneDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? IconPath { get; init; }
    public bool IsDefault { get; init; }
    public bool IsDefaultCommunication { get; init; }
    public bool IsMuted { get; init; }
    public float VolumeLevel { get; init; }
    public string FormatTag { get; init; } = "";
    public double InputLevelPercent { get; init; }
    public bool IsSelected => IsDefault || IsDefaultCommunication;
}
```

**Dependencies**: None ✅

**Migration**: Can reuse as-is ✅

## 7. Converters (WPF-Specific)

### 7.1 Converter List

All located in `Converters/`:

1. **BoolToVisibilityConverter.cs**
   - Converts `bool` → `Visibility` (True=Visible, False=Collapsed)
   - WPF-specific: `IValueConverter`, `Visibility` enum

2. **MuteStateToIconConverter.cs**
   - Converts `bool` (IsMuted) → Segoe MDL2 icon glyph string
   - Framework-agnostic (string output) ✅

3. **MuteStateToLabelConverter.cs**
   - Converts `bool` (IsMuted) → "Mute" or "Unmute" string
   - Framework-agnostic (string output) ✅

4. **PercentToWidthConverter.cs**
   - `MultiBinding` converter: (percent, trackWidth) → pixel width for meters
   - Optional parameter: "remaining", "1", "2" for different meter overlays
   - Framework-agnostic (math only) ✅

### 7.2 Migration Strategy

- **BoolToVisibilityConverter**: WinUI 3 has built-in `x:Boolean` → `Visibility` support or use community toolkit converter
- **Other converters**: Can reuse logic, but must implement WinUI 3 `IValueConverter` interface

**WPF Interface**:
```csharp
public interface IValueConverter
{
    object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
```

**WinUI 3 Interface** (Microsoft.UI.Xaml.Data):
```csharp
public interface IValueConverter
{
    object Convert(object value, Type targetType, object parameter, string language);
    object ConvertBack(object value, Type targetType, object parameter, string language);
}
```

Difference: `CultureInfo culture` → `string language`

## 8. Third-Party Dependencies

### 8.1 NuGet Packages

From `MicrophoneManager.csproj`:

| Package | Version | Purpose | WinUI 3 Compatibility |
|---------|---------|---------|----------------------|
| **H.NotifyIcon.Wpf** | 2.1.3 | System tray icon | ❌ WPF-specific → Need H.NotifyIcon.WinUI or alternative |
| **NAudio** | 2.2.1 | Audio device management | ✅ Framework-agnostic |
| **CommunityToolkit.Mvvm** | 8.3.2 | MVVM toolkit | ✅ Framework-agnostic |

### 8.2 System References

- `System.Windows.Forms` (for `Screen` class) — Multi-monitor support
  - **Migration**: WinUI 3 can use `Microsoft.UI.Windowing` APIs or keep Forms reference

## 9. WPF-Only Features & Patterns

### 9.1 Critical WPF Dependencies

1. **System Tray**: H.NotifyIcon.Wpf
2. **Dispatcher**: `System.Windows.Threading.Dispatcher` and `DispatcherTimer`
3. **VisualTreeHelper**: Used in FlyoutWindow for interactive control detection
4. **SystemParameters**: Used for screen bounds (`SystemParameters.WorkArea`)
5. **DragMove**: Window dragging in FlyoutWindow
6. **Converters**: WPF `IValueConverter` interface
7. **Custom Control Templates**: WPF `ControlTemplate` with `Triggers`
8. **Window Properties**: `AllowsTransparency`, `WindowStyle`, `ShowInTaskbar`
9. **Effects**: `DropShadowEffect` for window shadows

### 9.2 XAML Features Used

- **Attached Properties**: `tb:TaskbarIcon`, `ScrollViewer.VerticalScrollBarVisibility`
- **Triggers**: `DataTrigger`, `Trigger` (Property/IsMouseOver/IsSelected)
- **MultiBinding**: For meter width calculations
- **TranslateTransform**: For meter animations
- **TemplateBinding**: In custom control templates
- **RelativeSource**: `{RelativeSource AncestorType=UserControl}`

**WinUI 3 Support**:
- ✅ Most attached properties
- ⚠️ Triggers → WinUI 3 uses VisualStates + StateTriggers (different pattern)
- ✅ MultiBinding (with CommunityToolkit)
- ✅ Transforms
- ✅ TemplateBinding
- ✅ RelativeSource

## 10. Build & Packaging

### 10.1 Current Configuration

**Build Type**: Self-contained single-file executable
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

**Platform**: x64 primary, AnyCPU supported

### 10.2 Packaging Decision

**Current**: Unpackaged (plain EXE)

**WinUI 3 Options**:
1. **Unpackaged** (recommended for continuity):
   - Uses Windows App SDK unpackaged deployment
   - Requires bootstrap initialization
   - Can use single-file publish
   - No Store deployment
   - No MSIX sandboxing

2. **Packaged (MSIX)**:
   - Modern deployment
   - Store-ready
   - Auto-updates
   - Requires package identity
   - More complex CI/CD

**Recommendation**: Start with **unpackaged** to maintain current EXE-based deployment model.

### 10.3 CI/CD

**Current**: No CI config found in `.github/workflows/`

**Migration**: Will need to add CI for WinUI 3 build when ready

## 11. Dependency Map & Reusability

### 11.1 Assembly Structure

```
MicrophoneManager (WPF project)
├── Models/              ✅ 100% reusable (POCO)
├── Services/            ✅ 95% reusable (check IconGenerator)
├── ViewModels/          ⚠️ 70% reusable (Dispatcher usage)
├── Views/               ❌ 0% reusable (WPF XAML + code-behind)
├── Converters/          ⚠️ 80% reusable (logic OK, interface change)
├── App.xaml/cs          ❌ WPF-specific (must rewrite)
└── MainWindow.xaml/cs   ❌ WPF-specific (must rewrite)
```

### 11.2 Reusability Matrix

| Layer | Reusable As-Is | Needs Adaptation | Must Rewrite |
|-------|---------------|------------------|--------------|
| Models | ✅ 100% | - | - |
| Services (IAudioDeviceService) | ✅ 100% | - | - |
| Services (AudioDeviceService) | ✅ 100% | - | - |
| Services (PolicyConfigService) | ✅ 100% | - | - |
| Services (StartupService) | ✅ 100% | - | - |
| Services (ObsMeterMath) | ✅ 100% | - | - |
| Services (IconGenerator) | - | ⚠️ 100% | - |
| ViewModels (MicrophoneEntryViewModel) | ✅ 100% | - | - |
| ViewModels (TrayViewModel) | - | ⚠️ 100% (Dispatcher) | - |
| ViewModels (MicrophoneListViewModel) | - | ⚠️ 100% (DispatcherTimer) | - |
| Converters (logic) | ✅ 80% | ⚠️ 20% (interface) | - |
| Views (XAML) | - | - | ❌ 100% |
| Views (code-behind) | - | ⚠️ 50% (logic) | ❌ 50% (UI APIs) |
| App lifecycle | - | - | ❌ 100% |

### 11.3 Recommended Project Structure for Migration

```
MicrophoneManager.Shared/           (New class library, .NET 8)
├── Models/
├── Services/
├── ViewModels/                    (with abstracted Dispatcher)
└── Converters/ (logic only)

MicrophoneManager.WinUI/           (New WinUI 3 app)
├── App.xaml/cs
├── MainWindow.xaml/cs
├── Views/
├── Converters/ (WinUI implementations)
└── Reference: MicrophoneManager.Shared

MicrophoneManager/                 (Keep for now, deprecate later)
└── (Current WPF app)
```

**Alternative**: Convert MicrophoneManager project to WinUI 3 in-place (more disruptive but simpler structure)

## 12. Critical Migration Challenges

### 12.1 System Tray Icon

**WPF**: H.NotifyIcon.Wpf with `TaskbarIcon` control
**WinUI 3 Options**:
1. **H.NotifyIcon.WinUI** (check if exists and is maintained)
2. **Microsoft.Toolkit.Uwp.Notifications** + custom `NOTIFYICONDATA` Win32 interop
3. **Win32 Shell_NotifyIcon** P/Invoke directly

**Recommendation**: Investigate H.NotifyIcon.WinUI first; fallback to Win32 interop wrapper if needed.

### 12.2 Triggers → VisualStates

**WPF Pattern**:
```xml
<ControlTemplate.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
        <Setter TargetName="Border" Property="Background" Value="#3D3D3D"/>
    </Trigger>
</ControlTemplate.Triggers>
```

**WinUI 3 Pattern**:
```xml
<VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="CommonStates">
        <VisualState x:Name="PointerOver">
            <VisualState.Setters>
                <Setter Target="Border.Background" Value="#3D3D3D"/>
            </VisualState.Setters>
        </VisualState>
    </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

**Impact**: All custom control templates (buttons, list items, sliders) need VisualState rewrite.

### 12.3 Window Transparency & Borders

**WPF**: `AllowsTransparency="True"`, `WindowStyle="None"`, custom rounded borders
**WinUI 3**:
- Uses `AppWindow` APIs
- Built-in rounded corners (Windows 11)
- Backdrop materials (Acrylic, Mica)
- Different layering model

**Challenge**: Achieving exact visual parity for FlyoutWindow with drop shadow.

### 12.4 Dispatcher Abstraction

**Options**:
1. Create `IDispatcher` interface + WPF/WinUI implementations
2. Replace all `Dispatcher` usage with `DispatcherQueue` in ViewModels
3. Use CommunityToolkit's threading helpers if available

**Recommendation**: Option 2 (direct replacement) for simplicity if only targeting WinUI 3.

### 12.5 Multi-Monitor Support

**WPF**: Uses `System.Windows.Forms.Screen` class
**WinUI 3**:
- `Microsoft.UI.Windowing.DisplayArea` APIs
- Or keep WinForms reference (works but mixing frameworks)

### 12.6 DispatcherTimer Replacement

**WPF**: `System.Windows.Threading.DispatcherTimer`
**WinUI 3**: `Microsoft.UI.Dispatching.DispatcherQueueTimer`

**Impact**: 2 timers in MicrophoneListViewModel (16ms polling)

## 13. Testing Strategy

### 13.1 Current Tests

**Project**: MicrophoneManager.Tests
**Coverage**: Not analyzed in detail, but ViewModels are testable (MVVM separation)

### 13.2 Migration Testing

**Phase 1 (Shared Layer)**:
- Unit tests for Services → Should pass unchanged ✅
- Unit tests for ViewModels → May need Dispatcher mocking updates ⚠️

**Phase 2 (WinUI 3 UI)**:
- Manual testing of UI interactions
- Visual regression testing for theming
- Device hotplug testing (USB mic connect/disconnect)
- Multi-monitor window positioning

## 14. Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| System tray library unavailable | **High** | Research H.NotifyIcon.WinUI or plan Win32 interop |
| VisualState complexity | **Medium** | Incremental control migration, test thoroughly |
| Meter animations broken | **Medium** | Verify TranslateTransform, fallback to code-behind |
| Multi-monitor positioning | **Low** | Use DisplayArea APIs or keep WinForms ref |
| Performance regression | **Low** | Profile meter updates (16ms timers) |
| Audio API compatibility | **Low** | NAudio should work unchanged |

## 15. Migration Prerequisites

### 15.1 Tools & SDKs

- **Visual Studio 2022** (17.8+) with:
  - .NET 8 SDK
  - Windows App SDK 1.5+ workload
  - WinUI 3 templates
- **Windows SDK**: 10.0.19041.0 or newer

### 15.2 Research Tasks

1. ✅ Confirm H.NotifyIcon.WinUI availability and maturity
2. ✅ Test NAudio compatibility with WinUI 3 process model
3. ✅ Validate single-file publish with Windows App SDK unpackaged
4. ✅ Test DispatcherQueueTimer precision for 16ms meter updates

## 16. Next Steps

1. **Create Target Architecture Document** (`winui3-architecture.md`)
   - Define WinUI 3 project structure
   - Document Dispatcher abstraction approach
   - Specify system tray implementation
   - Design VisualState templates

2. **Create Migration Plan** (`winui3-migration-plan.md`)
   - Stage A: Skeleton WinUI 3 app with DI and shell
   - Stage B: Vertical slice (single microphone card with meter)
   - Stage C: Full parity

3. **Create Control Mapping Guide** (`wpf-to-winui-mapping.md`)
   - Document WPF → WinUI control equivalents
   - List XAML syntax differences
   - Provide migration snippets

---

**Document Version**: 1.0
**Last Updated**: 2026-01-04
**Reviewed By**: Migration Agent
