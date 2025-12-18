use crate::audio::MicrophoneDevice;
use windows::core::*;
use windows::Win32::Foundation::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::Win32::System::Registry::*;

// Menu command IDs
pub const CMD_EXIT: u32 = 1;
pub const CMD_TOGGLE_MUTE: u32 = 2;
pub const CMD_TOGGLE_STARTUP: u32 = 3;
pub const CMD_DEVICE_BASE: u32 = 100; // Devices start at 100

const APP_NAME: &str = "MicManager";
const STARTUP_KEY: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";

/// Show the context menu at the specified position
pub fn show_context_menu(hwnd: HWND, x: i32, y: i32, devices: &[MicrophoneDevice], is_startup: bool) {
    unsafe {
        let menu = CreatePopupMenu().unwrap();

        // Add device selection items
        for (i, device) in devices.iter().enumerate() {
            let label = if device.is_default {
                format!("✓ {}", device.name)
            } else {
                format!("   {}", device.name)
            };

            let label_wide: Vec<u16> = label.encode_utf16().chain(std::iter::once(0)).collect();
            let flags = if device.is_default {
                MF_STRING | MF_CHECKED
            } else {
                MF_STRING
            };

            let _ = AppendMenuW(menu, flags, (CMD_DEVICE_BASE + i as u32) as usize, PCWSTR(label_wide.as_ptr()));
        }

        // Separator
        let _ = AppendMenuW(menu, MF_SEPARATOR, 0, None);

        // Toggle mute option
        let mute_label = w!("Toggle Mute");
        let _ = AppendMenuW(menu, MF_STRING, CMD_TOGGLE_MUTE as usize, mute_label);

        // Separator
        let _ = AppendMenuW(menu, MF_SEPARATOR, 0, None);

        // Start with Windows option
        let startup_label = w!("Start with Windows");
        let startup_flags = if is_startup {
            MF_STRING | MF_CHECKED
        } else {
            MF_STRING
        };
        let _ = AppendMenuW(menu, startup_flags, CMD_TOGGLE_STARTUP as usize, startup_label);

        // Separator
        let _ = AppendMenuW(menu, MF_SEPARATOR, 0, None);

        // Exit option
        let exit_label = w!("Exit");
        let _ = AppendMenuW(menu, MF_STRING, CMD_EXIT as usize, exit_label);

        // Show the menu
        let _ = TrackPopupMenu(menu, TPM_RIGHTBUTTON, x, y, 0, hwnd, None);

        // Clean up
        let _ = DestroyMenu(menu);
    }
}

/// Check if the app is set to start with Windows
pub fn is_startup_enabled() -> bool {
    unsafe {
        let key_path: Vec<u16> = STARTUP_KEY.encode_utf16().chain(std::iter::once(0)).collect();
        let value_name: Vec<u16> = APP_NAME.encode_utf16().chain(std::iter::once(0)).collect();

        let mut key = HKEY::default();
        let result = RegOpenKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(key_path.as_ptr()),
            0,
            KEY_READ,
            &mut key,
        );

        if result.is_err() {
            return false;
        }

        let exists = RegQueryValueExW(
            key,
            PCWSTR(value_name.as_ptr()),
            None,
            None,
            None,
            None,
        ).is_ok();

        let _ = RegCloseKey(key);

        exists
    }
}

/// Toggle the startup registry entry
pub fn toggle_startup() {
    if is_startup_enabled() {
        disable_startup();
    } else {
        enable_startup();
    }
}

fn enable_startup() {
    unsafe {
        let key_path: Vec<u16> = STARTUP_KEY.encode_utf16().chain(std::iter::once(0)).collect();
        let value_name: Vec<u16> = APP_NAME.encode_utf16().chain(std::iter::once(0)).collect();

        let mut key = HKEY::default();
        let result = RegOpenKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(key_path.as_ptr()),
            0,
            KEY_WRITE,
            &mut key,
        );

        if result.is_err() {
            return;
        }

        // Get the path to the current executable
        let mut path_buf = [0u16; 260];
        let len = windows::Win32::System::LibraryLoader::GetModuleFileNameW(
            None,
            &mut path_buf,
        );

        if len > 0 {
            let path_bytes = &path_buf[..len as usize];
            let path_with_null: Vec<u16> = path_bytes.iter().copied().chain(std::iter::once(0)).collect();

            let _ = RegSetValueExW(
                key,
                PCWSTR(value_name.as_ptr()),
                0,
                REG_SZ,
                Some(std::slice::from_raw_parts(
                    path_with_null.as_ptr() as *const u8,
                    path_with_null.len() * 2,
                )),
            );
        }

        let _ = RegCloseKey(key);
    }
}

fn disable_startup() {
    unsafe {
        let key_path: Vec<u16> = STARTUP_KEY.encode_utf16().chain(std::iter::once(0)).collect();
        let value_name: Vec<u16> = APP_NAME.encode_utf16().chain(std::iter::once(0)).collect();

        let mut key = HKEY::default();
        let result = RegOpenKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(key_path.as_ptr()),
            0,
            KEY_WRITE,
            &mut key,
        );

        if result.is_err() {
            return;
        }

        let _ = RegDeleteValueW(key, PCWSTR(value_name.as_ptr()));
        let _ = RegCloseKey(key);
    }
}
