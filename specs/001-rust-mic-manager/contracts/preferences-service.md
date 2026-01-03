# Preferences Service Contract

**Module**: `mic_manager::platform::preferences`
**Purpose**: Manages user preferences persisted to Windows Registry.

---

## Service Trait

```rust
/// User preferences management
pub trait PreferencesService {
    /// Load preferences from storage
    fn load(&self) -> Result<UserPreferences, PreferencesError>;

    /// Save preferences to storage
    fn save(&self, preferences: &UserPreferences) -> Result<(), PreferencesError>;

    /// Check if "Start with Windows" is enabled
    fn is_startup_enabled(&self) -> Result<bool, PreferencesError>;

    /// Enable or disable "Start with Windows"
    fn set_startup_enabled(&self, enabled: bool) -> Result<(), PreferencesError>;
}
```

---

## Data Types

```rust
/// User preferences (see data-model.md for full definition)
#[derive(Debug, Clone, Default)]
pub struct UserPreferences {
    /// Start application when Windows starts
    pub start_with_windows: bool,

    /// Remember window mode between sessions
    pub window_mode: WindowMode,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum WindowMode {
    #[default]
    Flyout,
    Docked,
}
```

---

## Error Types

```rust
#[derive(Debug, thiserror::Error)]
pub enum PreferencesError {
    #[error("Failed to access registry: {0}")]
    RegistryAccess(#[source] std::io::Error),

    #[error("Failed to read preference: {key}")]
    ReadFailed { key: String },

    #[error("Failed to write preference: {key}")]
    WriteFailed { key: String },

    #[error("Invalid preference value for: {key}")]
    InvalidValue { key: String },
}
```

---

## Storage Contract

### Registry Location
```
HKEY_CURRENT_USER\Software\MicrophoneManager
```

### Registry Keys
| Key | Type | Description | Default |
|-----|------|-------------|---------|
| `WindowMode` | REG_DWORD | 0=Flyout, 1=Docked | 0 |

### Startup Location
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

Value name: `MicrophoneManager`
Value data: Path to executable

---

## Usage Contract

### Initialization
```rust
let prefs_service = RegistryPreferencesService::new()?;

// Load preferences at startup
let preferences = prefs_service.load().unwrap_or_default();

// Apply to app state
app_state.window_mode = preferences.window_mode;
```

### Save on Change
```rust
// When user changes a preference
app_state.window_mode = WindowMode::Docked;

// Save immediately
let preferences = UserPreferences {
    start_with_windows: prefs_service.is_startup_enabled().unwrap_or(false),
    window_mode: app_state.window_mode,
};
prefs_service.save(&preferences)?;
```

### Startup Registration
```rust
// Enable startup
prefs_service.set_startup_enabled(true)?;
// Creates: HKCU\...\Run\MicrophoneManager = "C:\path\to\mic-manager.exe"

// Disable startup
prefs_service.set_startup_enabled(false)?;
// Deletes: HKCU\...\Run\MicrophoneManager
```

---

## Implementation Notes

1. **No Admin Required**: All registry writes are under `HKEY_CURRENT_USER` (no elevation needed)
2. **Graceful Defaults**: Return defaults if registry keys don't exist
3. **Atomic Saves**: Preferences are small; write all at once
4. **Executable Path**: Use `std::env::current_exe()` for startup registration
