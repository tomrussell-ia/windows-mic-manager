//! Device enumeration using Windows MMDevice API.
//!
//! Provides COM initialization and device enumeration functionality.

use super::device::{AudioError, AudioFormat, DeviceRole, MicrophoneDevice};
use std::collections::HashMap;
use windows::core::PCWSTR;
use windows::Win32::Devices::Properties::DEVPKEY_Device_FriendlyName;
use windows::Win32::Media::Audio::{
    eCapture, eCommunications, eConsole, IAudioClient, IMMDevice, IMMDeviceEnumerator,
    MMDeviceEnumerator, DEVICE_STATE_ACTIVE, WAVEFORMATEX,
};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoTaskMemFree, CoUninitialize, CLSCTX_ALL,
    COINIT_APARTMENTTHREADED, STGM,
};
use windows::Win32::UI::Shell::PropertiesSystem::{IPropertyStore, PROPERTYKEY};

/// COM initialization guard that uninitializes COM on drop.
pub struct ComGuard {
    initialized: bool,
}

impl ComGuard {
    /// Initialize COM for the current thread.
    pub fn new() -> Result<Self, AudioError> {
        unsafe {
            // Use apartment-threaded for UI compatibility
            CoInitializeEx(None, COINIT_APARTMENTTHREADED)
                .ok()
                .map_err(AudioError::ComInitFailed)?;
        }
        Ok(Self { initialized: true })
    }
}

impl Drop for ComGuard {
    fn drop(&mut self) {
        if self.initialized {
            unsafe {
                CoUninitialize();
            }
        }
    }
}

/// Device enumerator using Windows MMDevice API.
pub struct DeviceEnumerator {
    enumerator: IMMDeviceEnumerator,
}

impl DeviceEnumerator {
    /// Create a new DeviceEnumerator.
    ///
    /// Note: COM must be initialized before calling this function.
    pub fn new() -> Result<Self, AudioError> {
        unsafe {
            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)
                    .map_err(AudioError::EnumerationFailed)?;

            Ok(Self { enumerator })
        }
    }

    /// Get all active microphone devices.
    pub fn get_devices(&self) -> Result<Vec<MicrophoneDevice>, AudioError> {
        unsafe {
            let collection = self
                .enumerator
                .EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE)
                .map_err(AudioError::EnumerationFailed)?;

            let count = collection
                .GetCount()
                .map_err(AudioError::EnumerationFailed)?;

            // Get default device IDs
            let default_console = self.get_default_device_id(DeviceRole::Console)?;
            let default_comm = self.get_default_device_id(DeviceRole::Communications)?;

            let mut devices = Vec::with_capacity(count as usize);

            for i in 0..count {
                let device = collection.Item(i).map_err(AudioError::EnumerationFailed)?;

                if let Ok(mic) = self.device_to_microphone(&device, &default_console, &default_comm)
                {
                    devices.push(mic);
                }
            }

            Ok(devices)
        }
    }

    /// Get a specific device by ID.
    pub fn get_device(&self, device_id: &str) -> Result<MicrophoneDevice, AudioError> {
        unsafe {
            let device_id_wide: Vec<u16> =
                device_id.encode_utf16().chain(std::iter::once(0)).collect();

            let device = self
                .enumerator
                .GetDevice(PCWSTR::from_raw(device_id_wide.as_ptr()))
                .map_err(|_| AudioError::DeviceNotFound {
                    device_id: device_id.to_string(),
                })?;

            let default_console = self.get_default_device_id(DeviceRole::Console)?;
            let default_comm = self.get_default_device_id(DeviceRole::Communications)?;

            self.device_to_microphone(&device, &default_console, &default_comm)
        }
    }

    /// Get the default device ID for a specific role.
    pub fn get_default_device_id(&self, role: DeviceRole) -> Result<Option<String>, AudioError> {
        unsafe {
            let erole = match role {
                DeviceRole::Console => eConsole,
                DeviceRole::Multimedia => windows::Win32::Media::Audio::eMultimedia,
                DeviceRole::Communications => eCommunications,
            };

            let device = match self.enumerator.GetDefaultAudioEndpoint(eCapture, erole) {
                Ok(d) => d,
                Err(_) => return Ok(None),
            };

            let id = device.GetId().map_err(AudioError::EnumerationFailed)?;
            let id_string = id
                .to_string()
                .map_err(|e| AudioError::StringConversion(e.to_string()))?;

            Ok(Some(id_string))
        }
    }

    /// Get devices as a HashMap keyed by device ID.
    pub fn get_devices_map(&self) -> Result<HashMap<String, MicrophoneDevice>, AudioError> {
        let devices = self.get_devices()?;
        Ok(devices.into_iter().map(|d| (d.id.clone(), d)).collect())
    }

    /// Convert an IMMDevice to a MicrophoneDevice.
    fn device_to_microphone(
        &self,
        device: &IMMDevice,
        default_console: &Option<String>,
        default_comm: &Option<String>,
    ) -> Result<MicrophoneDevice, AudioError> {
        unsafe {
            // Get device ID
            let id = device.GetId().map_err(AudioError::EnumerationFailed)?;
            let id_string = id
                .to_string()
                .map_err(|e| AudioError::StringConversion(e.to_string()))?;

            // Get device name from properties
            let props: IPropertyStore = device
                .OpenPropertyStore(STGM(0))
                .map_err(AudioError::EnumerationFailed)?;

            let name = self
                .get_device_name(&props)
                .unwrap_or_else(|| "Unknown".to_string());

            // Check if this is a default device
            let is_default = default_console
                .as_ref()
                .map(|d| d == &id_string)
                .unwrap_or(false);
            let is_default_communication = default_comm
                .as_ref()
                .map(|d| d == &id_string)
                .unwrap_or(false);

            // Get audio format
            let audio_format = self.get_audio_format(device);

            Ok(MicrophoneDevice {
                id: id_string,
                name,
                is_default,
                is_default_communication,
                is_muted: false,
                volume_level: 1.0,
                audio_format,
                input_level: 0.0,
                peak_hold: 0.0,
            })
        }
    }

    /// Get the friendly name of a device from its property store.
    fn get_device_name(&self, props: &IPropertyStore) -> Option<String> {
        unsafe {
            // Convert DEVPROPKEY to PROPERTYKEY
            let key = PROPERTYKEY {
                fmtid: DEVPKEY_Device_FriendlyName.fmtid,
                pid: DEVPKEY_Device_FriendlyName.pid,
            };

            let prop = match props.GetValue(&key) {
                Ok(p) => p,
                Err(_) => return None,
            };

            // Use the Display trait to get the string value
            let s = prop.to_string();
            if s.is_empty() {
                None
            } else {
                Some(s)
            }
        }
    }

    /// Get the raw IMMDeviceEnumerator for notification registration.
    pub fn raw_enumerator(&self) -> &IMMDeviceEnumerator {
        &self.enumerator
    }

    /// Get the audio format for a device.
    pub fn get_audio_format(&self, device: &IMMDevice) -> Option<AudioFormat> {
        unsafe {
            // Activate IAudioClient to get the mix format
            let audio_client: IAudioClient = match device.Activate(CLSCTX_ALL, None) {
                Ok(client) => client,
                Err(_) => return None,
            };

            // Get the mix format
            let format_ptr = match audio_client.GetMixFormat() {
                Ok(ptr) => ptr,
                Err(_) => return None,
            };

            if format_ptr.is_null() {
                return None;
            }

            // Read the format
            let format: &WAVEFORMATEX = &*format_ptr;
            let audio_format = AudioFormat {
                sample_rate: format.nSamplesPerSec,
                bit_depth: format.wBitsPerSample,
                channels: format.nChannels,
            };

            // Free the format memory
            CoTaskMemFree(Some(format_ptr as *const _));

            Some(audio_format)
        }
    }

    /// Get the audio format for a device by ID.
    pub fn get_audio_format_by_id(&self, device_id: &str) -> Option<AudioFormat> {
        unsafe {
            let device_id_wide: Vec<u16> =
                device_id.encode_utf16().chain(std::iter::once(0)).collect();

            let device = match self
                .enumerator
                .GetDevice(PCWSTR::from_raw(device_id_wide.as_ptr()))
            {
                Ok(d) => d,
                Err(_) => return None,
            };

            self.get_audio_format(&device)
        }
    }
}
