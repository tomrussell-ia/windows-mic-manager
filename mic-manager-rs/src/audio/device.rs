//! Audio device data models.
//!
//! Defines the core data structures for representing microphone devices,
//! their state, audio format, and related events.

use thiserror::Error;

/// A microphone device with its current state.
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

impl MicrophoneDevice {
    /// Create a new MicrophoneDevice with default values.
    pub fn new(id: String, name: String) -> Self {
        Self {
            id,
            name,
            is_default: false,
            is_default_communication: false,
            is_muted: false,
            volume_level: 1.0,
            audio_format: None,
            input_level: 0.0,
            peak_hold: 0.0,
        }
    }

    /// True if device is either default (Console) or default communication.
    pub fn is_selected(&self) -> bool {
        self.is_default || self.is_default_communication
    }

    /// Volume as percentage (0-100).
    pub fn volume_percent(&self) -> u8 {
        (self.volume_level * 100.0).round() as u8
    }

    /// Input level in dBFS (clamped to -60dB to 0dB).
    pub fn input_level_dbfs(&self) -> f64 {
        if self.input_level <= 0.0 {
            -60.0
        } else {
            (20.0 * (self.input_level as f64).log10())
                .max(-60.0)
                .min(0.0)
        }
    }

    /// Input level as percentage for UI meter (maps -60dB..0dB to 0..100).
    pub fn input_level_percent(&self) -> f64 {
        (self.input_level_dbfs() + 60.0) / 60.0 * 100.0
    }
}

/// Audio format (sample rate, bit depth, channels).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct AudioFormat {
    /// Sample rate in Hz (e.g., 44100, 48000, 96000)
    pub sample_rate: u32,

    /// Bits per sample (e.g., 16, 24, 32)
    pub bit_depth: u16,

    /// Number of audio channels (typically 1 for mono mic, 2 for stereo)
    pub channels: u16,
}

impl std::fmt::Display for AudioFormat {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let rate_khz = self.sample_rate as f64 / 1000.0;
        if rate_khz.fract() == 0.0 {
            write!(f, "{}kHz/{}-bit", rate_khz as u32, self.bit_depth)
        } else {
            write!(f, "{:.1}kHz/{}-bit", rate_khz, self.bit_depth)
        }
    }
}

/// Audio device role (maps to Windows ERole enum).
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

/// Windows device state flags.
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

/// Events from the Windows audio system.
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

/// Audio service error types.
#[derive(Debug, Error)]
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

    #[error("String conversion error: {0}")]
    StringConversion(String),
}
