# Project Context

## Purpose
Windows Microphone Manager is a Windows 10/11 system tray application for quick microphone selection and control. It provides comprehensive visibility and control over ALL audio input devices—not just the default microphone. Users can see and control non-default microphones that applications may be actively using (e.g., game using headset mic while default is a desk mic).

## Tech Stack
- C# (.NET 8)
- WinUI 3 (Windows App SDK)
- NAudio 2.2.1 (Windows Audio API integration)
- H.NotifyIcon.WinUI 2.1.3 (System tray)
- CommunityToolkit.Mvvm 8.3.2 (MVVM source generators)
- xUnit (Testing framework)

## Project Conventions

### Code Style
- MVVM pattern with CommunityToolkit.Mvvm source generators
- Use `[ObservableProperty]` and `[RelayCommand]` attributes
- All ViewModels inherit from `ObservableObject`
- Avoid manual property change notification implementations

### Architecture Patterns
- **Single WinUI Project**: All code consolidated in `MicrophoneManager.WinUI/`
- **Service Layer**: Audio operations via `AudioDeviceService` and `PolicyConfigService`
- **COM Interop**: Undocumented `IPolicyConfig` interface for default device control
- **Event-Driven UI Updates**: Services raise events, ViewModels marshal to UI thread via `DispatcherQueue.TryEnqueue()`
- **Feedback Loop Prevention**: Use `_suppressVolumeWrite` flags when handling external events

### Testing Strategy
- xUnit tests in `MicrophoneManager.Tests/`
- Fake service implementations for ViewModels (e.g., `FakeAudioDeviceService`)
- Test coverage: device enumeration, role detection, volume/mute sync, meter calculations, command execution
- All tests must pass with `dotnet test -p:Platform=x64`

### Git Workflow
- Main branch: `main`
- Feature branches for new capabilities
- Commit messages: conventional commits preferred
- OpenSpec workflow for proposals and changes

## Domain Context
### Windows Audio Roles
- **eConsole**: Default device for games, system sounds, general recording
- **eCommunications**: Default device for VoIP (Teams, Zoom, Discord)
- These are independent designations in Windows

### Device Identification
- Device IDs are persistent GUIDs: `{0.0.1.00000000}.{guid}`
- Always use device ID (not name) for operations—names can duplicate
- Device IDs can change after Windows updates or driver reinstalls

### Audio API Details
- NAudio wraps Windows Core Audio APIs
- Real-time input level monitoring via WasapiCapture loopback
- Volume control via `AudioEndpointVolume` interface
- Mute is a separate boolean flag (volume=0 ≠ muted)

## Important Constraints
- **Platform**: Windows 10/11 only (x64 builds required for WinUI 3)
- **IPolicyConfig**: Undocumented COM interface—could break in future Windows updates
- **Single-file publish**: ~150MB due to self-contained .NET runtime
- **UI Thread Requirements**: All UI updates must be marshaled via Dispatcher
- **Device Hot-Plugging**: Must handle devices appearing/disappearing at runtime

## External Dependencies
- **NAudio**: Audio device enumeration and WASAPI capture
- **H.NotifyIcon.WinUI**: System tray icon implementation
- **CommunityToolkit.Mvvm**: MVVM source generators
- **Windows Core Audio APIs**: MMDeviceEnumerator, IMMNotificationClient, AudioEndpointVolume
- **Undocumented APIs**: IPolicyConfig COM interface for default device control
