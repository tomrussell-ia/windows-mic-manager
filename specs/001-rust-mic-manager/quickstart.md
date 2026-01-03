# Quickstart: Windows Microphone Manager (Rust)

**Feature Branch**: `001-rust-mic-manager`

## Prerequisites

### System Requirements
- Windows 10 version 1809+ or Windows 11 (development and runtime)
- Rust 1.75+ stable
- Visual Studio Build Tools 2019+ with "Desktop development with C++" workload

### Verify Rust Installation
```powershell
rustc --version  # Should be 1.75.0 or higher
cargo --version
```

If not installed:
```powershell
# Install rustup from https://rustup.rs
winget install Rustlang.Rustup
# Or download from https://win.rustup.rs
```

### Verify MSVC Toolchain
```powershell
# Check for cl.exe (MSVC compiler)
where cl
```

If not found, install Visual Studio Build Tools:
```powershell
winget install Microsoft.VisualStudio.2022.BuildTools
# Select "Desktop development with C++" workload
```

---

## Project Setup

### 1. Create Project Structure
```powershell
cd d:\repos\local\windows-microphone-manager

# Create the Rust project
cargo new mic-manager-rs
cd mic-manager-rs
```

### 2. Configure Cargo.toml
```toml
[package]
name = "mic-manager-rs"
version = "0.1.0"
edition = "2021"
authors = ["Your Name"]
description = "Windows microphone manager system tray utility"

[dependencies]
# UI Framework
eframe = "0.29"

# System Tray
tray-icon = "0.17"

# Windows APIs
windows = { version = "0.58", features = [
    "Win32_Media_Audio",
    "Win32_Media_Audio_Endpoints",
    "Win32_System_Com",
    "Win32_Foundation",
    "Win32_UI_Shell_PropertiesSystem",
    "Win32_UI_WindowsAndMessaging",
    "Win32_Graphics_Dwm",
    "Win32_Devices_Properties",
    "implement",
]}

# For IPolicyConfig (set default device)
com-policy-config = "0.1"

# Error handling
thiserror = "1.0"
anyhow = "1.0"

# Logging
tracing = "0.1"
tracing-subscriber = { version = "0.3", features = ["env-filter"] }

[build-dependencies]
# For embedding Windows manifest and resources
embed-resource = "2.4"

[profile.release]
lto = true
codegen-units = 1
strip = true
```

### 3. Create Windows Application Manifest
Create `resources/app.manifest`:
```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity
    version="1.0.0.0"
    processorArchitecture="amd64"
    name="MicrophoneManager"
    type="win32"/>
  <description>Windows Microphone Manager</description>
  <dependency>
    <dependentAssembly>
      <assemblyIdentity
        type="win32"
        name="Microsoft.Windows.Common-Controls"
        version="6.0.0.0"
        processorArchitecture="*"
        publicKeyToken="6595b64144ccf1df"
        language="*"/>
    </dependentAssembly>
  </dependency>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">True/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

### 4. Create Build Script
Create `build.rs`:
```rust
fn main() {
    // Embed Windows manifest
    embed_resource::compile("resources/app.manifest", embed_resource::NONE);

    // Link Windows libraries
    println!("cargo:rustc-link-lib=ole32");
    println!("cargo:rustc-link-lib=user32");
    println!("cargo:rustc-link-lib=shell32");
}
```

---

## Development Workflow

### Build and Run (Debug)
```powershell
cargo build
cargo run
```

### Build Release
```powershell
cargo build --release
# Output: target/release/mic-manager-rs.exe
```

### Run Tests
```powershell
cargo test
```

### Check for Errors Without Building
```powershell
cargo check
```

### Format Code
```powershell
cargo fmt
```

### Lint Code
```powershell
cargo clippy
```

---

## Verify Setup

### Minimal Test Program
Replace `src/main.rs` with:
```rust
use windows::{
    core::*,
    Win32::{
        Media::Audio::*,
        System::Com::*,
    },
};

fn main() -> Result<()> {
    unsafe {
        // Initialize COM
        CoInitializeEx(None, COINIT_MULTITHREADED)?;

        // Create device enumerator
        let enumerator: IMMDeviceEnumerator =
            CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)?;

        // Get all capture devices
        let collection = enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE)?;
        let count = collection.GetCount()?;

        println!("Found {} microphone(s):", count);

        for i in 0..count {
            let device = collection.Item(i)?;
            let id = device.GetId()?;
            println!("  Device {}: {}", i, id.to_string()?);
        }

        Ok(())
    }
}
```

Run:
```powershell
cargo run
```

Expected output:
```
Found 2 microphone(s):
  Device 0: {0.0.1.00000000}.{guid-here}
  Device 1: {0.0.1.00000000}.{another-guid}
```

---

## IDE Setup

### VS Code
Install extensions:
- `rust-analyzer` - Rust language server
- `Even Better TOML` - Cargo.toml support
- `CodeLLDB` - Debugging

Create `.vscode/launch.json`:
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "type": "lldb",
            "request": "launch",
            "name": "Debug mic-manager-rs",
            "cargo": {
                "args": ["build", "--bin=mic-manager-rs"],
                "filter": {
                    "name": "mic-manager-rs",
                    "kind": "bin"
                }
            },
            "args": [],
            "cwd": "${workspaceFolder}/mic-manager-rs"
        }
    ]
}
```

---

## Common Issues

### "windows crate missing methods"
Ensure all required features are enabled in `Cargo.toml`. Check:
```toml
[dependencies.windows]
features = [
    "Win32_Media_Audio",
    "Win32_Media_Audio_Endpoints",
    # ... all features listed above
]
```

### "COM not initialized"
Every thread using COM must initialize it:
```rust
unsafe { CoInitializeEx(None, COINIT_MULTITHREADED)?; }
```

### "IPolicyConfig not found"
Add the `com-policy-config` crate:
```toml
com-policy-config = "0.1"
```

### Build fails on non-Windows
This project is Windows-only. Use Windows or WSL2 with a Windows target:
```powershell
rustup target add x86_64-pc-windows-msvc
```

---

## Next Steps

1. Create module structure per `plan.md`
2. Implement `AudioService` per `contracts/audio-service.md`
3. Implement `TrayService` per `contracts/tray-service.md`
4. Build UI with egui
5. Run `/speckit.tasks` to generate implementation tasks

---

## Resources

- [windows-rs Documentation](https://microsoft.github.io/windows-docs-rs/)
- [egui Book](https://docs.rs/egui/latest/egui/)
- [tray-icon Documentation](https://docs.rs/tray-icon/latest/tray_icon/)
- [Windows Core Audio APIs](https://learn.microsoft.com/en-us/windows/win32/coreaudio/core-audio-apis-in-windows-vista)
