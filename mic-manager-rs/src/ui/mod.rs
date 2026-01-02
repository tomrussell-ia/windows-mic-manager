//! UI module for system tray and flyout window.
//!
//! This module provides the user interface components including
//! the system tray icon, flyout window, and reusable UI components.

pub mod components;
pub mod flyout;
pub mod theme;
pub mod tray;

pub use flyout::FlyoutWindow;
pub use tray::{MenuItemId, Position, Rect, TrayError, TrayEvent, TrayManager, TrayState};
