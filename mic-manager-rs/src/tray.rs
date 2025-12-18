use crate::WM_TRAY_ICON;
use windows::core::*;
use windows::Win32::Foundation::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::Win32::Graphics::Gdi::*;

const TRAY_ICON_ID: u32 = 1;

/// Manages the system tray icon
pub struct TrayIcon {
    hwnd: HWND,
    icon_active: HICON,
    icon_muted: HICON,
}

impl TrayIcon {
    pub fn new(hwnd: HWND, is_muted: bool, tooltip: &str) -> Result<Self> {
        // Try to create custom icons, fall back to system icons
        let (icon_active, icon_muted) = match (create_microphone_icon(false), create_microphone_icon(true)) {
            (Ok(active), Ok(muted)) => (active, muted),
            _ => unsafe {
                // Fall back to system icons
                let active = LoadIconW(None, IDI_APPLICATION)?;
                let muted = LoadIconW(None, IDI_WARNING)?;
                (active, muted)
            }
        };

        let tray = Self {
            hwnd,
            icon_active,
            icon_muted,
        };

        tray.add(is_muted, tooltip)?;

        Ok(tray)
    }

    fn add(&self, is_muted: bool, tooltip: &str) -> Result<()> {
        let icon = if is_muted { self.icon_muted } else { self.icon_active };

        let mut nid = NOTIFYICONDATAW {
            cbSize: std::mem::size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: self.hwnd,
            uID: TRAY_ICON_ID,
            uFlags: NIF_ICON | NIF_MESSAGE | NIF_TIP | NIF_SHOWTIP,
            uCallbackMessage: WM_TRAY_ICON,
            hIcon: icon,
            ..Default::default()
        };

        // Set tooltip
        let tooltip_wide: Vec<u16> = tooltip.encode_utf16().chain(std::iter::once(0)).collect();
        let len = std::cmp::min(tooltip_wide.len(), nid.szTip.len());
        nid.szTip[..len].copy_from_slice(&tooltip_wide[..len]);

        unsafe {
            if !Shell_NotifyIconW(NIM_ADD, &nid).as_bool() {
                let err = GetLastError();
                return Err(Error::new(HRESULT::from_win32(err.0), "Shell_NotifyIconW failed"));
            }

            // Set version for modern behavior
            nid.Anonymous.uVersion = NOTIFYICON_VERSION_4;
            let _ = Shell_NotifyIconW(NIM_SETVERSION, &nid);
        }

        Ok(())
    }

    pub fn update(&mut self, is_muted: bool, tooltip: &str) -> Result<()> {
        let icon = if is_muted { self.icon_muted } else { self.icon_active };

        let mut nid = NOTIFYICONDATAW {
            cbSize: std::mem::size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: self.hwnd,
            uID: TRAY_ICON_ID,
            uFlags: NIF_ICON | NIF_TIP | NIF_SHOWTIP,
            hIcon: icon,
            ..Default::default()
        };

        // Set tooltip with mute status
        let status = if is_muted { " (Muted)" } else { "" };
        let full_tooltip = format!("{}{}", tooltip, status);
        let tooltip_wide: Vec<u16> = full_tooltip.encode_utf16().chain(std::iter::once(0)).collect();
        let len = std::cmp::min(tooltip_wide.len(), nid.szTip.len());
        nid.szTip[..len].copy_from_slice(&tooltip_wide[..len]);

        unsafe {
            let _ = Shell_NotifyIconW(NIM_MODIFY, &nid);
        }

        Ok(())
    }

    pub fn remove(&self) {
        let nid = NOTIFYICONDATAW {
            cbSize: std::mem::size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: self.hwnd,
            uID: TRAY_ICON_ID,
            ..Default::default()
        };

        unsafe {
            let _ = Shell_NotifyIconW(NIM_DELETE, &nid);
        }
    }
}

impl Drop for TrayIcon {
    fn drop(&mut self) {
        // Note: Don't destroy system icons (IDI_APPLICATION, etc.)
        // Only destroy custom-created icons
    }
}

/// Create a simple microphone icon programmatically
fn create_microphone_icon(muted: bool) -> Result<HICON> {
    unsafe {
        let size = 16i32;

        // Create a device context and bitmap for the icon
        let screen_dc = GetDC(None);
        if screen_dc.is_invalid() {
            return Err(Error::from_win32());
        }

        let mem_dc = CreateCompatibleDC(screen_dc);
        if mem_dc.is_invalid() {
            ReleaseDC(None, screen_dc);
            return Err(Error::from_win32());
        }

        let bitmap = CreateCompatibleBitmap(screen_dc, size, size);
        if bitmap.is_invalid() {
            let _ = DeleteDC(mem_dc);
            ReleaseDC(None, screen_dc);
            return Err(Error::from_win32());
        }

        let old_bitmap = SelectObject(mem_dc, bitmap);

        // Fill background with black (transparent)
        let black_brush = GetStockObject(BLACK_BRUSH);
        let rect = RECT { left: 0, top: 0, right: size, bottom: size };
        FillRect(mem_dc, &rect, HBRUSH(black_brush.0));

        // Draw microphone shape in white or gray
        let color = if muted { COLORREF(0x00808080) } else { COLORREF(0x00FFFFFF) };
        let pen = CreatePen(PS_SOLID, 1, color);
        let brush = CreateSolidBrush(color);
        let old_pen = SelectObject(mem_dc, pen);
        let old_brush = SelectObject(mem_dc, brush);

        // Draw microphone body (oval)
        let _ = Ellipse(mem_dc, 5, 1, 11, 9);

        // Draw microphone stand
        let _ = MoveToEx(mem_dc, 8, 9, None);
        let _ = LineTo(mem_dc, 8, 12);

        // Draw microphone base
        let _ = MoveToEx(mem_dc, 5, 12, None);
        let _ = LineTo(mem_dc, 11, 12);

        // Draw arc around microphone (holder)
        let _ = Arc(mem_dc, 3, 4, 13, 11, 12, 8, 4, 8);

        // If muted, draw a red line through it
        if muted {
            let red_pen = CreatePen(PS_SOLID, 2, COLORREF(0x000000FF)); // Red
            SelectObject(mem_dc, red_pen);
            let _ = MoveToEx(mem_dc, 2, 2, None);
            let _ = LineTo(mem_dc, 14, 14);
            let _ = DeleteObject(red_pen);
        }

        SelectObject(mem_dc, old_pen);
        SelectObject(mem_dc, old_brush);
        let _ = DeleteObject(pen);
        let _ = DeleteObject(brush);

        SelectObject(mem_dc, old_bitmap);

        // Create mask bitmap (all white = all opaque for color icon)
        let mask = CreateBitmap(size, size, 1, 1, None);
        if mask.is_invalid() {
            let _ = DeleteObject(bitmap);
            let _ = DeleteDC(mem_dc);
            ReleaseDC(None, screen_dc);
            return Err(Error::from_win32());
        }

        // Fill mask with zeros (all opaque)
        let mask_dc = CreateCompatibleDC(screen_dc);
        let old_mask = SelectObject(mask_dc, mask);
        let black_brush2 = GetStockObject(BLACK_BRUSH);
        FillRect(mask_dc, &rect, HBRUSH(black_brush2.0));
        SelectObject(mask_dc, old_mask);
        let _ = DeleteDC(mask_dc);

        let _ = DeleteDC(mem_dc);
        ReleaseDC(None, screen_dc);

        // Create icon from bitmaps
        let icon_info = ICONINFO {
            fIcon: TRUE,
            xHotspot: 0,
            yHotspot: 0,
            hbmMask: mask,
            hbmColor: bitmap,
        };

        let icon = CreateIconIndirect(&icon_info)?;

        let _ = DeleteObject(bitmap);
        let _ = DeleteObject(mask);

        Ok(icon)
    }
}
