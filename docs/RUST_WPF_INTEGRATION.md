# Rust + WPF Integration Architecture

## Executive Summary

This document describes the integration approach for combining the stable C# WPF UI with Rust-based core audio logic. The goal is "best of both worlds": WPF's mature UI tooling and MVVM patterns with Rust's safety guarantees and performance.

## 1. Repository Reconnaissance Summary

### C# WPF Application (Stable, Production-Ready)

| Component | Details |
|-----------|---------|
| **Solution** | `MicrophoneManager.sln` |
| **Project** | `MicrophoneManager/MicrophoneManager.csproj` |
| **Framework** | .NET 8.0 Windows (`net8.0-windows`) |
| **Architecture** | x64 only |
| **MVVM Framework** | CommunityToolkit.Mvvm 8.3.2 |
| **Audio Library** | NAudio 2.2.1 |
| **Tray Icon** | H.NotifyIcon.Wpf 2.1.3 |
| **Lines of Code** | ~1,840 (C#) |

**Key Entry Points:**
- `App.xaml` / `App.xaml.cs` - Application startup
- `MainWindow.xaml` - Hidden window hosting tray icon
- `ViewModels/MicrophoneListViewModel.cs` - Main flyout logic (268 lines)
- `Services/AudioDeviceService.cs` - Audio management (675 lines)
- `Services/PolicyConfigService.cs` - COM interop for IPolicyConfig (69 lines)

### Rust Prototype (Feature-Complete, Alternative UI)

| Component | Details |
|-----------|---------|
| **Location** | `mic-manager-rs/` |
| **Edition** | 2021 |
| **UI Framework** | egui/eframe 0.29 |
| **Windows APIs** | `windows` crate 0.58 |
| **Lines of Code** | ~2,797 (Rust) |

**Crate Structure:**
```
mic-manager-rs/src/
├── audio/           # Core audio logic (948 lines)
│   ├── device.rs    # MicrophoneDevice model
│   ├── enumerator.rs # Device enumeration
│   ├── policy.rs    # IPolicyConfig wrapper
│   ├── volume.rs    # Volume control
│   ├── capture.rs   # Level metering
│   └── notifications.rs # Event handling
├── platform/        # Registry, icons (297 lines)
└── ui/              # egui UI (718 lines)
```

### Build Tooling

- **C# Build**: `dotnet build/publish` with `win-x64-singlefile` profile
- **Rust Build**: `cargo build` (no release scripts yet)
- **Scripts**: `scripts/publish-singlefile-to-utils.ps1`
- **CI**: Not yet configured

---

## 2. Interop Approach Decision

### Options Evaluated

| Approach | Pros | Cons | Verdict |
|----------|------|------|---------|
| **C ABI + P/Invoke** | Simple, standard, in-process, no code gen | Manual marshaling, some unsafe C# | ✅ **Selected** |
| **C ABI + Source Gen** | Type-safe generated bindings | Added complexity, toolchain dependency | Future option |
| **COM** | Native Windows integration | Complex, overkill for this use case | ❌ |
| **gRPC/IPC** | Process isolation, language-agnostic | Performance overhead, complexity | ❌ |
| **uniffi** | Multi-language support | Overkill, unfamiliar tooling | ❌ |

### Recommended Approach: C ABI + P/Invoke

**Rationale:**
1. **Simplicity**: Standard pattern for Rust-to-C# interop
2. **Performance**: In-process calls with minimal overhead
3. **Stability**: Uses Rust's stable `extern "C"` ABI
4. **Familiarity**: P/Invoke is well-understood in .NET ecosystem
5. **Tooling**: No additional code generators required

**Key Design Decisions:**
- JSON for structured data exchange (simple, debuggable)
- Explicit memory ownership (caller or callee frees)
- Error codes + optional error message retrieval
- Handle-based API for engine lifecycle

---

## 3. API Design

### Rust FFI Surface (C ABI)

```c
// Lifecycle
MicEngineHandle mic_engine_create(const char* config_json);
void mic_engine_destroy(MicEngineHandle handle);

// Device Operations
char* mic_engine_get_devices(MicEngineHandle handle);
char* mic_engine_get_device(MicEngineHandle handle, const char* device_id);
int32_t mic_engine_set_default_device(MicEngineHandle handle, const char* device_id, uint32_t role);
int32_t mic_engine_set_volume(MicEngineHandle handle, const char* device_id, float volume);
int32_t mic_engine_toggle_mute(MicEngineHandle handle, const char* device_id);

// Memory Management
void mic_engine_free_string(char* ptr);

// Error Handling
int32_t mic_engine_last_error_code();
char* mic_engine_last_error_message();
```

### JSON Data Contracts

**Device List Response:**
```json
{
  "devices": [
    {
      "id": "\\\\?\\SWD#MMDEVAPI#...",
      "name": "USB Microphone",
      "is_default": true,
      "is_default_communication": false,
      "is_muted": false,
      "volume_level": 0.75,
      "audio_format": {
        "sample_rate": 48000,
        "bit_depth": 16,
        "channels": 1
      }
    }
  ]
}
```

### C# Interop Layer

```csharp
// Low-level P/Invoke definitions
internal static class MicEngineNative
{
    [DllImport("mic_engine_ffi.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mic_engine_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson);
    // ...
}

// Safe wrapper with IDisposable
public sealed class MicEngine : IDisposable
{
    private readonly MicEngineSafeHandle _handle;

    public List<MicrophoneDeviceDto> GetDevices() { ... }
    public void SetDefaultDevice(string deviceId, DeviceRole role) { ... }
}
```

---

## 4. Build & Packaging Plan

### Directory Structure

```
windows-mic-manager/
├── MicrophoneManager/           # C# WPF app
│   └── runtimes/
│       └── win-x64/
│           └── native/
│               └── mic_engine_ffi.dll   # Rust DLL goes here
├── mic-manager-rs/              # Rust egui app (standalone)
├── mic-engine-ffi/              # NEW: Rust FFI library (cdylib)
│   ├── Cargo.toml
│   └── src/lib.rs
└── scripts/
    ├── build-rust-ffi.ps1       # Build Rust DLL
    └── build-all.ps1            # Build everything
```

### Local Development Workflow

```powershell
# 1. Build Rust FFI library
cd mic-engine-ffi
cargo build --release

# 2. Copy DLL to C# project
Copy-Item target/release/mic_engine_ffi.dll `
    ../MicrophoneManager/runtimes/win-x64/native/

# 3. Build and run C# app
cd ../MicrophoneManager
dotnet build
dotnet run
```

### CI Workflow (Future)

```yaml
jobs:
  build:
    - name: Build Rust FFI
      run: |
        cd mic-engine-ffi
        cargo build --release

    - name: Build C# with Rust DLL
      run: |
        dotnet publish -p:PublishProfile=win-x64-singlefile
```

### Architecture Alignment

| Concern | Decision |
|---------|----------|
| **C# Platform** | x64 only (matches existing) |
| **Rust Target** | `x86_64-pc-windows-msvc` |
| **Debug** | Unoptimized Rust DLL for faster builds |
| **Release** | LTO-enabled Rust DLL in single-file publish |

---

## 5. Safety & Correctness Plan

### Memory Ownership Rules

| Data Type | Allocation | Deallocation | Notes |
|-----------|------------|--------------|-------|
| **Config JSON (input)** | C# | C# | Passed as const pointer |
| **Result JSON (output)** | Rust | Rust via `mic_engine_free_string` | Must be freed |
| **Engine Handle** | Rust | Rust via `mic_engine_destroy` | SafeHandle in C# |
| **Device ID strings** | C# | C# | Passed as const pointer |

### Panic Handling

```rust
// All FFI functions use catch_unwind
#[no_mangle]
pub extern "C" fn mic_engine_get_devices(handle: MicEngineHandle) -> *mut c_char {
    std::panic::catch_unwind(|| {
        // Actual implementation
    })
    .unwrap_or_else(|_| {
        set_last_error(ErrorCode::Panic, "Rust panic occurred");
        std::ptr::null_mut()
    })
}
```

### Threading Model

| Thread | Owner | Constraints |
|--------|-------|-------------|
| **UI Thread** | C# (WPF) | All UI updates |
| **FFI Calls** | C# | Can call from any thread |
| **COM Apartment** | Rust | STA initialized per-call |
| **Audio Callbacks** | Windows | Marshaled to Rust channel |

**Recommendation:** Make all FFI calls from background threads in C#, marshal results to UI thread.

### Error Model

```rust
#[repr(i32)]
pub enum ErrorCode {
    Success = 0,
    InvalidHandle = -1,
    DeviceNotFound = -2,
    ComError = -3,
    JsonError = -4,
    Panic = -99,
}

// Thread-local error storage
thread_local! {
    static LAST_ERROR: RefCell<Option<(ErrorCode, String)>> = RefCell::new(None);
}
```

### Logging Strategy

```rust
// Rust: Use tracing with callback to C#
pub type LogCallback = extern "C" fn(level: i32, message: *const c_char);

#[no_mangle]
pub extern "C" fn mic_engine_set_log_callback(callback: LogCallback) { ... }
```

---

## 6. Recommendations & Next Steps

### Boundary Proposal: What Goes Where

| Component | Location | Rationale |
|-----------|----------|-----------|
| **UI Layer** | C# WPF | Mature, stable, no rewrite needed |
| **ViewModel Logic** | C# | MVVM pattern, UI-specific |
| **Audio Device Enumeration** | Rust | Safety, correctness |
| **Volume/Mute Control** | Rust | Atomic operations |
| **Input Level Metering** | Rust | Performance-critical |
| **IPolicyConfig COM** | Rust | Already implemented |
| **Device Notifications** | Rust | Event channel pattern |
| **Tray Icon** | C# | H.NotifyIcon works well |
| **Registry Prefs** | C# | .NET has good registry support |

### Deprecation Plan

After integration is stable:
1. Remove `AudioDeviceService.cs` (replaced by Rust)
2. Remove `PolicyConfigService.cs` (replaced by Rust)
3. Keep `mic-manager-rs/` for standalone builds if desired
4. Consider merging audio logic into single Rust workspace

### Risk Mitigation

| Risk | Mitigation |
|------|------------|
| FFI complexity | Start with minimal spike, expand gradually |
| Build complexity | Single script that builds both |
| Debugging | Rust logging forwarded to C# |
| Performance | Profile after initial integration |
| Memory leaks | SafeHandle + explicit free calls |

### Task Breakdown

1. **Create `mic-engine-ffi` crate** (Rust cdylib)
   - Engine lifecycle (create/destroy)
   - Device enumeration
   - JSON serialization
   - Error handling with thread-local storage
   - Panic catching

2. **Create C# interop layer**
   - P/Invoke declarations
   - SafeHandle for engine
   - Marshaling helpers
   - JSON deserialization

3. **Integrate into ViewModel**
   - Replace AudioDeviceService calls
   - Async wrapper for FFI calls
   - UI thread marshaling

4. **Build automation**
   - PowerShell script for Rust build
   - MSBuild integration for DLL copy
   - CI workflow

5. **Testing**
   - Unit tests for Rust FFI
   - Integration tests in C#
   - Manual end-to-end testing

6. **Documentation**
   - README updates
   - API documentation
   - Troubleshooting guide

---

## Appendix: File Changes Summary

### New Files

| Path | Purpose |
|------|---------|
| `mic-engine-ffi/Cargo.toml` | Rust FFI library manifest |
| `mic-engine-ffi/src/lib.rs` | FFI exports and engine implementation |
| `MicrophoneManager/Interop/MicEngineNative.cs` | P/Invoke declarations |
| `MicrophoneManager/Interop/MicEngineSafeHandle.cs` | SafeHandle wrapper |
| `MicrophoneManager/Interop/MicEngine.cs` | High-level safe wrapper |
| `MicrophoneManager/Interop/MicEngineTypes.cs` | JSON DTOs |
| `scripts/build-rust-ffi.ps1` | Rust build script |
| `scripts/build-all.ps1` | Combined build script |

### Modified Files

| Path | Changes |
|------|---------|
| `MicrophoneManager/MicrophoneManager.csproj` | Native DLL reference |
| `MicrophoneManager/ViewModels/MicrophoneListViewModel.cs` | Use Rust engine |
| `README.md` | Build instructions |
| `CLAUDE.md` | Updated commands |
