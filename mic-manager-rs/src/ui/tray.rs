//! System tray icon management.
//!
//! Manages the system tray icon, tooltip, and context menu.

use std::sync::mpsc::{channel, Receiver, Sender};
use thiserror::Error;
use tray_icon::{
    menu::{CheckMenuItem, Menu, MenuEvent, MenuItem, PredefinedMenuItem},
    Icon, TrayIcon, TrayIconBuilder, TrayIconEvent,
};

/// Initial state for tray icon.
#[derive(Debug, Clone)]
pub struct TrayState {
    /// Tooltip text (device name + mute state)
    pub tooltip: String,

    /// Whether the default microphone is muted
    pub muted: bool,
}

impl Default for TrayState {
    fn default() -> Self {
        Self {
            tooltip: "Microphone Manager".to_string(),
            muted: false,
        }
    }
}

/// Events from the system tray.
#[derive(Debug, Clone)]
pub enum TrayEvent {
    /// Left-click on tray icon
    LeftClick {
        /// Icon bounding rectangle (for flyout positioning)
        rect: Rect,
        /// Mouse position
        position: Position,
    },

    /// Right-click on tray icon (show context menu)
    RightClick { rect: Rect, position: Position },

    /// Double-click on tray icon
    DoubleClick { rect: Rect, position: Position },

    /// Menu item selected
    MenuItemClicked { id: MenuItemId },
}

/// Menu item identifiers.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum MenuItemId {
    Exit,
    StartWithWindows,
}

/// Rectangle in screen coordinates.
#[derive(Debug, Clone, Copy, Default)]
pub struct Rect {
    pub left: f64,
    pub top: f64,
    pub right: f64,
    pub bottom: f64,
}

impl Rect {
    pub fn center(&self) -> Position {
        Position {
            x: (self.left + self.right) / 2.0,
            y: (self.top + self.bottom) / 2.0,
        }
    }

    pub fn width(&self) -> f64 {
        self.right - self.left
    }

    pub fn height(&self) -> f64 {
        self.bottom - self.top
    }
}

impl From<tray_icon::Rect> for Rect {
    fn from(r: tray_icon::Rect) -> Self {
        Self {
            left: r.position.x,
            top: r.position.y,
            right: r.position.x + r.size.width as f64,
            bottom: r.position.y + r.size.height as f64,
        }
    }
}

/// Position in screen coordinates.
#[derive(Debug, Clone, Copy, Default)]
pub struct Position {
    pub x: f64,
    pub y: f64,
}

impl From<tray_icon::dpi::PhysicalPosition<f64>> for Position {
    fn from(p: tray_icon::dpi::PhysicalPosition<f64>) -> Self {
        Self { x: p.x, y: p.y }
    }
}

/// Tray service error types.
#[derive(Debug, Error)]
pub enum TrayError {
    #[error("Failed to create tray icon: {0}")]
    CreateFailed(String),

    #[error("Failed to load icon resource")]
    IconLoadFailed,

    #[error("Tray icon not initialized")]
    NotInitialized,

    #[error("Failed to get icon position")]
    PositionUnavailable,

    #[error("Failed to create menu: {0}")]
    MenuFailed(String),
}

/// System tray manager.
pub struct TrayManager {
    tray_icon: Option<TrayIcon>,
    event_sender: Sender<TrayEvent>,
    event_receiver: Receiver<TrayEvent>,
    exit_menu_id: Option<tray_icon::menu::MenuId>,
    startup_menu_id: Option<tray_icon::menu::MenuId>,
    startup_item: Option<CheckMenuItem>,
}

impl TrayManager {
    /// Create a new TrayManager.
    pub fn new() -> Self {
        let (sender, receiver) = channel();
        Self {
            tray_icon: None,
            event_sender: sender,
            event_receiver: receiver,
            exit_menu_id: None,
            startup_menu_id: None,
            startup_item: None,
        }
    }

    /// Create and show the tray icon.
    pub fn create(&mut self, initial_state: TrayState) -> Result<(), TrayError> {
        // Create icon based on mute state
        let icon = self.create_icon(initial_state.muted)?;

        // Create context menu
        let menu = Menu::new();

        // Add "Start with Windows" menu item (as a checkbox)
        let startup_item = CheckMenuItem::new("Start with Windows", true, false, None);
        self.startup_menu_id = Some(startup_item.id().clone());
        self.startup_item = Some(startup_item.clone());
        menu.append(&startup_item)
            .map_err(|e| TrayError::MenuFailed(e.to_string()))?;

        // Add separator
        menu.append(&PredefinedMenuItem::separator())
            .map_err(|e| TrayError::MenuFailed(e.to_string()))?;

        // Add "Exit" menu item
        let exit_item = MenuItem::new("Exit", true, None);
        self.exit_menu_id = Some(exit_item.id().clone());
        menu.append(&exit_item)
            .map_err(|e| TrayError::MenuFailed(e.to_string()))?;

        // Create tray icon
        let tray_icon = TrayIconBuilder::new()
            .with_icon(icon)
            .with_tooltip(&initial_state.tooltip)
            .with_menu(Box::new(menu))
            .build()
            .map_err(|e| TrayError::CreateFailed(e.to_string()))?;

        self.tray_icon = Some(tray_icon);

        Ok(())
    }

    /// Process tray icon events. Call this from the event loop.
    pub fn process_events(&self) {
        // Process tray icon click events
        if let Ok(event) = TrayIconEvent::receiver().try_recv() {
            match event {
                TrayIconEvent::Click {
                    button: tray_icon::MouseButton::Left,
                    button_state: tray_icon::MouseButtonState::Up,
                    rect,
                    position,
                    ..
                } => {
                    let _ = self.event_sender.send(TrayEvent::LeftClick {
                        rect: rect.into(),
                        position: position.into(),
                    });
                }
                TrayIconEvent::Click {
                    button: tray_icon::MouseButton::Right,
                    button_state: tray_icon::MouseButtonState::Up,
                    rect,
                    position,
                    ..
                } => {
                    let _ = self.event_sender.send(TrayEvent::RightClick {
                        rect: rect.into(),
                        position: position.into(),
                    });
                }
                TrayIconEvent::DoubleClick {
                    button: tray_icon::MouseButton::Left,
                    rect,
                    position,
                    ..
                } => {
                    let _ = self.event_sender.send(TrayEvent::DoubleClick {
                        rect: rect.into(),
                        position: position.into(),
                    });
                }
                _ => {}
            }
        }

        // Process menu events
        if let Ok(event) = MenuEvent::receiver().try_recv() {
            if Some(&event.id) == self.exit_menu_id.as_ref() {
                let _ = self.event_sender.send(TrayEvent::MenuItemClicked {
                    id: MenuItemId::Exit,
                });
            } else if Some(&event.id) == self.startup_menu_id.as_ref() {
                let _ = self.event_sender.send(TrayEvent::MenuItemClicked {
                    id: MenuItemId::StartWithWindows,
                });
            }
        }
    }

    /// Get the event receiver for tray events.
    pub fn events(&self) -> &Receiver<TrayEvent> {
        &self.event_receiver
    }

    /// Update the tray icon based on mute state.
    pub fn set_icon(&mut self, muted: bool) -> Result<(), TrayError> {
        // Create icon before borrowing tray_icon
        let icon = self.create_icon(muted)?;
        let tray = self.tray_icon.as_mut().ok_or(TrayError::NotInitialized)?;
        tray.set_icon(Some(icon))
            .map_err(|e| TrayError::CreateFailed(e.to_string()))?;
        Ok(())
    }

    /// Update the tooltip text.
    pub fn set_tooltip(&mut self, text: &str) -> Result<(), TrayError> {
        let tray = self.tray_icon.as_mut().ok_or(TrayError::NotInitialized)?;
        tray.set_tooltip(Some(text))
            .map_err(|e| TrayError::CreateFailed(e.to_string()))?;
        Ok(())
    }

    /// Update the "Start with Windows" menu item checkmark.
    pub fn set_startup_checked(&mut self, checked: bool) -> Result<(), TrayError> {
        if let Some(ref item) = self.startup_item {
            item.set_checked(checked);
        }
        Ok(())
    }

    /// Create an icon for the given mute state.
    fn create_icon(&self, muted: bool) -> Result<Icon, TrayError> {
        // Create a simple icon programmatically
        // 32x32 RGBA icon
        const SIZE: usize = 32;
        let mut rgba = vec![0u8; SIZE * SIZE * 4];

        if muted {
            // Red icon for muted state
            for y in 0..SIZE {
                for x in 0..SIZE {
                    let idx = (y * SIZE + x) * 4;
                    let dx = x as f32 - SIZE as f32 / 2.0;
                    let dy = y as f32 - SIZE as f32 / 2.0;
                    let dist = (dx * dx + dy * dy).sqrt();

                    if dist < SIZE as f32 / 2.0 - 2.0 {
                        rgba[idx] = 220; // R
                        rgba[idx + 1] = 60; // G
                        rgba[idx + 2] = 60; // B
                        rgba[idx + 3] = 255; // A
                    }
                }
            }

            // Draw strike-through line
            for i in 4..SIZE - 4 {
                let idx = (i * SIZE + i) * 4;
                rgba[idx] = 255;
                rgba[idx + 1] = 255;
                rgba[idx + 2] = 255;
                rgba[idx + 3] = 255;

                let idx2 = (i * SIZE + i + 1) * 4;
                if idx2 + 3 < rgba.len() {
                    rgba[idx2] = 255;
                    rgba[idx2 + 1] = 255;
                    rgba[idx2 + 2] = 255;
                    rgba[idx2 + 3] = 255;
                }
            }
        } else {
            // Green icon for unmuted state
            for y in 0..SIZE {
                for x in 0..SIZE {
                    let idx = (y * SIZE + x) * 4;
                    let dx = x as f32 - SIZE as f32 / 2.0;
                    let dy = y as f32 - SIZE as f32 / 2.0;
                    let dist = (dx * dx + dy * dy).sqrt();

                    if dist < SIZE as f32 / 2.0 - 2.0 {
                        rgba[idx] = 60; // R
                        rgba[idx + 1] = 180; // G
                        rgba[idx + 2] = 60; // B
                        rgba[idx + 3] = 255; // A
                    }
                }
            }
        }

        Icon::from_rgba(rgba, SIZE as u32, SIZE as u32).map_err(|_| TrayError::IconLoadFailed)
    }

    /// Destroy the tray icon.
    pub fn destroy(&mut self) -> Result<(), TrayError> {
        self.tray_icon = None;
        Ok(())
    }
}

impl Default for TrayManager {
    fn default() -> Self {
        Self::new()
    }
}
