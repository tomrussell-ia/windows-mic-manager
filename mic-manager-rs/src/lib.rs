//! Windows Microphone Manager - Library
//!
//! A system tray utility for managing microphone devices on Windows.
//!
//! ## Features
//!
//! - View all microphone devices with real-time input level meters
//! - Adjust volume and mute state for each device
//! - Set default device for Console and Communications roles
//! - Automatic detection of device hot-plug events
//! - Start with Windows option
//! - Dock/undock flyout window

pub mod app;
pub mod audio;
pub mod platform;
pub mod ui;

pub use app::AppState;
pub use audio::{AudioError, DeviceEnumerator, DeviceEvent, MicrophoneDevice};
pub use platform::{RegistryPreferences, UserPreferences, WindowMode};
pub use ui::{FlyoutWindow, TrayEvent, TrayManager, TrayState};
