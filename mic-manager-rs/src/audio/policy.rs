//! IPolicyConfig interface for setting default audio devices.
//!
//! Uses raw COM calls to the undocumented IPolicyConfig interface.

use super::device::{AudioError, DeviceRole};
use std::ffi::c_void;
use windows::core::{GUID, HRESULT, PCWSTR};
use windows::Win32::Media::Audio::{eCommunications, eConsole, eMultimedia, ERole};
use windows::Win32::System::Com::CLSCTX_ALL;

// CPolicyConfigClient class CLSID
const CPOLICYCONFIGCLIENT_CLSID: GUID = GUID::from_u128(0x870af99c_171d_4f9e_af0d_e63df40c2bc9);

// IPolicyConfig interface IID
const IPOLICYCONFIG_IID: GUID = GUID::from_u128(0xf8679f50_850a_41cf_9c72_430f290290c8);

// External declaration for CoCreateInstance
#[link(name = "ole32")]
extern "system" {
    fn CoCreateInstance(
        rclsid: *const GUID,
        punk_outer: *mut c_void,
        dw_cls_context: u32,
        riid: *const GUID,
        ppv: *mut *mut c_void,
    ) -> HRESULT;
}

/// IPolicyConfig vtable (partial - only what we need)
#[repr(C)]
struct IPolicyConfigVtbl {
    // IUnknown methods
    query_interface:
        unsafe extern "system" fn(*mut c_void, *const GUID, *mut *mut c_void) -> HRESULT,
    add_ref: unsafe extern "system" fn(*mut c_void) -> u32,
    release: unsafe extern "system" fn(*mut c_void) -> u32,
    // IPolicyConfig methods (indices 3-11 are other methods we don't need)
    _reserved: [*const c_void; 8],
    // SetDefaultEndpoint is at vtable index 11
    set_default_endpoint: unsafe extern "system" fn(*mut c_void, PCWSTR, ERole) -> HRESULT,
}

/// Raw IPolicyConfig COM object
#[repr(C)]
struct IPolicyConfigRaw {
    vtbl: *const IPolicyConfigVtbl,
}

/// Policy config wrapper for setting default devices.
pub struct PolicyConfig {
    ptr: *mut IPolicyConfigRaw,
}

impl PolicyConfig {
    /// Create a new PolicyConfig instance.
    ///
    /// Note: COM must be initialized before calling this function.
    pub fn new() -> Result<Self, AudioError> {
        unsafe {
            let mut ptr: *mut c_void = std::ptr::null_mut();

            // Create the PolicyConfigClient COM object
            let hr = CoCreateInstance(
                &CPOLICYCONFIGCLIENT_CLSID,
                std::ptr::null_mut(),
                CLSCTX_ALL.0 as u32,
                &IPOLICYCONFIG_IID,
                &mut ptr,
            );

            if hr.is_err() {
                return Err(AudioError::StringConversion(format!(
                    "CoCreateInstance failed: {:?}",
                    hr
                )));
            }

            if ptr.is_null() {
                return Err(AudioError::StringConversion(
                    "CoCreateInstance returned null".to_string(),
                ));
            }

            Ok(Self {
                ptr: ptr as *mut IPolicyConfigRaw,
            })
        }
    }

    /// Set a device as the default for a specific role.
    pub fn set_default_device(&self, device_id: &str, role: DeviceRole) -> Result<(), AudioError> {
        let device_id_wide: Vec<u16> = device_id.encode_utf16().chain(std::iter::once(0)).collect();

        let erole: ERole = match role {
            DeviceRole::Console => eConsole,
            DeviceRole::Multimedia => eMultimedia,
            DeviceRole::Communications => eCommunications,
        };

        unsafe {
            let vtbl = (*self.ptr).vtbl;
            let pcwstr = PCWSTR::from_raw(device_id_wide.as_ptr());

            let hr = ((*vtbl).set_default_endpoint)(self.ptr as *mut c_void, pcwstr, erole);

            if hr.is_err() {
                return Err(AudioError::StringConversion(format!(
                    "SetDefaultEndpoint failed: {:?}",
                    hr
                )));
            }
        }

        Ok(())
    }

    /// Set a device as the default for all roles (Console and Communications).
    pub fn set_default_device_all_roles(&self, device_id: &str) -> Result<(), AudioError> {
        self.set_default_device(device_id, DeviceRole::Console)?;
        self.set_default_device(device_id, DeviceRole::Communications)?;
        Ok(())
    }
}

impl Drop for PolicyConfig {
    fn drop(&mut self) {
        if !self.ptr.is_null() {
            unsafe {
                let vtbl = (*self.ptr).vtbl;
                ((*vtbl).release)(self.ptr as *mut c_void);
            }
        }
    }
}

// Note: PolicyConfig is not thread-safe (COM STA) - do not share between threads
