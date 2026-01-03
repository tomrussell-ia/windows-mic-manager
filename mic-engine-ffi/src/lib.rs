//! FFI bindings for Windows Microphone Manager.
//!
//! This crate provides C ABI functions for use from C# via P/Invoke.
//! All functions use panic::catch_unwind to prevent Rust panics from
//! unwinding across the FFI boundary.

use mic_manager_rs::{
    AudioError, DeviceEnumerator, DeviceRole, MicrophoneDevice,
    PolicyConfig, VolumeController,
};
use serde::{Deserialize, Serialize};
use std::cell::RefCell;
use std::ffi::{c_char, c_void, CStr, CString};
use std::panic;
use std::ptr;
use windows::core::PCWSTR;
use windows::Win32::System::Com::{CoInitializeEx, CoUninitialize, COINIT_APARTMENTTHREADED};

// ============================================================================
// Error Handling
// ============================================================================

/// Error codes returned by FFI functions.
#[repr(i32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ErrorCode {
    Success = 0,
    InvalidHandle = -1,
    InvalidArgument = -2,
    DeviceNotFound = -3,
    ComError = -4,
    JsonError = -5,
    VolumeNotAvailable = -6,
    Panic = -99,
}

impl From<AudioError> for ErrorCode {
    fn from(err: AudioError) -> Self {
        match err {
            AudioError::DeviceNotFound { .. } => ErrorCode::DeviceNotFound,
            AudioError::ComInitFailed(_) => ErrorCode::ComError,
            AudioError::EnumerationFailed(_) => ErrorCode::ComError,
            AudioError::SetDefaultFailed(_) => ErrorCode::ComError,
            AudioError::VolumeNotAvailable => ErrorCode::VolumeNotAvailable,
            AudioError::WindowsError(_) => ErrorCode::ComError,
            _ => ErrorCode::ComError,
        }
    }
}

/// Thread-local storage for the last error.
thread_local! {
    static LAST_ERROR: RefCell<Option<(ErrorCode, String)>> = const { RefCell::new(None) };
}

fn set_last_error(code: ErrorCode, message: impl Into<String>) {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = Some((code, message.into()));
    });
}

fn clear_last_error() {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = None;
    });
}

// ============================================================================
// Data Types for JSON Serialization
// ============================================================================

/// Configuration for engine creation (currently unused, reserved for future).
#[derive(Debug, Serialize, Deserialize)]
pub struct EngineConfig {
    #[serde(default)]
    pub log_level: Option<String>,
}

/// Audio format information.
#[derive(Debug, Serialize, Deserialize)]
pub struct AudioFormatDto {
    pub sample_rate: u32,
    pub bit_depth: u16,
    pub channels: u16,
}

/// A microphone device with its current state.
#[derive(Debug, Serialize, Deserialize)]
pub struct MicrophoneDeviceDto {
    pub id: String,
    pub name: String,
    pub is_default: bool,
    pub is_default_communication: bool,
    pub is_muted: bool,
    pub volume_level: f32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub audio_format: Option<AudioFormatDto>,
}

impl From<MicrophoneDevice> for MicrophoneDeviceDto {
    fn from(device: MicrophoneDevice) -> Self {
        Self {
            id: device.id,
            name: device.name,
            is_default: device.is_default,
            is_default_communication: device.is_default_communication,
            is_muted: device.is_muted,
            volume_level: device.volume_level,
            audio_format: device.audio_format.map(|f| AudioFormatDto {
                sample_rate: f.sample_rate,
                bit_depth: f.bit_depth,
                channels: f.channels,
            }),
        }
    }
}

/// Response containing a list of devices.
#[derive(Debug, Serialize, Deserialize)]
pub struct DeviceListResponse {
    pub devices: Vec<MicrophoneDeviceDto>,
}

/// Response containing a single device.
#[derive(Debug, Serialize, Deserialize)]
pub struct DeviceResponse {
    pub device: MicrophoneDeviceDto,
}

/// Response containing operation result.
#[derive(Debug, Serialize, Deserialize)]
pub struct OperationResult {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub is_muted: Option<bool>,
}

// ============================================================================
// Engine Handle Type
// ============================================================================

/// Opaque handle to the mic engine. Actually points to a MicEngine struct.
pub type MicEngineHandle = *mut c_void;

/// Internal engine state.
struct MicEngine {
    // No persistent state needed - we create COM objects per-call
    // This is safer for cross-thread usage
    _marker: std::marker::PhantomData<()>,
}

impl MicEngine {
    fn new() -> Self {
        Self {
            _marker: std::marker::PhantomData,
        }
    }
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Allocate a C string from a Rust string. Caller must free with mic_engine_free_string.
fn alloc_c_string(s: &str) -> *mut c_char {
    match CString::new(s) {
        Ok(cs) => cs.into_raw(),
        Err(_) => {
            // String contained a null byte, replace with empty
            CString::new("").unwrap().into_raw()
        }
    }
}

/// Parse a C string to a Rust string slice.
unsafe fn parse_c_str<'a>(ptr: *const c_char) -> Option<&'a str> {
    if ptr.is_null() {
        return None;
    }
    CStr::from_ptr(ptr).to_str().ok()
}

/// Execute a closure with COM initialized for the current thread.
/// Returns None if COM initialization fails.
fn with_com<T, F: FnOnce() -> Result<T, AudioError>>(f: F) -> Result<T, AudioError> {
    unsafe {
        // Initialize COM for this thread
        CoInitializeEx(None, COINIT_APARTMENTTHREADED)
            .ok()
            .map_err(AudioError::ComInitFailed)?;
    }

    let result = f();

    unsafe {
        CoUninitialize();
    }

    result
}

/// Get an IMMDevice by ID for volume operations.
fn get_device_for_volume(device_id: &str) -> Result<windows::Win32::Media::Audio::IMMDevice, AudioError> {
    use windows::Win32::Media::Audio::{eCapture, IMMDeviceEnumerator, MMDeviceEnumerator};
    use windows::Win32::System::Com::{CoCreateInstance, CLSCTX_ALL};

    unsafe {
        let enumerator: IMMDeviceEnumerator =
            CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)
                .map_err(AudioError::EnumerationFailed)?;

        let device_id_wide: Vec<u16> = device_id.encode_utf16().chain(std::iter::once(0)).collect();
        let device = enumerator
            .GetDevice(PCWSTR::from_raw(device_id_wide.as_ptr()))
            .map_err(|_| AudioError::DeviceNotFound {
                device_id: device_id.to_string(),
            })?;

        Ok(device)
    }
}

// ============================================================================
// FFI Functions - Lifecycle
// ============================================================================

/// Create a new mic engine instance.
///
/// # Arguments
/// * `config_json` - JSON configuration string (can be null for defaults)
///
/// # Returns
/// Handle to the engine, or null on failure. Check mic_engine_last_error_code() on failure.
///
/// # Safety
/// The returned handle must be freed with mic_engine_destroy().
#[no_mangle]
pub extern "C" fn mic_engine_create(config_json: *const c_char) -> MicEngineHandle {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        // Parse config if provided (currently ignored)
        if !config_json.is_null() {
            unsafe {
                if let Some(json_str) = parse_c_str(config_json) {
                    let _config: EngineConfig = serde_json::from_str(json_str).unwrap_or(EngineConfig {
                        log_level: None,
                    });
                }
            }
        }

        let engine = Box::new(MicEngine::new());
        Box::into_raw(engine) as MicEngineHandle
    });

    match result {
        Ok(handle) => handle,
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during engine creation");
            ptr::null_mut()
        }
    }
}

/// Destroy a mic engine instance.
///
/// # Safety
/// The handle must have been created by mic_engine_create() and must not be used after this call.
#[no_mangle]
pub extern "C" fn mic_engine_destroy(handle: MicEngineHandle) {
    if handle.is_null() {
        return;
    }

    let _ = panic::catch_unwind(|| {
        unsafe {
            let _ = Box::from_raw(handle as *mut MicEngine);
        }
    });
}

// ============================================================================
// FFI Functions - Device Operations
// ============================================================================

/// Get all microphone devices.
///
/// # Arguments
/// * `handle` - Engine handle (currently unused but reserved for future state)
///
/// # Returns
/// JSON string containing the device list. Caller must free with mic_engine_free_string().
/// Returns null on failure.
#[no_mangle]
pub extern "C" fn mic_engine_get_devices(_handle: MicEngineHandle) -> *mut c_char {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        with_com(|| {
            let enumerator = DeviceEnumerator::new()?;
            let mut devices = enumerator.get_devices()?;

            // Populate volume and mute state for each device
            for device in &mut devices {
                if let Ok(mm_device) = get_device_for_volume(&device.id) {
                    if let Ok(volume_ctrl) = VolumeController::new(&mm_device) {
                        device.volume_level = volume_ctrl.get_volume().unwrap_or(1.0);
                        device.is_muted = volume_ctrl.get_mute().unwrap_or(false);
                    }
                }
            }

            let response = DeviceListResponse {
                devices: devices.into_iter().map(Into::into).collect(),
            };

            serde_json::to_string(&response).map_err(|e| {
                AudioError::StringConversion(e.to_string())
            })
        })
    });

    match result {
        Ok(Ok(json)) => alloc_c_string(&json),
        Ok(Err(e)) => {
            set_last_error(ErrorCode::from(e.clone()), e.to_string());
            ptr::null_mut()
        }
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during device enumeration");
            ptr::null_mut()
        }
    }
}

/// Get a specific microphone device by ID.
///
/// # Arguments
/// * `handle` - Engine handle
/// * `device_id` - The device ID (UTF-8 string)
///
/// # Returns
/// JSON string containing the device. Caller must free with mic_engine_free_string().
/// Returns null on failure.
#[no_mangle]
pub extern "C" fn mic_engine_get_device(
    _handle: MicEngineHandle,
    device_id: *const c_char,
) -> *mut c_char {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        let device_id_str = unsafe {
            match parse_c_str(device_id) {
                Some(s) => s,
                None => {
                    return Err(AudioError::StringConversion("Invalid device ID".to_string()));
                }
            }
        };

        with_com(|| {
            let enumerator = DeviceEnumerator::new()?;
            let mut device = enumerator.get_device(device_id_str)?;

            // Populate volume and mute state
            if let Ok(mm_device) = get_device_for_volume(&device.id) {
                if let Ok(volume_ctrl) = VolumeController::new(&mm_device) {
                    device.volume_level = volume_ctrl.get_volume().unwrap_or(1.0);
                    device.is_muted = volume_ctrl.get_mute().unwrap_or(false);
                }
            }

            let response = DeviceResponse {
                device: device.into(),
            };

            serde_json::to_string(&response).map_err(|e| {
                AudioError::StringConversion(e.to_string())
            })
        })
    });

    match result {
        Ok(Ok(json)) => alloc_c_string(&json),
        Ok(Err(e)) => {
            set_last_error(ErrorCode::from(e.clone()), e.to_string());
            ptr::null_mut()
        }
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during device get");
            ptr::null_mut()
        }
    }
}

/// Set a device as the default for a specific role.
///
/// # Arguments
/// * `handle` - Engine handle
/// * `device_id` - The device ID (UTF-8 string)
/// * `role` - 0 = Console (default), 1 = Multimedia, 2 = Communications
///
/// # Returns
/// 0 on success, negative error code on failure.
#[no_mangle]
pub extern "C" fn mic_engine_set_default_device(
    _handle: MicEngineHandle,
    device_id: *const c_char,
    role: u32,
) -> i32 {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        let device_id_str = unsafe {
            match parse_c_str(device_id) {
                Some(s) => s,
                None => {
                    set_last_error(ErrorCode::InvalidArgument, "Invalid device ID");
                    return ErrorCode::InvalidArgument as i32;
                }
            }
        };

        let device_role = match role {
            0 => DeviceRole::Console,
            1 => DeviceRole::Multimedia,
            2 => DeviceRole::Communications,
            _ => {
                set_last_error(ErrorCode::InvalidArgument, "Invalid role");
                return ErrorCode::InvalidArgument as i32;
            }
        };

        match with_com(|| {
            let policy = PolicyConfig::new()?;
            policy.set_default_device(device_id_str, device_role)?;
            Ok(())
        }) {
            Ok(()) => ErrorCode::Success as i32,
            Err(e) => {
                let code = ErrorCode::from(e.clone());
                set_last_error(code, e.to_string());
                code as i32
            }
        }
    });

    match result {
        Ok(code) => code,
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during set default device");
            ErrorCode::Panic as i32
        }
    }
}

/// Set the volume level for a device.
///
/// # Arguments
/// * `handle` - Engine handle
/// * `device_id` - The device ID (UTF-8 string)
/// * `volume` - Volume level (0.0 to 1.0)
///
/// # Returns
/// 0 on success, negative error code on failure.
#[no_mangle]
pub extern "C" fn mic_engine_set_volume(
    _handle: MicEngineHandle,
    device_id: *const c_char,
    volume: f32,
) -> i32 {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        let device_id_str = unsafe {
            match parse_c_str(device_id) {
                Some(s) => s,
                None => {
                    set_last_error(ErrorCode::InvalidArgument, "Invalid device ID");
                    return ErrorCode::InvalidArgument as i32;
                }
            }
        };

        match with_com(|| {
            let mm_device = get_device_for_volume(device_id_str)?;
            let volume_ctrl = VolumeController::new(&mm_device)?;
            volume_ctrl.set_volume(volume)?;
            Ok(())
        }) {
            Ok(()) => ErrorCode::Success as i32,
            Err(e) => {
                let code = ErrorCode::from(e.clone());
                set_last_error(code, e.to_string());
                code as i32
            }
        }
    });

    match result {
        Ok(code) => code,
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during set volume");
            ErrorCode::Panic as i32
        }
    }
}

/// Toggle the mute state of a device.
///
/// # Arguments
/// * `handle` - Engine handle
/// * `device_id` - The device ID (UTF-8 string)
///
/// # Returns
/// JSON string with the result (includes new mute state). Caller must free with mic_engine_free_string().
/// Returns null on failure.
#[no_mangle]
pub extern "C" fn mic_engine_toggle_mute(
    _handle: MicEngineHandle,
    device_id: *const c_char,
) -> *mut c_char {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        let device_id_str = unsafe {
            match parse_c_str(device_id) {
                Some(s) => s,
                None => {
                    return Err(AudioError::StringConversion("Invalid device ID".to_string()));
                }
            }
        };

        with_com(|| {
            let mm_device = get_device_for_volume(device_id_str)?;
            let volume_ctrl = VolumeController::new(&mm_device)?;
            let new_mute_state = volume_ctrl.toggle_mute()?;

            let response = OperationResult {
                success: true,
                error: None,
                is_muted: Some(new_mute_state),
            };

            serde_json::to_string(&response).map_err(|e| {
                AudioError::StringConversion(e.to_string())
            })
        })
    });

    match result {
        Ok(Ok(json)) => alloc_c_string(&json),
        Ok(Err(e)) => {
            set_last_error(ErrorCode::from(e.clone()), e.to_string());
            ptr::null_mut()
        }
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during toggle mute");
            ptr::null_mut()
        }
    }
}

/// Set the mute state of a device.
///
/// # Arguments
/// * `handle` - Engine handle
/// * `device_id` - The device ID (UTF-8 string)
/// * `muted` - 1 = muted, 0 = unmuted
///
/// # Returns
/// 0 on success, negative error code on failure.
#[no_mangle]
pub extern "C" fn mic_engine_set_mute(
    _handle: MicEngineHandle,
    device_id: *const c_char,
    muted: i32,
) -> i32 {
    clear_last_error();

    let result = panic::catch_unwind(|| {
        let device_id_str = unsafe {
            match parse_c_str(device_id) {
                Some(s) => s,
                None => {
                    set_last_error(ErrorCode::InvalidArgument, "Invalid device ID");
                    return ErrorCode::InvalidArgument as i32;
                }
            }
        };

        match with_com(|| {
            let mm_device = get_device_for_volume(device_id_str)?;
            let volume_ctrl = VolumeController::new(&mm_device)?;
            volume_ctrl.set_mute(muted != 0)?;
            Ok(())
        }) {
            Ok(()) => ErrorCode::Success as i32,
            Err(e) => {
                let code = ErrorCode::from(e.clone());
                set_last_error(code, e.to_string());
                code as i32
            }
        }
    });

    match result {
        Ok(code) => code,
        Err(_) => {
            set_last_error(ErrorCode::Panic, "Panic during set mute");
            ErrorCode::Panic as i32
        }
    }
}

// ============================================================================
// FFI Functions - Memory Management
// ============================================================================

/// Free a string allocated by this library.
///
/// # Safety
/// The pointer must have been returned by one of the mic_engine_* functions.
/// Do not call this on strings from other sources.
#[no_mangle]
pub extern "C" fn mic_engine_free_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }

    let _ = panic::catch_unwind(|| {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    });
}

// ============================================================================
// FFI Functions - Error Handling
// ============================================================================

/// Get the last error code.
///
/// # Returns
/// The error code from the last failed operation, or 0 if no error.
#[no_mangle]
pub extern "C" fn mic_engine_last_error_code() -> i32 {
    LAST_ERROR.with(|e| {
        e.borrow()
            .as_ref()
            .map(|(code, _)| *code as i32)
            .unwrap_or(0)
    })
}

/// Get the last error message.
///
/// # Returns
/// Error message string. Caller must free with mic_engine_free_string().
/// Returns null if no error.
#[no_mangle]
pub extern "C" fn mic_engine_last_error_message() -> *mut c_char {
    LAST_ERROR.with(|e| {
        e.borrow()
            .as_ref()
            .map(|(_, msg)| alloc_c_string(msg))
            .unwrap_or(ptr::null_mut())
    })
}

// ============================================================================
// FFI Functions - Utility
// ============================================================================

/// Get the library version.
///
/// # Returns
/// Version string. Caller must free with mic_engine_free_string().
#[no_mangle]
pub extern "C" fn mic_engine_version() -> *mut c_char {
    alloc_c_string(env!("CARGO_PKG_VERSION"))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_error_code_conversion() {
        assert_eq!(
            ErrorCode::from(AudioError::DeviceNotFound {
                device_id: "test".to_string()
            }),
            ErrorCode::DeviceNotFound
        );
    }

    #[test]
    fn test_engine_lifecycle() {
        let handle = mic_engine_create(ptr::null());
        assert!(!handle.is_null());
        mic_engine_destroy(handle);
    }

    #[test]
    fn test_version() {
        let version = mic_engine_version();
        assert!(!version.is_null());
        unsafe {
            let s = CStr::from_ptr(version).to_str().unwrap();
            assert!(!s.is_empty());
        }
        mic_engine_free_string(version);
    }
}
