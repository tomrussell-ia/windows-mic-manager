use super::policy;
use crate::WM_DEVICE_CHANGED;
use windows::core::*;
use windows::Win32::Foundation::*;
use windows::Win32::Media::Audio::*;
use windows::Win32::Media::Audio::Endpoints::*;
use windows::Win32::System::Com::*;
use windows::Win32::UI::WindowsAndMessaging::PostMessageW;
use windows::Win32::UI::Shell::PropertiesSystem::PROPERTYKEY;

// Property key for device friendly name
const PKEY_DEVICE_FRIENDLY_NAME: PROPERTYKEY = PROPERTYKEY {
    fmtid: GUID::from_u128(0xa45c254e_df1c_4efd_8020_67d146a850e0),
    pid: 14,
};

/// Represents a microphone device
#[derive(Clone)]
pub struct MicrophoneDevice {
    pub id: String,
    pub name: String,
    pub is_default: bool,
    pub is_muted: bool,
}

/// Manages audio devices and provides methods to enumerate, control, and monitor them
pub struct AudioManager {
    enumerator: IMMDeviceEnumerator,
    #[allow(dead_code)]
    notification_client: IMMNotificationClient,
    #[allow(dead_code)]
    hwnd: HWND,
}

impl AudioManager {
    pub fn new(hwnd: HWND) -> Result<Self> {
        unsafe {
            let enumerator: IMMDeviceEnumerator = CoCreateInstance(
                &MMDeviceEnumerator,
                None,
                CLSCTX_ALL,
            )?;

            // Create and register notification client
            let notification_client: IMMNotificationClient = NotificationClient { hwnd }.into();
            enumerator.RegisterEndpointNotificationCallback(&notification_client)?;

            Ok(Self {
                enumerator,
                notification_client,
                hwnd,
            })
        }
    }

    /// Get all active microphone devices
    pub fn get_microphones(&self) -> Vec<MicrophoneDevice> {
        let mut devices = Vec::new();
        let default_id = self.get_default_device_id();

        unsafe {
            if let Ok(collection) = self.enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE) {
                if let Ok(count) = collection.GetCount() {
                    for i in 0..count {
                        if let Ok(device) = collection.Item(i) {
                            if let Some(mic) = self.device_to_microphone(&device, &default_id) {
                                devices.push(mic);
                            }
                        }
                    }
                }
            }
        }

        devices
    }

    fn device_to_microphone(&self, device: &IMMDevice, default_id: &Option<String>) -> Option<MicrophoneDevice> {
        unsafe {
            let id = device.GetId().ok()?;
            let id_string = id.to_string().ok()?;

            let name = self.get_device_name(device).unwrap_or_else(|| "Unknown".to_string());
            let is_default = default_id.as_ref().map_or(false, |d| d == &id_string);
            let is_muted = self.get_device_mute_state(device);

            Some(MicrophoneDevice {
                id: id_string,
                name,
                is_default,
                is_muted,
            })
        }
    }

    fn get_device_name(&self, device: &IMMDevice) -> Option<String> {
        unsafe {
            let store = device.OpenPropertyStore(STGM(0)).ok()?; // STGM_READ = 0
            let prop = store.GetValue(&PKEY_DEVICE_FRIENDLY_NAME as *const _).ok()?;

            // The PROPVARIANT for strings - try to convert to string
            let name = prop.to_string();
            if name.is_empty() {
                None
            } else {
                Some(name)
            }
        }
    }

    fn get_device_mute_state(&self, device: &IMMDevice) -> bool {
        unsafe {
            if let Ok(endpoint_volume) = device.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None) {
                endpoint_volume.GetMute().unwrap_or(BOOL(0)).as_bool()
            } else {
                false
            }
        }
    }

    /// Get the default capture device ID
    pub fn get_default_device_id(&self) -> Option<String> {
        unsafe {
            let device = self.enumerator.GetDefaultAudioEndpoint(eCapture, eConsole).ok()?;
            let id = device.GetId().ok()?;
            id.to_string().ok()
        }
    }

    /// Get the name of the default capture device
    pub fn get_default_device_name(&self) -> String {
        unsafe {
            if let Ok(device) = self.enumerator.GetDefaultAudioEndpoint(eCapture, eConsole) {
                self.get_device_name(&device).unwrap_or_else(|| "No microphone".to_string())
            } else {
                "No microphone".to_string()
            }
        }
    }

    /// Check if the default microphone is muted
    pub fn is_default_muted(&self) -> bool {
        unsafe {
            if let Ok(device) = self.enumerator.GetDefaultAudioEndpoint(eCapture, eConsole) {
                self.get_device_mute_state(&device)
            } else {
                false
            }
        }
    }

    /// Toggle mute on the default microphone
    pub fn toggle_default_mute(&self) -> Result<bool> {
        unsafe {
            let device = self.enumerator.GetDefaultAudioEndpoint(eCapture, eConsole)?;
            let endpoint_volume: IAudioEndpointVolume = device.Activate(CLSCTX_ALL, None)?;

            let current_mute = endpoint_volume.GetMute()?.as_bool();
            let new_mute = !current_mute;
            endpoint_volume.SetMute(new_mute, std::ptr::null())?;

            Ok(new_mute)
        }
    }

    /// Set a device as the default for all roles
    pub fn set_default_device(&self, device_id: &str) -> Result<()> {
        policy::set_default_device_for_all_roles(device_id)
    }
}

impl Drop for AudioManager {
    fn drop(&mut self) {
        unsafe {
            let _ = self.enumerator.UnregisterEndpointNotificationCallback(&self.notification_client);
        }
    }
}

/// COM notification client for device change events
#[windows::core::implement(IMMNotificationClient)]
struct NotificationClient {
    hwnd: HWND,
}

impl IMMNotificationClient_Impl for NotificationClient_Impl {
    fn OnDeviceStateChanged(&self, _pwstrdeviceid: &PCWSTR, _dwnewstate: DEVICE_STATE) -> Result<()> {
        self.notify_change();
        Ok(())
    }

    fn OnDeviceAdded(&self, _pwstrdeviceid: &PCWSTR) -> Result<()> {
        self.notify_change();
        Ok(())
    }

    fn OnDeviceRemoved(&self, _pwstrdeviceid: &PCWSTR) -> Result<()> {
        self.notify_change();
        Ok(())
    }

    fn OnDefaultDeviceChanged(&self, flow: EDataFlow, _role: ERole, _pwstrdefaultdeviceid: &PCWSTR) -> Result<()> {
        if flow == eCapture {
            self.notify_change();
        }
        Ok(())
    }

    fn OnPropertyValueChanged(&self, _pwstrdeviceid: &PCWSTR, _key: &PROPERTYKEY) -> Result<()> {
        Ok(())
    }
}

impl NotificationClient_Impl {
    fn notify_change(&self) {
        unsafe {
            let _ = PostMessageW(self.hwnd, WM_DEVICE_CHANGED, WPARAM(0), LPARAM(0));
        }
    }
}
