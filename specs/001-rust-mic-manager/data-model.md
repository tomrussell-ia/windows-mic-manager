# Data Model: Windows Microphone Manager (Rust Rebuild)

**Feature Branch**: `001-rust-mic-manager`
**Date**: 2026-01-01

## Overview

This document defines the core data structures and their relationships for the Windows Microphone Manager application. The model is derived from the feature specification and existing C# proof-of-concept.

---

## Core Entities

### MicrophoneDevice

Represents a capture audio endpoint (microphone) in the system.

```rust
/// A microphone device with its current state
#[derive(Debug, Clone)]
pub struct MicrophoneDevice {
    /// Unique Windows device ID (opaque string from IMMDevice::GetId)
    pub id: String,

    /// Human-readable device name (from device properties)
    pub name: String,

    /// Whether this is the default device for Console role (games, system sounds)
    pub is_default: bool,

    /// Whether this is the default device for Communications role (Teams, Zoom)
    pub is_default_communication: bool,

    /// Current mute state
    pub is_muted: bool,

    /// Volume level as scalar (0.0 to 1.0)
    pub volume_level: f32,

    /// Audio format information (optional - may not be available for all devices)
    pub audio_format: Option<AudioFormat>,

    /// Real-time input level (0.0 to 1.0, updated at 20Hz+)
    /// This is transient state, not persisted
    pub input_level: f32,

    /// Peak hold value for the level meter (decays over time)
    pub peak_hold: f32,
}
```

**Validation Rules**:
- `id`: Non-empty, unique across all devices
- `name`: Non-empty string, max 256 characters
- `volume_level`: Must be in range [0.0, 1.0]
- `input_level`: Must be in range [0.0, 1.0]
- `peak_hold`: Must be in range [0.0, 1.0], >= `input_level`

**Derived Properties**:
```rust
impl MicrophoneDevice {
    /// True if device is either default (Console) or default communication
    pub fn is_selected(&self) -> bool {
        self.is_default || self.is_default_communication
    }

    /// Volume as percentage (0-100)
    pub fn volume_percent(&self) -> u8 {
        (self.volume_level * 100.0).round() as u8
    }

    /// Input level in dBFS (clamped to -60dB to 0dB)
    pub fn input_level_dbfs(&self) -> f64 {
        if self.input_level <= 0.0 {
            -60.0
        } else {
            (20.0 * (self.input_level as f64).log10()).max(-60.0).min(0.0)
        }
    }

    /// Input level as percentage for UI meter (maps -60dB..0dB to 0..100)
    pub fn input_level_percent(&self) -> f64 {
        (self.input_level_dbfs() + 60.0) / 60.0 * 100.0
    }
}
```

---

### AudioFormat

Audio format information for a device.

```rust
/// Audio format (sample rate, bit depth, channels)
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct AudioFormat {
    /// Sample rate in Hz (e.g., 44100, 48000, 96000)
    pub sample_rate: u32,

    /// Bits per sample (e.g., 16, 24, 32)
    pub bit_depth: u16,

    /// Number of audio channels (typically 1 for mono mic, 2 for stereo)
    pub channels: u16,
}
```

**Display Format**:
```rust
impl std::fmt::Display for AudioFormat {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let rate_khz = self.sample_rate as f64 / 1000.0;
        write!(f, "{:.1}kHz/{}-bit", rate_khz, self.bit_depth)
    }
}
```

**Examples**: "48kHz/24-bit", "44.1kHz/16-bit", "96kHz/32-bit"

---

### DeviceRole

The system-defined role for audio devices.

```rust
/// Audio device role (maps to Windows ERole enum)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(u32)]
pub enum DeviceRole {
    /// Used by games, system sounds, most general applications
    Console = 0,

    /// Used by music players, video players
    Multimedia = 1,

    /// Used by Teams, Zoom, Discord, and other VoIP applications
    Communications = 2,
}
```

**Notes**:
- The application primarily uses `Console` and `Communications` roles
- `Multimedia` is rarely used for microphones but included for completeness
- Maps directly to Windows Core Audio `ERole` enum

---

### DeviceEvent

Events emitted by the audio system for state changes.

```rust
/// Events from the Windows audio system
#[derive(Debug, Clone)]
pub enum DeviceEvent {
    /// A new audio device was connected
    DeviceAdded { device_id: String },

    /// An audio device was disconnected
    DeviceRemoved { device_id: String },

    /// Device state changed (active, disabled, not present, unplugged)
    DeviceStateChanged {
        device_id: String,
        new_state: DeviceState,
    },

    /// Default device changed for a specific role
    DefaultDeviceChanged {
        role: DeviceRole,
        device_id: Option<String>, // None if no default device
    },

    /// Volume or mute state changed on a device
    VolumeChanged {
        device_id: String,
        volume_level: f32,
        is_muted: bool,
    },

    /// Audio format changed on a device
    FormatChanged {
        device_id: String,
        format: AudioFormat,
    },
}
```

---

### DeviceState

State of an audio device.

```rust
/// Windows device state flags
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DeviceState {
    /// Device is active and available for use
    Active,

    /// Device is disabled in Windows Sound settings
    Disabled,

    /// Device is not present (driver issue)
    NotPresent,

    /// Device is unplugged (for pluggable devices)
    Unplugged,
}
```

---

## Application State

### AppState

Global application state.

```rust
/// Main application state
pub struct AppState {
    /// All currently available microphones
    pub devices: Vec<MicrophoneDevice>,

    /// ID of the current default device (Console role)
    pub default_device_id: Option<String>,

    /// ID of the current default communication device
    pub default_communication_device_id: Option<String>,

    /// Window mode (flyout vs docked)
    pub window_mode: WindowMode,

    /// User preferences
    pub preferences: UserPreferences,

    /// Error state for UI display
    pub error_message: Option<String>,
}
```

---

### WindowMode

Flyout window behavior mode.

```rust
/// Window display mode
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum WindowMode {
    /// Window acts as a flyout - closes when clicking outside
    #[default]
    Flyout,

    /// Window is docked/persistent - stays open
    Docked,
}
```

---

### UserPreferences

User-configurable settings.

```rust
/// Persisted user preferences
#[derive(Debug, Clone)]
pub struct UserPreferences {
    /// Start application when Windows starts
    pub start_with_windows: bool,

    /// Remember window mode between sessions
    pub window_mode: WindowMode,
}
```

**Storage**: Windows Registry at `HKEY_CURRENT_USER\Software\MicrophoneManager`

---

## State Transitions

### Mute State Machine

```
                ┌──────────┐
    ToggleMute  │          │  ToggleMute
       ┌───────►│  Muted   │◄───────┐
       │        │          │        │
       │        └──────────┘        │
       │                            │
┌──────┴─────┐              ┌───────┴────┐
│            │              │            │
│  Unmuted   │◄────────────►│  Unmuted   │
│  (Default) │  External    │ (Changed)  │
└────────────┘   Change     └────────────┘
```

### Window Mode State Machine

```
┌───────────────┐                    ┌───────────────┐
│               │    Dock Button     │               │
│    Flyout     │───────────────────►│    Docked     │
│  (Default)    │                    │               │
│               │◄───────────────────│               │
└───────────────┘   Undock Button    └───────────────┘
       │                                    │
       │ Click Outside                      │ Close Button
       ▼                                    ▼
┌───────────────┐                    ┌───────────────┐
│    Hidden     │                    │    Hidden     │
│  (Tray Only)  │                    │  (Tray Only)  │
└───────────────┘                    └───────────────┘
```

---

## Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                        AppState                             │
├─────────────────────────────────────────────────────────────┤
│ devices: Vec<MicrophoneDevice>  ────────┐                   │
│ default_device_id: Option<String>       │                   │
│ default_communication_device_id         │                   │
│ window_mode: WindowMode                 │                   │
│ preferences: UserPreferences            │                   │
│ error_message: Option<String>           │                   │
└─────────────────────────────────────────┼───────────────────┘
                                          │
                                          │ 1..*
                                          ▼
┌─────────────────────────────────────────────────────────────┐
│                    MicrophoneDevice                         │
├─────────────────────────────────────────────────────────────┤
│ id: String (PK)                                             │
│ name: String                                                │
│ is_default: bool           ────────► DeviceRole::Console    │
│ is_default_communication   ────────► DeviceRole::Comm.      │
│ is_muted: bool                                              │
│ volume_level: f32                                           │
│ audio_format: Option<AudioFormat>  ───┐                     │
│ input_level: f32                      │                     │
│ peak_hold: f32                        │                     │
└───────────────────────────────────────┼─────────────────────┘
                                        │ 0..1
                                        ▼
┌─────────────────────────────────────────────────────────────┐
│                      AudioFormat                            │
├─────────────────────────────────────────────────────────────┤
│ sample_rate: u32                                            │
│ bit_depth: u16                                              │
│ channels: u16                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Event Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Windows Audio  │     │    Audio        │     │      App        │
│    System       │────►│   Service       │────►│     State       │
│                 │     │                 │     │                 │
│ - Hot-plug      │     │ - Translates    │     │ - Updates UI    │
│ - Volume change │     │   Win32 events  │     │ - Refreshes     │
│ - Default change│     │ - Polls levels  │     │   device list   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                      │                      │
         │  IMMNotification     │  DeviceEvent         │  UI Update
         │  Client callbacks    │  (channel)           │  (egui)
         ▼                      ▼                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Event Types                              │
│  DeviceAdded | DeviceRemoved | DefaultDeviceChanged |           │
│  VolumeChanged | FormatChanged | DeviceStateChanged             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Notes

1. **Transient vs Persisted State**: `input_level` and `peak_hold` are transient (updated in real-time, not saved). `UserPreferences` is persisted to the registry.

2. **Thread Safety**: Audio events arrive on system threads. Use channels or `Arc<Mutex<>>` for cross-thread state updates.

3. **Device ID Stability**: Windows device IDs are stable across sessions for the same physical device. They can be used as keys for caching or user preferences.

4. **Graceful Degradation**: If `AudioFormat` cannot be retrieved, set to `None` and don't display the format tag.
