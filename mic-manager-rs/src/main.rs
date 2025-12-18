#![windows_subsystem = "windows"]

mod audio;
mod tray;
mod ui;

use std::cell::RefCell;
use std::rc::Rc;
use windows::core::*;
use windows::Win32::Foundation::*;
use windows::Win32::System::Com::*;
use windows::Win32::UI::WindowsAndMessaging::*;

pub const WM_TRAY_ICON: u32 = WM_USER + 1;
pub const WM_DEVICE_CHANGED: u32 = WM_USER + 2;

fn show_error(msg: &str) {
    unsafe {
        let msg_wide: Vec<u16> = msg.encode_utf16().chain(std::iter::once(0)).collect();
        let title_wide: Vec<u16> = "Mic Manager Error".encode_utf16().chain(std::iter::once(0)).collect();
        MessageBoxW(None, PCWSTR(msg_wide.as_ptr()), PCWSTR(title_wide.as_ptr()), MB_OK | MB_ICONERROR);
    }
}

fn main() -> Result<()> {
    unsafe {
        // Initialize COM
        if let Err(e) = CoInitializeEx(None, COINIT_APARTMENTTHREADED).ok() {
            show_error(&format!("COM init failed: {:?}", e));
            return Err(e);
        }

        // Create hidden message window
        let instance = windows::Win32::System::LibraryLoader::GetModuleHandleW(None)?;

        let window_class = w!("MicManagerWindow");
        let wc = WNDCLASSEXW {
            cbSize: std::mem::size_of::<WNDCLASSEXW>() as u32,
            style: CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc: Some(window_proc),
            hInstance: instance.into(),
            lpszClassName: window_class,
            ..Default::default()
        };

        RegisterClassExW(&wc);

        let hwnd = CreateWindowExW(
            WINDOW_EX_STYLE::default(),
            window_class,
            w!("Mic Manager"),
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            None,
            None,
            instance,
            None,
        )?;

        // Initialize app state
        let app_state = match AppState::new(hwnd) {
            Ok(state) => Rc::new(RefCell::new(state)),
            Err(e) => {
                show_error(&format!("App init failed: {:?}", e));
                return Err(e);
            }
        };
        APP_STATE.with(|state| *state.borrow_mut() = Some(app_state));

        // Message loop
        let mut msg = MSG::default();
        while GetMessageW(&mut msg, None, 0, 0).into() {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        CoUninitialize();
    }

    Ok(())
}

struct AppState {
    hwnd: HWND,
    audio_manager: audio::AudioManager,
    tray_icon: tray::TrayIcon,
}

impl AppState {
    fn new(hwnd: HWND) -> Result<Self> {
        let audio_manager = audio::AudioManager::new(hwnd)?;
        let is_muted = audio_manager.is_default_muted();
        let default_name = audio_manager.get_default_device_name();
        let tray_icon = tray::TrayIcon::new(hwnd, is_muted, &default_name)?;

        Ok(Self {
            hwnd,
            audio_manager,
            tray_icon,
        })
    }

    fn update_tray(&mut self) {
        let is_muted = self.audio_manager.is_default_muted();
        let default_name = self.audio_manager.get_default_device_name();
        let _ = self.tray_icon.update(is_muted, &default_name);
    }

    fn toggle_mute(&mut self) {
        let _ = self.audio_manager.toggle_default_mute();
        self.update_tray();
    }

    fn show_menu(&self, x: i32, y: i32) {
        let devices = self.audio_manager.get_microphones();
        let is_startup = ui::menu::is_startup_enabled();
        ui::menu::show_context_menu(self.hwnd, x, y, &devices, is_startup);
    }

    fn set_default_device(&mut self, device_id: &str) {
        let _ = self.audio_manager.set_default_device(device_id);
        self.update_tray();
    }
}

thread_local! {
    static APP_STATE: RefCell<Option<Rc<RefCell<AppState>>>> = const { RefCell::new(None) };
}

fn with_app_state<F, R>(f: F) -> Option<R>
where
    F: FnOnce(&mut AppState) -> R,
{
    APP_STATE.with(|state| {
        state.borrow().as_ref().map(|app| f(&mut app.borrow_mut()))
    })
}

unsafe extern "system" fn window_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    match msg {
        WM_TRAY_ICON => {
            let event = (lparam.0 & 0xFFFF) as u32;
            match event {
                WM_LBUTTONUP => {
                    // Left click - toggle mute
                    with_app_state(|app| app.toggle_mute());
                }
                WM_RBUTTONUP => {
                    // Right click - show context menu
                    let mut pt = POINT::default();
                    let _ = GetCursorPos(&mut pt);
                    let _ = SetForegroundWindow(hwnd);
                    with_app_state(|app| app.show_menu(pt.x, pt.y));
                }
                _ => {}
            }
            LRESULT(0)
        }
        WM_DEVICE_CHANGED => {
            with_app_state(|app| app.update_tray());
            LRESULT(0)
        }
        WM_COMMAND => {
            let cmd_id = (wparam.0 & 0xFFFF) as u32;
            handle_menu_command(cmd_id);
            LRESULT(0)
        }
        WM_DESTROY => {
            with_app_state(|app| app.tray_icon.remove());
            PostQuitMessage(0);
            LRESULT(0)
        }
        _ => DefWindowProcW(hwnd, msg, wparam, lparam),
    }
}

fn handle_menu_command(cmd_id: u32) {
    match cmd_id {
        ui::menu::CMD_EXIT => unsafe {
            with_app_state(|app| {
                let _ = DestroyWindow(app.hwnd);
            });
        },
        ui::menu::CMD_TOGGLE_MUTE => {
            with_app_state(|app| app.toggle_mute());
        }
        ui::menu::CMD_TOGGLE_STARTUP => {
            ui::menu::toggle_startup();
        }
        id if id >= ui::menu::CMD_DEVICE_BASE => {
            // Device selection - ID encodes device index
            let device_index = (id - ui::menu::CMD_DEVICE_BASE) as usize;
            with_app_state(|app| {
                let devices = app.audio_manager.get_microphones();
                if let Some(device) = devices.get(device_index) {
                    app.set_default_device(&device.id);
                }
            });
        }
        _ => {}
    }
}
