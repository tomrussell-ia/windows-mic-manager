//! Device change notifications using IMMNotificationClient.
//!
//! Implements callbacks for device state changes, default device changes,
//! and volume/mute changes.

use super::device::{DeviceEvent, DeviceRole, DeviceState};
use std::sync::mpsc::Sender;
use windows::core::{implement, PCWSTR};
use windows::Win32::Media::Audio::{
    eCapture, eCommunications, eConsole, EDataFlow, ERole, IMMDeviceEnumerator,
    IMMNotificationClient, IMMNotificationClient_Impl, DEVICE_STATE,
};
// Re-export windows_core so the implement macro can find it
#[allow(unused_imports)]
use windows_core;

/// Notification client that sends events to a channel.
#[implement(IMMNotificationClient)]
pub struct DeviceNotificationClient {
    sender: Sender<DeviceEvent>,
}

impl DeviceNotificationClient {
    /// Create a new notification client.
    pub fn new(sender: Sender<DeviceEvent>) -> Self {
        Self { sender }
    }

    /// Register this notification client with an enumerator.
    /// Takes ownership of self because the COM interface needs to own the data.
    pub fn register(
        self,
        enumerator: &IMMDeviceEnumerator,
    ) -> Result<IMMNotificationClient, windows::core::Error> {
        unsafe {
            let client: IMMNotificationClient = self.into();
            enumerator.RegisterEndpointNotificationCallback(&client)?;
            Ok(client)
        }
    }

    fn convert_role(role: ERole) -> DeviceRole {
        if role == eConsole {
            DeviceRole::Console
        } else if role == eCommunications {
            DeviceRole::Communications
        } else {
            DeviceRole::Multimedia
        }
    }

    fn convert_state(state: DEVICE_STATE) -> DeviceState {
        match state.0 {
            1 => DeviceState::Active,
            2 => DeviceState::Disabled,
            4 => DeviceState::NotPresent,
            8 => DeviceState::Unplugged,
            _ => DeviceState::NotPresent,
        }
    }
}

impl IMMNotificationClient_Impl for DeviceNotificationClient_Impl {
    fn OnDeviceStateChanged(
        &self,
        pwstrdeviceid: &PCWSTR,
        dwnewstate: DEVICE_STATE,
    ) -> windows::core::Result<()> {
        unsafe {
            if let Ok(id) = pwstrdeviceid.to_string() {
                let _ = self.sender.send(DeviceEvent::DeviceStateChanged {
                    device_id: id,
                    new_state: DeviceNotificationClient::convert_state(dwnewstate),
                });
            }
        }
        Ok(())
    }

    fn OnDeviceAdded(&self, pwstrdeviceid: &PCWSTR) -> windows::core::Result<()> {
        unsafe {
            if let Ok(id) = pwstrdeviceid.to_string() {
                let _ = self.sender.send(DeviceEvent::DeviceAdded { device_id: id });
            }
        }
        Ok(())
    }

    fn OnDeviceRemoved(&self, pwstrdeviceid: &PCWSTR) -> windows::core::Result<()> {
        unsafe {
            if let Ok(id) = pwstrdeviceid.to_string() {
                let _ = self
                    .sender
                    .send(DeviceEvent::DeviceRemoved { device_id: id });
            }
        }
        Ok(())
    }

    fn OnDefaultDeviceChanged(
        &self,
        flow: EDataFlow,
        role: ERole,
        pwstrdefaultdeviceid: &PCWSTR,
    ) -> windows::core::Result<()> {
        // Only care about capture devices
        if flow != eCapture {
            return Ok(());
        }

        unsafe {
            let device_id = if pwstrdefaultdeviceid.is_null() {
                None
            } else {
                pwstrdefaultdeviceid.to_string().ok()
            };

            let _ = self.sender.send(DeviceEvent::DefaultDeviceChanged {
                role: DeviceNotificationClient::convert_role(role),
                device_id,
            });
        }
        Ok(())
    }

    fn OnPropertyValueChanged(
        &self,
        _pwstrdeviceid: &PCWSTR,
        _key: &windows::Win32::UI::Shell::PropertiesSystem::PROPERTYKEY,
    ) -> windows::core::Result<()> {
        // Could be used to detect format changes, but we'll handle this differently
        Ok(())
    }
}

/// Creates an event channel and returns the sender.
pub fn create_event_channel() -> (Sender<DeviceEvent>, std::sync::mpsc::Receiver<DeviceEvent>) {
    std::sync::mpsc::channel()
}

// Note: VolumeNotificationClient removed for now due to AUDIO_VOLUME_NOTIFICATION_DATA
// type compatibility issues. Volume changes will be polled instead.
