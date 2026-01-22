# Windows Microphone Manager - AI Coding Agent Instructions

## Project Overview

This is a Windows system tray application for quick microphone selection and control, targeting Windows 10/11. Built with **C# WinUI 3 on .NET 8**.

**Core Purpose**: Provide comprehensive visibility and control over ALL audio input devices—not just the default. Users need to see and control non-default microphones that applications may be actively using (e.g., game using headset mic while default is a desk mic).

## Architecture (WinUI 3 Consolidated)

**2025 Migration Complete**: This project was consolidated from a multi-project structure (WPF + Core + WinUI) into a single **WinUI-only codebase**. All business logic, services, ViewModels, and converters now reside in the **`MicrophoneManager.WinUI`** project.

### Project Structure
- **MicrophoneManager.WinUI/** — Single application project (WinUI 3)
  - `Services/` — Audio device management & system integration
  - `Models/` — Data models (MicrophoneDevice)
  - `ViewModels/` — MVVM logic (TrayViewModel, MicrophoneListViewModel, MicrophoneEntryViewModel)
  - `Views/` — XAML UI components (MainWindow, MicrophoneWindow, MicrophoneFlyout)
  - `Converters/` — XAML value converters
- **MicrophoneManager.Tests/** — xUnit test suite (targets WinUI project)

### Windows Audio API Integration (Undocumented)

The application uses **NAudio + undocumented IPolicyConfig COM interface** to set default devices. This is the standard approach used by utilities like EarTrumpet.

- **[PolicyConfigService.cs](MicrophoneManager.WinUI/Services/PolicyConfigService.cs)**: COM interop wrapper for `IPolicyConfig`
  - Must maintain exact vtable order (Reserved1-10 placeholders)
  - Sets default device for `eConsole` (general apps) and `eCommunications` (Teams/Zoom) roles **independently**
  - `SetDefaultDeviceForAllRoles()` is the common case but separate role control is essential

```csharp
// Two separate default designations in Windows:
ERole.eConsole         // Games, system sounds, recordings
ERole.eCommunications  // VoIP apps (Teams, Zoom, Discord)
```

### Service Layer Architecture

**[AudioDeviceService.cs](MicrophoneManager.WinUI/Services/AudioDeviceService.cs)** (~780 lines) is the core audio management service:

- Device enumeration via NAudio's `MMDeviceEnumerator`
- Real-time device change notifications via `IMMNotificationClient`
- Per-device volume and mute control
- **Real-time input level monitoring** via `WasapiCapture` loopback (critical for troubleshooting which mic is active)
- Dual subscription pattern: volume changes + input level metering

**Key Pattern**: Service maintains subscriptions to default device changes and raises events for ViewModels to handle on UI thread:
```csharp
_audioService.DefaultDeviceChanged += (s, e) => 
    Application.Current?.Dispatcher?.TryEnqueue(RefreshDevices);
```

### MVVM with CommunityToolkit.Mvvm

- ViewModels use `[ObservableProperty]` and `[RelayCommand]` source generators
- All ViewModels inherit `ObservableObject`
- **UI thread marshaling**: Service events invoke `DispatcherQueue.TryEnqueue()` to safely update UI properties
- **Suppress loops**: Use `_suppressVolumeWrite` flags when handling external volume changes to prevent feedback loops

Example from [MicrophoneListViewModel.cs](MicrophoneManager.WinUI/ViewModels/MicrophoneListViewModel.cs#L71-L85):
```csharp
_audioService.DefaultMicrophoneVolumeChanged += (s, e) =>
    Application.Current?.Dispatcher?.TryEnqueue(() =>
    {
        _suppressVolumeWrite = true;  // Prevent feedback loop
        try { CurrentMicLevelPercent = e.VolumeLevelScalar * 100.0; }
        finally { _suppressVolumeWrite = false; }
    });
```

## Build & Test Commands

### Development Build
```powershell
dotnet build MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -p:Platform=x64
```

### Release Single-File Executable
```powershell
# Uses checked-in publish profile (recommended)
dotnet publish MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -c Release -p:PublishProfile=win-x64-singlefile

# Output: publish\win-x64-singlefile\MicrophoneManager.WinUI.exe
```

**Note**: Regular `dotnet build` requires `-p:Platform=x64` due to WinUI 3 requirements. For distribution, always use `dotnet publish` with the profile.

### Testing Strategy

**Current State**: xUnit tests with 8 core scenarios passing.

```powershell
# Run all tests
dotnet test MicrophoneManager.Tests/ -p:Platform=x64
```

**Test Coverage** (from [MicrophoneManager.Tests/](MicrophoneManager.Tests/)):
- Device enumeration and role detection (Console vs Communications)
- Volume/mute state changes with external sync
- ViewModel meter calculations and peak hold
- Command execution for setting default devices
- Feedback loop prevention (`_suppressVolumeWrite` flags)

**Manual Testing Checklist**:
1. Launch app, verify tray icon appears with correct mute state
2. Left-click → flyout shows all devices with individual meters
3. Set Default vs Communication Device independently
4. External changes (Windows Settings) sync within 1 second
5. Hot-plug USB mic → appears in list within 2 seconds

## Project-Specific Conventions

### Device ID Handling
- Device IDs are persistent GUIDs from Windows: `{0.0.1.00000000}.{guid}`
- Always use device ID (not name) for operations - names can duplicate
- Check for null returns: devices can disappear during operations

### Mute State vs Volume
- Mute is a **separate boolean flag** (`AudioEndpointVolume.Mute`)
- Setting volume to 0 ≠ muting
- Tray icon reflects Default Device mute state only (not other devices)

### System Tray Pattern
- [MainWindow.xaml](MicrophoneManager.WinUI/MainWindow.xaml) hosts the main window and initializes services
- [FlyoutWindow.xaml](MicrophoneManager.WinUI/Views/FlyoutWindow.xaml) is a topmost, no-taskbar window positioned near tray icon
- Flyout closes when losing focus (`Deactivated` event)
- System tray icon provided by H.NotifyIcon.WinUI library

### Icon Generation
[IconGenerator.cs](MicrophoneManager.WinUI/Services/IconGenerator.cs) creates tray icons programmatically:
- Draws microphone glyph with red slash for muted state
- Returns `System.Drawing.Icon` for tray
- Must call `Icon.Dispose()` when updating to prevent memory leaks

## Data Flow Examples

### Setting Default Microphone
1. User clicks device in flyout → `MicrophoneListViewModel.SelectMicrophoneCommand`
2. ViewModel calls `AudioDeviceService.SetDefaultDevice(deviceId, role)`
3. Service calls `PolicyConfigService.SetDefaultDevice()` → COM interop
4. Windows raises device change notification
5. `DeviceNotificationClient.OnDefaultDeviceChanged()` fires event
6. ViewModel receives event on UI thread → refreshes device list

### Real-Time Input Level Metering
1. `AudioDeviceService` creates `WasapiCapture` on default microphone
2. `DataAvailable` event fires ~10ms intervals with audio buffer
3. Service calculates peak level in dBFS, raises `DefaultMicrophoneInputLevelChanged` event
4. ViewModel receives on UI thread, updates `CurrentMicInputLevelPercent`
5. XAML binds to property → animates level meter

## Debugging Workflows

### Audio Device Issues

**Check NAudio Device Enumeration**:
```csharp
// Add to AudioDeviceService or debugging tool:
foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
{
    Debug.WriteLine($"Device: {device.FriendlyName} | ID: {device.ID}");
    Debug.WriteLine($"  State: {device.State} | Format: {device.AudioClient.MixFormat}");
}
```

**Verify IPolicyConfig COM Registration**:
```powershell
# Check if COM interface is available (Windows 10/11)
Get-ChildItem HKLM:\Software\Classes\CLSID | Where-Object {$_.Name -match "870AF99C"}
```

**Trace WasapiCapture Issues**:
- **No audio in meters**: Check `WaveFormat` - may need format conversion for 24-bit PCM or IEEE float
- **Access denied**: Another app may have exclusive capture mode on device
- **Meters lag**: Verify `DataAvailable` fires ~10ms intervals (check with `Debug.WriteLine`)

**UI Thread Marshaling Failures**:
```csharp
// Verify Dispatcher is available before BeginInvoke:
if (Application.Current?.Dispatcher != null)
    Application.Current.Dispatcher.BeginInvoke(...);
else
    Debug.WriteLine("WARNING: Dispatcher not available - UI won't update");
```

### Common Pitfalls

- **Device ID changes**: Device IDs persist across sessions but can change after Windows updates or driver reinstalls
- **COM object leaks**: Always `Marshal.ReleaseComObject()` after `IPolicyConfig` calls
- **Icon memory leaks**: Call `Icon.Dispose()` before generating new tray icon
- **Feedback loops**: External volume changes trigger events → ViewModel updates → triggers service call → loop. Use `_suppressVolumeWrite` flags.

## Known Limitations

- **No per-device input metering yet**: Only default device shows real-time levels (P3 feature)
- **IPolicyConfig unsupported by Microsoft**: Works reliably but could break in future Windows updates
- **Single-file publish size**: ~150MB due to self-contained .NET runtime + compression

## External Dependencies

- **NAudio 2.2.1**: Audio device enumeration and WASAPI capture
- **H.NotifyIcon.WinUI 2.1.3**: System tray icon implementation
- **CommunityToolkit.Mvvm 8.3.2**: MVVM source generators

## OpenSpec Integration

Project uses [OpenSpec](https://github.com/InfraredAces/openspec) for specification-driven development:
- Feature specifications in `openspec/specs/<capability>/spec.md` with requirements and scenarios
- Change proposals in `openspec/changes/<change-id>/` for new features and modifications
- [CLAUDE.md](CLAUDE.md) contains quick reference for active technologies and commands
- Specifications drive development - see [openspec/AGENTS.md](openspec/AGENTS.md) for workflow details

<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

---

**Quick Start**: Run `dotnet build MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -p:Platform=x64`, launch `MicrophoneManager.WinUI.exe` from `bin/x64/Debug/`, click tray icon to see microphone list.