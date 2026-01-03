//! Application state and lifecycle management.
//!
//! Contains the main AppState struct and application lifecycle logic.

use crate::audio::{AudioError, DeviceEnumerator, DeviceEvent, MicrophoneDevice};
use crate::platform::{RegistryPreferences, UserPreferences, WindowMode};
use crate::ui::{TrayEvent, TrayManager};
use std::time::{Duration, Instant};

/// Main application state.
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

    /// Whether the flyout window is visible
    pub flyout_visible: bool,

    /// Last time level meters were updated
    pub last_level_update: Instant,

    /// Whether the application should exit
    pub should_exit: bool,
}

impl AppState {
    /// Create a new AppState with default values.
    pub fn new() -> Self {
        Self {
            devices: Vec::new(),
            default_device_id: None,
            default_communication_device_id: None,
            window_mode: WindowMode::Flyout,
            preferences: UserPreferences::default(),
            error_message: None,
            flyout_visible: false,
            last_level_update: Instant::now(),
            should_exit: false,
        }
    }

    /// Initialize the application state from system.
    pub fn initialize(&mut self, enumerator: &DeviceEnumerator) -> Result<(), AudioError> {
        // Load user preferences
        let prefs = RegistryPreferences::new();
        self.preferences = prefs.load().unwrap_or_default();
        self.window_mode = self.preferences.window_mode;

        // Get devices
        self.refresh_devices(enumerator)?;

        Ok(())
    }

    /// Refresh the device list from the system.
    pub fn refresh_devices(&mut self, enumerator: &DeviceEnumerator) -> Result<(), AudioError> {
        self.devices = enumerator.get_devices()?;

        // Update default device IDs
        self.default_device_id =
            enumerator.get_default_device_id(crate::audio::DeviceRole::Console)?;
        self.default_communication_device_id =
            enumerator.get_default_device_id(crate::audio::DeviceRole::Communications)?;

        // Update device flags
        for device in &mut self.devices {
            device.is_default = self
                .default_device_id
                .as_ref()
                .map(|id| id == &device.id)
                .unwrap_or(false);
            device.is_default_communication = self
                .default_communication_device_id
                .as_ref()
                .map(|id| id == &device.id)
                .unwrap_or(false);
        }

        Ok(())
    }

    /// Get the default device (if any).
    pub fn get_default_device(&self) -> Option<&MicrophoneDevice> {
        self.default_device_id
            .as_ref()
            .and_then(|id| self.devices.iter().find(|d| &d.id == id))
    }

    /// Check if the default device is muted.
    pub fn is_default_muted(&self) -> bool {
        self.get_default_device()
            .map(|d| d.is_muted)
            .unwrap_or(false)
    }

    /// Get the tooltip text for the tray icon.
    pub fn get_tooltip(&self) -> String {
        match self.get_default_device() {
            Some(device) => {
                if device.is_muted {
                    format!("{} (Muted)", device.name)
                } else {
                    device.name.clone()
                }
            }
            None => "No microphone".to_string(),
        }
    }

    /// Toggle the flyout visibility.
    pub fn toggle_flyout(&mut self) {
        self.flyout_visible = !self.flyout_visible;
    }

    /// Show the flyout.
    pub fn show_flyout(&mut self) {
        self.flyout_visible = true;
    }

    /// Hide the flyout.
    pub fn hide_flyout(&mut self) {
        self.flyout_visible = false;
    }

    /// Update a device's mute state.
    pub fn update_device_mute(&mut self, device_id: &str, muted: bool) {
        if let Some(device) = self.devices.iter_mut().find(|d| d.id == device_id) {
            device.is_muted = muted;
        }
    }

    /// Update a device's volume level.
    pub fn update_device_volume(&mut self, device_id: &str, volume: f32) {
        if let Some(device) = self.devices.iter_mut().find(|d| d.id == device_id) {
            device.volume_level = volume.clamp(0.0, 1.0);
        }
    }

    /// Update a device's input level (for level meters).
    pub fn update_device_level(&mut self, device_id: &str, level: f32) {
        if let Some(device) = self.devices.iter_mut().find(|d| d.id == device_id) {
            device.input_level = level.clamp(0.0, 1.0);

            // Update peak hold
            if level > device.peak_hold {
                device.peak_hold = level;
            }
        }
    }

    /// Decay peak hold values over time.
    pub fn decay_peak_holds(&mut self, decay_rate: f32) {
        for device in &mut self.devices {
            if device.peak_hold > device.input_level {
                device.peak_hold = (device.peak_hold - decay_rate).max(device.input_level);
            }
        }
    }

    /// Check if it's time to update level meters.
    pub fn should_update_levels(&self) -> bool {
        self.last_level_update.elapsed() >= Duration::from_millis(50) // 20Hz
    }

    /// Mark that levels were just updated.
    pub fn mark_levels_updated(&mut self) {
        self.last_level_update = Instant::now();
    }

    /// Handle a tray event.
    pub fn handle_tray_event(&mut self, event: TrayEvent, tray: &mut TrayManager) {
        match event {
            TrayEvent::LeftClick { .. } => {
                self.toggle_flyout();
            }
            TrayEvent::MenuItemClicked { id } => match id {
                crate::ui::MenuItemId::Exit => {
                    self.should_exit = true;
                }
                crate::ui::MenuItemId::StartWithWindows => {
                    self.preferences.start_with_windows = !self.preferences.start_with_windows;
                    let prefs = RegistryPreferences::new();
                    let _ = prefs.set_startup_enabled(self.preferences.start_with_windows);
                    let _ = tray.set_startup_checked(self.preferences.start_with_windows);
                }
            },
            _ => {}
        }
    }

    /// Handle a device event from the audio system.
    pub fn handle_device_event(
        &mut self,
        event: DeviceEvent,
        enumerator: &DeviceEnumerator,
        tray: &mut TrayManager,
    ) {
        match event {
            DeviceEvent::DeviceAdded { .. } | DeviceEvent::DeviceRemoved { .. } => {
                let _ = self.refresh_devices(enumerator);
                let _ = tray.set_tooltip(&self.get_tooltip());
                let _ = tray.set_icon(self.is_default_muted());
            }
            DeviceEvent::DefaultDeviceChanged { role, device_id } => {
                match role {
                    crate::audio::DeviceRole::Console => {
                        self.default_device_id = device_id;
                    }
                    crate::audio::DeviceRole::Communications => {
                        self.default_communication_device_id = device_id;
                    }
                    _ => {}
                }
                let _ = self.refresh_devices(enumerator);
                let _ = tray.set_tooltip(&self.get_tooltip());
                let _ = tray.set_icon(self.is_default_muted());
            }
            DeviceEvent::VolumeChanged {
                device_id,
                volume_level,
                is_muted,
            } => {
                self.update_device_volume(&device_id, volume_level);
                self.update_device_mute(&device_id, is_muted);

                // Update tray if this is the default device
                if Some(&device_id) == self.default_device_id.as_ref() {
                    let _ = tray.set_icon(is_muted);
                    let _ = tray.set_tooltip(&self.get_tooltip());
                }
            }
            DeviceEvent::FormatChanged { device_id, format } => {
                if let Some(device) = self.devices.iter_mut().find(|d| d.id == device_id) {
                    device.audio_format = Some(format);
                }
            }
            DeviceEvent::DeviceStateChanged { .. } => {
                let _ = self.refresh_devices(enumerator);
            }
        }
    }
}

impl Default for AppState {
    fn default() -> Self {
        Self::new()
    }
}
