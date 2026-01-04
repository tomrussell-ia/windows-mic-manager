# Building the WinUI 3 Version

## Prerequisites

### Required Software

1. **Windows 11** (recommended) or Windows 10 version 1809 (build 17763) or later
2. **Visual Studio 2022** (17.8 or later) with the following workloads:
   - .NET Desktop Development
   - Universal Windows Platform development
   - Windows App SDK (via individual components)
3. **.NET 8 SDK** (included with VS 2022 17.8+)
4. **Windows SDK 10.0.19041.0 or later**

### Visual Studio Setup

Install these components via Visual Studio Installer:

```
Workloads:
- .NET Desktop Development
- Universal Windows Platform development

Individual Components:
- Windows App SDK C# Templates
- Windows 11 SDK (10.0.22621.0)
```

## Building from Command Line

### Debug Build

```powershell
# Restore packages
dotnet restore MicrophoneManager.sln

# Build the WinUI project
dotnet build MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -c Debug

# Run the application
dotnet run --project MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj
```

### Release Build (Single-File EXE)

```powershell
# Publish as self-contained single-file executable
dotnet publish MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:WindowsAppSDKSelfContained=true

# Output location:
# MicrophoneManager.WinUI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/MicrophoneManager.WinUI.exe
```

## Building from Visual Studio

1. Open `MicrophoneManager.sln` in Visual Studio 2022
2. Set `MicrophoneManager.WinUI` as the startup project (right-click → Set as Startup Project)
3. Select platform: **x64** (required for audio APIs)
4. Build → Build Solution (Ctrl+Shift+B)
5. Debug → Start Debugging (F5) or Start Without Debugging (Ctrl+F5)

## Project Structure

```
MicrophoneManager.sln
├── MicrophoneManager/          [WPF - Original version]
├── MicrophoneManager.Tests/    [Unit tests]
├── MicrophoneManager.Core/     [NEW - Shared business logic]
└── MicrophoneManager.WinUI/    [NEW - WinUI 3 UI layer]
```

## Migration Status

### ✅ Stage A: Skeleton App (Current)

- [x] Project structure created
- [x] WinUI 3 application shell
- [x] System tray icon integration (H.NotifyIcon.WinUI)
- [x] Basic flyout window
- [x] Dependency injection setup
- [ ] **TODO**: Verify H.NotifyIcon.WinUI package availability
- [ ] **TODO**: Test build on Windows machine
- [ ] **TODO**: Verify system tray icon appears

### ⏳ Stage B: Vertical Slice (Next)

- [ ] Move Services to Core project
- [ ] Adapt ViewModels for DispatcherQueue
- [ ] Implement single microphone card
- [ ] Real-time input meter
- [ ] Volume/mute controls

### ⏳ Stage C: Full Parity

- [ ] Complete microphone list
- [ ] Device hotplug handling
- [ ] Dock/undock functionality
- [ ] All visual polish

## Known Issues / Notes

### H.NotifyIcon.WinUI Package

The project currently references `H.NotifyIcon.WinUI` version 2.1.3. This package may not exist or may have a different name. If the build fails with a package not found error:

**Option 1**: Check NuGet for available WinUI tray icon packages:
```powershell
dotnet list package --outdated
# Or search: https://www.nuget.org/packages?q=winui+tray+icon
```

**Option 2**: Fallback to Win32 interop if H.NotifyIcon.WinUI is unavailable:
- Create custom wrapper using `Shell_NotifyIcon` Win32 APIs
- See `docs/winui3-architecture.md` section 5.3 for implementation details

### Missing Icon File

The tray icon currently uses a placeholder. The icon will be generated dynamically in Stage B when the `IconGeneratorService` is implemented.

## Troubleshooting

### "Cannot find Windows App SDK"

Ensure you have installed the Windows App SDK via Visual Studio Installer or standalone installer:
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

### "The name 'DispatcherQueue' does not exist"

Ensure your project targets `net8.0-windows10.0.19041.0` or later in the `.csproj` file.

### "Package H.NotifyIcon.WinUI could not be found"

See "Known Issues / Notes" above for alternatives.

### Build succeeds but app doesn't start

Check Event Viewer (Windows Logs → Application) for errors. Common issues:
- Missing Windows App SDK runtime (should be bundled with `WindowsAppSDKSelfContained=true`)
- Incompatible Windows version (requires Windows 10 1809+)

## Testing Stage A

Once built successfully:

1. **Run the application**
   - App should minimize to system tray (look in overflow area)
   - Main window should be hidden

2. **Left-click tray icon**
   - Flyout window should appear with "Stage A: Skeleton App" message
   - Press Escape to close

3. **Right-click tray icon**
   - Context menu should show "Start with Windows" and "Exit"
   - Click "Exit" to close app

4. **Expected behavior**:
   - ✅ Tray icon visible
   - ✅ Flyout opens/closes
   - ✅ Context menu works
   - ✅ No crashes

## Next Steps

After verifying Stage A works:

1. Continue to Stage B implementation (see `docs/winui3-migration-plan.md`)
2. Move audio services to Core project
3. Implement first microphone card with real-time meter

## Documentation

- **Migration Plan**: `docs/winui3-migration-plan.md`
- **Architecture**: `docs/winui3-architecture.md`
- **WPF→WinUI Mapping**: `docs/wpf-to-winui-mapping.md`
- **Reconnaissance**: `docs/winui3-migration-recon.md`

---

**Last Updated**: 2026-01-04
**Migration Agent**: Claude
