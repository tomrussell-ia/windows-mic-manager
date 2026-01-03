# Audio Service Contract

**Module**: `mic_manager::audio`
**Purpose**: Provides access to Windows Core Audio APIs for device enumeration, control, and monitoring.

---

## Service Trait

```rust
/// Audio device management service
pub trait AudioService: Send + Sync {
    /// Get all active microphone devices
    fn get_devices(&self) -> Result<Vec<MicrophoneDevice>, AudioError>;

    /// Get a specific device by ID
    fn get_device(&self, device_id: &str) -> Result<MicrophoneDevice, AudioError>;

    /// Get the default device ID for a role
    fn get_default_device_id(&self, role: DeviceRole) -> Result<Option<String>, AudioError>;

    /// Set a device as default for a specific role
    fn set_default_device(&self, device_id: &str, role: DeviceRole) -> Result<(), AudioError>;

    /// Set a device as default for all roles (Console + Communications)
    fn set_default_device_all_roles(&self, device_id: &str) -> Result<(), AudioError>;

    /// Get volume level for a device (0.0 to 1.0)
    fn get_volume(&self, device_id: &str) -> Result<f32, AudioError>;

    /// Set volume level for a device (0.0 to 1.0)
    fn set_volume(&self, device_id: &str, level: f32) -> Result<(), AudioError>;

    /// Get mute state for a device
    fn get_mute(&self, device_id: &str) -> Result<bool, AudioError>;

    /// Set mute state for a device
    fn set_mute(&self, device_id: &str, muted: bool) -> Result<(), AudioError>;

    /// Toggle mute state for a device, returns new state
    fn toggle_mute(&self, device_id: &str) -> Result<bool, AudioError>;

    /// Get current peak input level for a device (0.0 to 1.0)
    fn get_peak_level(&self, device_id: &str) -> Result<f32, AudioError>;

    /// Get audio format for a device
    fn get_audio_format(&self, device_id: &str) -> Result<Option<AudioFormat>, AudioError>;

    /// Subscribe to device events (hot-plug, default change, volume change)
    fn subscribe(&self) -> Receiver<DeviceEvent>;
}
```

---

## Error Types

```rust
/// Audio service error types
#[derive(Debug, thiserror::Error)]
pub enum AudioError {
    #[error("Device not found: {device_id}")]
    DeviceNotFound { device_id: String },

    #[error("No default device available")]
    NoDefaultDevice,

    #[error("COM initialization failed: {0}")]
    ComInitFailed(#[source] windows::core::Error),

    #[error("Failed to enumerate devices: {0}")]
    EnumerationFailed(#[source] windows::core::Error),

    #[error("Failed to set default device: {0}")]
    SetDefaultFailed(#[source] windows::core::Error),

    #[error("Volume control not available for device")]
    VolumeNotAvailable,

    #[error("Level meter not available for device")]
    MeterNotAvailable,

    #[error("Windows API error: {0}")]
    WindowsError(#[source] windows::core::Error),
}
```

---

## Event Channel

```rust
use std::sync::mpsc::Receiver;

/// Device events emitted by the audio service
/// See data-model.md for DeviceEvent definition
pub type DeviceEventReceiver = Receiver<DeviceEvent>;
```

---

## Usage Contract

### Initialization
```rust
// Create service (initializes COM, creates enumerator)
let audio_service = WindowsAudioService::new()?;

// Subscribe to events BEFORE any operations
let events = audio_service.subscribe();

// Start event processing loop in background
std::thread::spawn(move || {
    while let Ok(event) = events.recv() {
        // Handle event
    }
});
```

### Thread Safety
- `AudioService` is `Send + Sync` and can be shared across threads
- All methods are thread-safe
- Events are delivered on a background thread - use appropriate synchronization

### Error Handling
- All operations return `Result<T, AudioError>`
- Device operations may fail if device is disconnected mid-operation
- Always handle `DeviceNotFound` gracefully - devices can disappear

### Performance Requirements
- `get_devices()`: < 100ms
- `get_peak_level()`: < 10ms (called at 20Hz+)
- `set_volume()`: < 100ms with immediate effect
- Event delivery: < 1 second from system change

---

## Implementation Notes

1. **COM Threading**: Service must initialize COM on construction and use appropriate apartment model
2. **Caching**: Device list can be cached and invalidated on `DeviceAdded`/`DeviceRemoved` events
3. **Level Polling**: Use `IAudioMeterInformation::GetPeakValue()` for efficiency, fallback to capture if needed
4. **Default Device**: Use `com-policy-config` crate for `IPolicyConfig` interface
