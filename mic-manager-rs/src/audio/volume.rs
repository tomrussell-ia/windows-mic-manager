//! Volume control using IAudioEndpointVolume.
//!
//! Provides volume and mute control for audio devices.

use super::device::AudioError;
use windows::Win32::Media::Audio::{Endpoints::IAudioEndpointVolume, IMMDevice};
use windows::Win32::System::Com::CLSCTX_ALL;

/// Volume controller for a specific device.
pub struct VolumeController {
    endpoint_volume: IAudioEndpointVolume,
}

impl VolumeController {
    /// Create a new VolumeController for the given device.
    pub fn new(device: &IMMDevice) -> Result<Self, AudioError> {
        unsafe {
            let endpoint_volume: IAudioEndpointVolume = device
                .Activate(CLSCTX_ALL, None)
                .map_err(|_| AudioError::VolumeNotAvailable)?;

            Ok(Self { endpoint_volume })
        }
    }

    /// Get the current mute state.
    pub fn get_mute(&self) -> Result<bool, AudioError> {
        unsafe {
            let muted = self
                .endpoint_volume
                .GetMute()
                .map_err(AudioError::WindowsError)?;
            Ok(muted.as_bool())
        }
    }

    /// Set the mute state.
    pub fn set_mute(&self, muted: bool) -> Result<(), AudioError> {
        unsafe {
            self.endpoint_volume
                .SetMute(muted, std::ptr::null())
                .map_err(AudioError::WindowsError)?;
            Ok(())
        }
    }

    /// Toggle the mute state. Returns the new state.
    pub fn toggle_mute(&self) -> Result<bool, AudioError> {
        let current = self.get_mute()?;
        let new_state = !current;
        self.set_mute(new_state)?;
        Ok(new_state)
    }

    /// Get the current volume level (0.0 to 1.0).
    pub fn get_volume(&self) -> Result<f32, AudioError> {
        unsafe {
            let level = self
                .endpoint_volume
                .GetMasterVolumeLevelScalar()
                .map_err(AudioError::WindowsError)?;
            Ok(level)
        }
    }

    /// Set the volume level (0.0 to 1.0).
    pub fn set_volume(&self, level: f32) -> Result<(), AudioError> {
        let level = level.clamp(0.0, 1.0);
        unsafe {
            self.endpoint_volume
                .SetMasterVolumeLevelScalar(level, std::ptr::null())
                .map_err(AudioError::WindowsError)?;
            Ok(())
        }
    }

    /// Get the raw IAudioEndpointVolume interface for notification registration.
    pub fn raw_endpoint_volume(&self) -> &IAudioEndpointVolume {
        &self.endpoint_volume
    }
}
