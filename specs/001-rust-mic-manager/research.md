# Research: Windows Microphone Manager (Rust Rebuild)

**Feature Branch**: `001-rust-mic-manager`
**Date**: 2026-01-01

## Research Summary

This document consolidates research findings for rebuilding the Windows Microphone Manager in Rust. Three key technical decisions were researched:

1. UI Framework selection
2. System Tray implementation
3. Windows Core Audio API integration

---

## Decision 1: UI Framework

### Decision: **egui + eframe** with **tray-icon** and **windows-rs** for native effects

### Rationale

1. **Meets performance requirements**: ~65MB memory (within 75MB constraint), ~3MB binary, fast startup
2. **Excellent for level meters**: Immediate mode is perfect for 20Hz+ refresh rate real-time visualization
3. **Reasonable learning curve**: Most approachable of the evaluated frameworks
4. **Active community**: Largest Rust GUI ecosystem with over 1 million daily downloads
5. **Custom widgets**: Built-in 2D graphics API (epaint) for drawing custom level meters and visualizations
6. **Mica/Acrylic support**: Can use windows-rs to call DwmSetWindowAttribute on the winit window handle

### Alternatives Considered

| Framework | Pros | Cons | Verdict |
|-----------|------|------|---------|
| **iced** | Elm-style architecture, good theming | Slow startup (500-600ms GPU init), 80-100MB memory | Rejected - exceeds performance constraints |
| **Slint** | Built-in Fluent styling, 20-50MB memory, native feel | Commercial license required, DSL learning curve | Alternative if licensing acceptable |
| **Native Win32** | Full control, smallest footprint (~15MB), fastest startup | High complexity, steep learning curve, verbose code | Reserved for native-only features |

### Implementation Notes

- Use `tray-icon` crate for system tray (provides position data for flyout)
- Use `winit` for window management
- Use `windows-rs` for Mica/Acrylic backdrop effects
- Custom widgets via `egui::Painter` for level meters

### Cargo Dependencies

```toml
[dependencies]
eframe = "0.29"
tray-icon = "0.17"
muda = "0.14"  # Re-exported by tray-icon for menus
windows = { version = "0.58", features = [
    "Win32_Graphics_Dwm",
    "Win32_UI_WindowsAndMessaging",
] }
```

---

## Decision 2: System Tray Implementation

### Decision: **tray-icon crate** (Tauri team) with winit integration

### Rationale

1. **Event data quality**: Provides both mouse position and icon rect in click events - essential for flyout positioning
2. **Dynamic icon updates**: Easy to change icons via `tray_icon.set_icon()` for mute state
3. **Built-in tooltip support**: `TrayIconBuilder::with_tooltip()` handles device name/mute state display
4. **Integrated menu system**: The `muda` crate provides context menus
5. **Active maintenance**: Part of Tauri ecosystem, benchmark score 85.8

### Alternatives Considered

| Approach | Pros | Cons | Verdict |
|----------|------|------|---------|
| **windows-rs direct** | Full control, maximum flexibility | High complexity, manual event handling | Not needed for this use case |
| **trayicon-rs** | Simpler API, provides icon coordinates | Smaller community, less documentation | tray-icon is more mature |

### Flyout Positioning Strategy

The `TrayIconEvent::Click` provides:
- `position: PhysicalPosition<f64>` - Mouse cursor coordinates
- `rect: Rect` - Icon bounding rectangle

```rust
TrayIconEvent::Click { rect, .. } => {
    let anchor_x = (rect.left + rect.right) / 2.0;
    let anchor_y = rect.top; // Or bottom depending on taskbar position
    // Position flyout window at (anchor_x, anchor_y)
}
```

For precise positioning, use Windows APIs:
- `Shell_NotifyIconGetRect` - Get exact icon rectangle
- `CalculatePopupWindowPosition` - Calculate optimal popup position
- `SHAppBarMessage(ABM_GETTASKBARPOS)` - Get taskbar location

### Hot-Plug Device Notifications

Use `IMMNotificationClient` (covered in Decision 3) for audio device changes. For USB device arrival/removal, register via `RegisterDeviceNotificationW` with `KSCATEGORY_AUDIO`.

---

## Decision 3: Windows Core Audio APIs (windows-rs)

### Decision: Use **windows-rs** crate for all audio APIs with **com-policy-config** for default device setting

### Rationale

1. **Official Microsoft bindings**: windows-rs is the official Rust binding for Windows APIs
2. **Full API coverage**: All required interfaces available (IMMDeviceEnumerator, IAudioEndpointVolume, IAudioCaptureClient)
3. **Active maintenance**: Regular updates aligned with Windows SDK
4. **Type safety**: Rust's type system prevents common COM errors

### Required Cargo Features

```toml
[dependencies]
windows = { version = "0.58", features = [
    "Win32_Media_Audio",
    "Win32_Media_Audio_Endpoints",
    "Win32_System_Com",
    "Win32_Foundation",
    "Win32_UI_Shell_PropertiesSystem",
    "Win32_Devices_Properties",
    "implement",
]}
com-policy-config = "0.1"  # For IPolicyConfig (undocumented API)
```

### API Mapping

| C# (NAudio) | Rust (windows-rs) |
|-------------|-------------------|
| `MMDeviceEnumerator` | `IMMDeviceEnumerator` via `CoCreateInstance` |
| `device.AudioEndpointVolume` | `device.Activate::<IAudioEndpointVolume>()` |
| `WasapiCapture` | `IAudioClient` + `IAudioCaptureClient` |
| `IMMNotificationClient` | `#[implement(IMMNotificationClient)]` trait |
| `PolicyConfigService` | `com-policy-config` crate or manual COM |

### Key Implementation Patterns

#### Device Enumeration
```rust
let enumerator: IMMDeviceEnumerator = CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)?;
let collection = enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE)?;
```

#### Volume Control
```rust
let volume: IAudioEndpointVolume = device.Activate(CLSCTX_ALL, None)?;
let level = volume.GetMasterVolumeLevelScalar()?;
volume.SetMasterVolumeLevelScalar(new_level, std::ptr::null())?;
```

#### Level Metering (Two Options)

**Option A: IAudioMeterInformation** (simpler, less CPU)
```rust
let meter: IAudioMeterInformation = device.Activate(CLSCTX_ALL, None)?;
let peak = meter.GetPeakValue()?; // 0.0 to 1.0
```

**Option B: IAudioCaptureClient** (full audio capture for custom analysis)
```rust
let audio_client: IAudioClient = device.Activate(CLSCTX_ALL, None)?;
audio_client.Initialize(AUDCLNT_SHAREMODE_SHARED, ...)?;
let capture_client: IAudioCaptureClient = audio_client.GetService()?;
audio_client.Start()?;
// Read buffers and calculate peak
```

#### Device Notifications
```rust
#[implement(IMMNotificationClient)]
pub struct DeviceNotificationClient { sender: Sender<DeviceEvent> }

impl IMMNotificationClient_Impl for DeviceNotificationClient_Impl {
    fn OnDefaultDeviceChanged(&self, flow: EDataFlow, role: ERole, id: &PCWSTR) -> Result<()> {
        // Handle default device change
    }
}

enumerator.RegisterEndpointNotificationCallback(&callback)?;
```

#### Set Default Device (IPolicyConfig)
```rust
use com_policy_config::PolicyConfigClient;

let policy: IPolicyConfig = CoCreateInstance(&PolicyConfigClient, None, CLSCTX_ALL)?;
policy.SetDefaultEndpoint(device_id, 0)?; // 0 = eConsole, 2 = eCommunications
```

### Known Issues / Gotchas

1. **COM Threading**: Initialize COM with `COINIT_MULTITHREADED` for background threads, `COINIT_APARTMENTTHREADED` for UI threads
2. **Callback thread safety**: Notifications arrive on system threads - use channels for cross-thread communication
3. **Memory management**: Free COM-allocated memory with `CoTaskMemFree`, clear `PROPVARIANT` with `PropVariantClear`
4. **IPolicyConfig**: Undocumented API - may break with Windows updates, test on target versions
5. **IAudioMeterInformation**: May return 0 in exclusive mode if no hardware meter exists
6. **Missing methods**: Ensure all required Cargo features are enabled (e.g., `Win32_Media_Audio_Endpoints` for `Activate`)

### Helper Crates to Consider

| Crate | Purpose | Notes |
|-------|---------|-------|
| `com-policy-config` | IPolicyConfig interface | Wraps undocumented API |
| `wasapi-rs` | High-level WASAPI wrapper | Alternative to raw windows-rs |

---

## Technology Stack Summary

| Component | Technology | Version |
|-----------|------------|---------|
| Language | Rust | 1.75+ stable |
| Windows API | windows-rs | 0.58 |
| UI Framework | egui + eframe | 0.29 |
| System Tray | tray-icon + muda | 0.17 / 0.14 |
| Audio API | Windows Core Audio | Via windows-rs |
| Default Device | com-policy-config | 0.1 |

---

## Sources

### UI Framework Research
- [egui GitHub Repository](https://github.com/emilk/egui)
- [iced Startup Performance Issue](https://github.com/iced-rs/iced/issues/615)
- [Slint Desktop-Ready Blog](https://slint.dev/blog/making-slint-desktop-ready)
- [2025 Survey of Rust GUI Libraries](https://www.boringcactus.com/2025/04/13/2025-survey-of-rust-gui-libraries.html)

### System Tray Research
- [tray-icon crate documentation](https://docs.rs/tray-icon/latest/tray_icon/)
- [Shell_NotifyIconGetRect](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyicongetrect)
- [CalculatePopupWindowPosition](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-calculatepopupwindowposition)

### Windows Audio API Research
- [windows-rs GitHub](https://github.com/microsoft/windows-rs)
- [Microsoft Learn - Core Audio APIs](https://learn.microsoft.com/en-us/windows/win32/coreaudio/core-audio-apis-in-windows-vista)
- [Microsoft Learn - Capturing a Stream](https://learn.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream)
- [com-policy-config crate](https://crates.io/crates/com-policy-config)
