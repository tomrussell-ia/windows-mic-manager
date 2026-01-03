# Windows Microphone Manager

A Windows 11 system tray application for quick microphone selection, similar to the native speaker quick-select flyout.

## Features

- **System tray icon** - Microphone indicator that shows muted state
- **Click to open flyout** - Shows all available microphones
- **One-click selection** - Changes both Default and Communication Device roles together
- **Mute/unmute button** - Quick toggle in flyout, tray icon reflects muted state
- **Real-time updates** - Automatically detects device changes (plug/unplug)

## Requirements

- Windows 10 version 1809 or later (Windows 11 recommended)
- .NET 8.0 Runtime (included in self-contained builds)

## Building

### Prerequisites

- .NET 8.0 SDK

### Build from command line

```bash
cd MicrophoneManager
dotnet build
```

Note: `dotnet build` produces a normal development layout (lots of files). For distribution as a single `.exe`, use `dotnet publish`.

### Build portable EXE (Release)

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

For this repo, Release publishing is configured to produce a **single, self-contained** `MicrophoneManager.exe`.

Recommended (uses the checked-in publish profile):

```bash
dotnet publish MicrophoneManager/MicrophoneManager.csproj -p:PublishProfile=win-x64-singlefile
```

The output will be in `publish\win-x64-singlefile\`.

## Usage

1. Run `MicrophoneManager.exe`
2. A microphone icon appears in the system tray
3. **Left-click** the icon to open the microphone selection flyout
4. Click on any microphone to set it as the default
5. Use the "Mute/Unmute" button at the bottom to toggle mute
6. **Right-click** the icon and select "Exit" to close the application

## Technical Details

### Architecture

- **Framework**: WPF (.NET 8)
- **System Tray**: H.NotifyIcon.Wpf
- **Audio Management**: NAudio + IPolicyConfig COM interface
- **MVVM**: CommunityToolkit.Mvvm

### Setting Default Microphone

Windows does not provide a public API to change the default audio device. This application uses the undocumented `IPolicyConfig` COM interface, which is the standard approach used by utilities like EarTrumpet. This works reliably but is not officially supported by Microsoft.

### Device Roles

Windows has two "default" designations:
- **Default Device** (eConsole) - Used by most apps
- **Default Communication Device** (eCommunications) - Used by Teams, Zoom, etc.

This application sets both roles when you select a microphone.

## Project Structure

```
MicrophoneManager/
├── App.xaml                    # Application entry
├── MainWindow.xaml             # Hidden window hosting tray icon
├── Views/
│   └── MicrophoneFlyout.xaml   # Flyout UI with device list
├── ViewModels/
│   ├── TrayViewModel.cs        # Tray icon state
│   └── MicrophoneListViewModel.cs
├── Services/
│   ├── AudioDeviceService.cs   # NAudio device enumeration
│   ├── PolicyConfigService.cs  # IPolicyConfig wrapper (set defaults)
│   └── IconGenerator.cs        # Programmatic icon generation
├── Models/
│   └── MicrophoneDevice.cs     # Device data model
└── Converters/
    └── BoolToVisibilityConverter.cs
```

## License

MIT License - Feel free to use and modify as needed.
