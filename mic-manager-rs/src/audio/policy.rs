use windows::core::*;
use windows::Win32::System::Com::*;

/// Device role for audio endpoints
#[repr(u32)]
#[allow(dead_code)]
pub enum ERole {
    Console = 0,        // Games, system sounds, voice commands
    Multimedia = 1,     // Music, movies
    Communications = 2, // Voice chat, VoIP
}

/// IPolicyConfig COM interface (undocumented but stable)
/// Used to set the default audio device
#[windows::core::interface("F8679F50-850A-41CF-9C72-430F290290C8")]
pub unsafe trait IPolicyConfig: IUnknown {
    // Reserved methods to maintain vtable order
    fn reserved1(&self) -> HRESULT;
    fn reserved2(&self) -> HRESULT;
    fn reserved3(&self) -> HRESULT;
    fn reserved4(&self) -> HRESULT;
    fn reserved5(&self) -> HRESULT;
    fn reserved6(&self) -> HRESULT;
    fn reserved7(&self) -> HRESULT;
    fn reserved8(&self) -> HRESULT;
    fn reserved9(&self) -> HRESULT;
    fn reserved10(&self) -> HRESULT;

    fn SetDefaultEndpoint(&self, device_id: PCWSTR, role: u32) -> HRESULT;
}

// PolicyConfigClient CLSID
const CLSID_POLICY_CONFIG_CLIENT: GUID = GUID::from_u128(0x870af99c_171d_4f9e_af0d_e63df40c2bc9);

/// Sets the specified device as the default for both Console and Communications roles
pub fn set_default_device_for_all_roles(device_id: &str) -> Result<()> {
    set_default_device(device_id, ERole::Console)?;
    set_default_device(device_id, ERole::Communications)?;
    Ok(())
}

/// Sets the specified device as the default for the given role
pub fn set_default_device(device_id: &str, role: ERole) -> Result<()> {
    unsafe {
        let policy_config: IPolicyConfig = CoCreateInstance(
            &CLSID_POLICY_CONFIG_CLIENT,
            None,
            CLSCTX_ALL,
        )?;

        let device_id_wide: Vec<u16> = device_id.encode_utf16().chain(std::iter::once(0)).collect();
        policy_config.SetDefaultEndpoint(PCWSTR(device_id_wide.as_ptr()), role as u32).ok()?;

        Ok(())
    }
}
