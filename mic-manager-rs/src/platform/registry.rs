//! Windows Registry preferences management.
//!
//! Manages user preferences persisted to Windows Registry.

use thiserror::Error;
use windows::core::PCWSTR;
use windows::Win32::System::Registry::{
    RegCloseKey, RegCreateKeyExW, RegDeleteValueW, RegOpenKeyExW, RegQueryValueExW, RegSetValueExW,
    HKEY, HKEY_CURRENT_USER, KEY_READ, KEY_WRITE, REG_CREATE_KEY_DISPOSITION, REG_DWORD,
    REG_OPTION_NON_VOLATILE, REG_SZ,
};

/// User preferences.
#[derive(Debug, Clone, Default)]
pub struct UserPreferences {
    /// Start application when Windows starts
    pub start_with_windows: bool,

    /// Remember window mode between sessions
    pub window_mode: WindowMode,
}

/// Window display mode.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum WindowMode {
    /// Window acts as a flyout - closes when clicking outside
    #[default]
    Flyout,

    /// Window is docked/persistent - stays open
    Docked,
}

impl WindowMode {
    fn to_dword(&self) -> u32 {
        match self {
            WindowMode::Flyout => 0,
            WindowMode::Docked => 1,
        }
    }

    fn from_dword(value: u32) -> Self {
        match value {
            1 => WindowMode::Docked,
            _ => WindowMode::Flyout,
        }
    }
}

/// Preferences service error types.
#[derive(Debug, Error)]
pub enum PreferencesError {
    #[error("Failed to access registry: {0}")]
    RegistryAccess(String),

    #[error("Failed to read preference: {key}")]
    ReadFailed { key: String },

    #[error("Failed to write preference: {key}")]
    WriteFailed { key: String },

    #[error("Invalid preference value for: {key}")]
    InvalidValue { key: String },
}

/// Registry-based preferences service.
pub struct RegistryPreferences {
    app_key_path: Vec<u16>,
    run_key_path: Vec<u16>,
    value_name: Vec<u16>,
}

impl RegistryPreferences {
    const APP_KEY: &'static str = r"Software\MicrophoneManager";
    const RUN_KEY: &'static str = r"Software\Microsoft\Windows\CurrentVersion\Run";
    const APP_NAME: &'static str = "MicrophoneManager";
    const WINDOW_MODE_VALUE: &'static str = "WindowMode";

    /// Create a new RegistryPreferences instance.
    pub fn new() -> Self {
        Self {
            app_key_path: Self::to_wide(Self::APP_KEY),
            run_key_path: Self::to_wide(Self::RUN_KEY),
            value_name: Self::to_wide(Self::APP_NAME),
        }
    }

    fn to_wide(s: &str) -> Vec<u16> {
        s.encode_utf16().chain(std::iter::once(0)).collect()
    }

    /// Load preferences from storage.
    pub fn load(&self) -> Result<UserPreferences, PreferencesError> {
        let window_mode = self.load_window_mode().unwrap_or_default();
        let start_with_windows = self.is_startup_enabled().unwrap_or(false);

        Ok(UserPreferences {
            start_with_windows,
            window_mode,
        })
    }

    /// Save preferences to storage.
    pub fn save(&self, preferences: &UserPreferences) -> Result<(), PreferencesError> {
        self.save_window_mode(preferences.window_mode)?;
        self.set_startup_enabled(preferences.start_with_windows)?;
        Ok(())
    }

    /// Load window mode from registry.
    fn load_window_mode(&self) -> Result<WindowMode, PreferencesError> {
        unsafe {
            let mut hkey = HKEY::default();
            let result = RegOpenKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR::from_raw(self.app_key_path.as_ptr()),
                0,
                KEY_READ,
                &mut hkey,
            );

            if result.is_err() {
                return Ok(WindowMode::default());
            }

            let value_name = Self::to_wide(Self::WINDOW_MODE_VALUE);
            let mut data: u32 = 0;
            let mut data_size = std::mem::size_of::<u32>() as u32;

            let result = RegQueryValueExW(
                hkey,
                PCWSTR::from_raw(value_name.as_ptr()),
                None,
                None,
                Some(&mut data as *mut u32 as *mut u8),
                Some(&mut data_size),
            );

            let _ = RegCloseKey(hkey);

            if result.is_ok() {
                Ok(WindowMode::from_dword(data))
            } else {
                Ok(WindowMode::default())
            }
        }
    }

    /// Save window mode to registry.
    fn save_window_mode(&self, mode: WindowMode) -> Result<(), PreferencesError> {
        unsafe {
            let mut hkey = HKEY::default();
            let mut disposition = REG_CREATE_KEY_DISPOSITION::default();

            let result = RegCreateKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR::from_raw(self.app_key_path.as_ptr()),
                0,
                PCWSTR::null(),
                REG_OPTION_NON_VOLATILE,
                KEY_WRITE,
                None,
                &mut hkey,
                Some(&mut disposition),
            );

            if result.is_err() {
                return Err(PreferencesError::WriteFailed {
                    key: Self::WINDOW_MODE_VALUE.to_string(),
                });
            }

            let value_name = Self::to_wide(Self::WINDOW_MODE_VALUE);
            let data = mode.to_dword();

            let result = RegSetValueExW(
                hkey,
                PCWSTR::from_raw(value_name.as_ptr()),
                0,
                REG_DWORD,
                Some(std::slice::from_raw_parts(
                    &data as *const u32 as *const u8,
                    std::mem::size_of::<u32>(),
                )),
            );

            let _ = RegCloseKey(hkey);

            if result.is_err() {
                Err(PreferencesError::WriteFailed {
                    key: Self::WINDOW_MODE_VALUE.to_string(),
                })
            } else {
                Ok(())
            }
        }
    }

    /// Check if "Start with Windows" is enabled.
    pub fn is_startup_enabled(&self) -> Result<bool, PreferencesError> {
        unsafe {
            let mut hkey = HKEY::default();
            let result = RegOpenKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR::from_raw(self.run_key_path.as_ptr()),
                0,
                KEY_READ,
                &mut hkey,
            );

            if result.is_err() {
                return Ok(false);
            }

            let mut data_size = 0u32;
            let result = RegQueryValueExW(
                hkey,
                PCWSTR::from_raw(self.value_name.as_ptr()),
                None,
                None,
                None,
                Some(&mut data_size),
            );

            let _ = RegCloseKey(hkey);

            Ok(result.is_ok() && data_size > 0)
        }
    }

    /// Enable or disable "Start with Windows".
    pub fn set_startup_enabled(&self, enabled: bool) -> Result<(), PreferencesError> {
        unsafe {
            let mut hkey = HKEY::default();
            let result = RegOpenKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR::from_raw(self.run_key_path.as_ptr()),
                0,
                KEY_WRITE,
                &mut hkey,
            );

            if result.is_err() {
                return Err(PreferencesError::RegistryAccess(
                    "Failed to open Run key".to_string(),
                ));
            }

            let result = if enabled {
                // Get executable path
                let exe_path =
                    std::env::current_exe().map_err(|_| PreferencesError::WriteFailed {
                        key: Self::APP_NAME.to_string(),
                    })?;
                let exe_path_str = exe_path.to_string_lossy();
                let exe_path_wide = Self::to_wide(&exe_path_str);

                RegSetValueExW(
                    hkey,
                    PCWSTR::from_raw(self.value_name.as_ptr()),
                    0,
                    REG_SZ,
                    Some(std::slice::from_raw_parts(
                        exe_path_wide.as_ptr() as *const u8,
                        exe_path_wide.len() * 2,
                    )),
                )
            } else {
                RegDeleteValueW(hkey, PCWSTR::from_raw(self.value_name.as_ptr()))
            };

            let _ = RegCloseKey(hkey);

            if result.is_err() && enabled {
                Err(PreferencesError::WriteFailed {
                    key: Self::APP_NAME.to_string(),
                })
            } else {
                Ok(())
            }
        }
    }
}

impl Default for RegistryPreferences {
    fn default() -> Self {
        Self::new()
    }
}
