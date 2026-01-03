# Tray Service Contract

**Module**: `mic_manager::ui::tray`
**Purpose**: Manages the system tray icon, tooltip, and context menu.

---

## Service Trait

```rust
/// System tray icon management
pub trait TrayService {
    /// Create and show the tray icon
    fn create(&mut self, initial_state: TrayState) -> Result<(), TrayError>;

    /// Update the tray icon based on mute state
    fn set_icon(&mut self, muted: bool) -> Result<(), TrayError>;

    /// Update the tooltip text
    fn set_tooltip(&mut self, text: &str) -> Result<(), TrayError>;

    /// Get events from the tray icon (clicks, menu selections)
    fn events(&self) -> &Receiver<TrayEvent>;

    /// Get the icon rectangle (for positioning flyout)
    fn get_icon_rect(&self) -> Result<Rect, TrayError>;

    /// Destroy the tray icon
    fn destroy(&mut self) -> Result<(), TrayError>;
}
```

---

## Data Types

```rust
/// Initial state for tray icon
pub struct TrayState {
    /// Tooltip text (device name + mute state)
    pub tooltip: String,

    /// Whether the default microphone is muted
    pub muted: bool,
}

/// Events from the system tray
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
    RightClick {
        rect: Rect,
        position: Position,
    },

    /// Double-click on tray icon
    DoubleClick {
        rect: Rect,
        position: Position,
    },

    /// Menu item selected
    MenuItemClicked { id: MenuItemId },
}

/// Menu item identifiers
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum MenuItemId {
    Exit,
    StartWithWindows,
    // Future: OpenSettings, About, etc.
}

/// Rectangle in screen coordinates
#[derive(Debug, Clone, Copy)]
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
}

/// Position in screen coordinates
#[derive(Debug, Clone, Copy)]
pub struct Position {
    pub x: f64,
    pub y: f64,
}
```

---

## Error Types

```rust
#[derive(Debug, thiserror::Error)]
pub enum TrayError {
    #[error("Failed to create tray icon: {0}")]
    CreateFailed(String),

    #[error("Failed to load icon resource")]
    IconLoadFailed,

    #[error("Tray icon not initialized")]
    NotInitialized,

    #[error("Failed to get icon position")]
    PositionUnavailable,
}
```

---

## Usage Contract

### Icon States
The tray icon has two visual states:
1. **Unmuted**: Microphone icon (normal)
2. **Muted**: Microphone icon with strike-through or red indicator

### Tooltip Format
```
{Device Name}
```
or if muted:
```
{Device Name} (Muted)
```

### Context Menu Items
- **Start with Windows** (toggle, checkmark when enabled)
- **Exit**

### Event Handling
```rust
// Process tray events in the application event loop
match tray.events().try_recv() {
    Ok(TrayEvent::LeftClick { rect, .. }) => {
        // Show flyout near rect
        show_flyout_at(rect);
    }
    Ok(TrayEvent::MenuItemClicked { id: MenuItemId::Exit }) => {
        // Exit application
        app.quit();
    }
    // ...
}
```

---

## Implementation Notes

1. **Icon Resources**: Use `Icon::from_rgba()` to generate icons dynamically, or load from embedded resources
2. **DPI Awareness**: Position values are in physical pixels; handle DPI scaling for window positioning
3. **Menu Integration**: Use `muda` crate (re-exported by `tray-icon`) for context menus
4. **Event Loop**: Tray events must be processed on the thread that created the icon
