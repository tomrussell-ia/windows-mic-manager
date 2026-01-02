//! Audio capture and level metering.
//!
//! Provides audio level metering using IAudioMeterInformation.

use super::device::AudioError;
use windows::Win32::Media::Audio::{Endpoints::IAudioMeterInformation, IMMDevice};
use windows::Win32::System::Com::CLSCTX_ALL;

/// Level meter for a specific device.
pub struct LevelMeter {
    meter_info: IAudioMeterInformation,
}

impl LevelMeter {
    /// Create a new LevelMeter for the given device.
    pub fn new(device: &IMMDevice) -> Result<Self, AudioError> {
        unsafe {
            let meter_info: IAudioMeterInformation = device
                .Activate(CLSCTX_ALL, None)
                .map_err(|_| AudioError::MeterNotAvailable)?;

            Ok(Self { meter_info })
        }
    }

    /// Get the current peak level (0.0 to 1.0).
    pub fn get_peak_level(&self) -> Result<f32, AudioError> {
        unsafe {
            let peak = self
                .meter_info
                .GetPeakValue()
                .map_err(AudioError::WindowsError)?;
            Ok(peak)
        }
    }

    /// Get peak values for all channels.
    pub fn get_channel_peaks(&self) -> Result<Vec<f32>, AudioError> {
        unsafe {
            let channel_count = self
                .meter_info
                .GetMeteringChannelCount()
                .map_err(AudioError::WindowsError)?;

            let mut peaks = vec![0.0f32; channel_count as usize];
            self.meter_info
                .GetChannelsPeakValues(&mut peaks)
                .map_err(AudioError::WindowsError)?;

            Ok(peaks)
        }
    }
}
