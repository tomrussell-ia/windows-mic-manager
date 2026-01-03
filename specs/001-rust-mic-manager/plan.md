# Implementation Plan: Windows Microphone Manager (Rust Rebuild)

**Branch**: `001-rust-mic-manager` | **Date**: 2026-01-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-rust-mic-manager/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Rebuild the existing Windows Microphone Manager (currently a WPF/C# proof-of-concept) in Rust. The application is a system tray utility that provides:
- Flyout window showing all microphones with individual controls (level meters, volume sliders, mute toggles, format tags)
- Default device selection (Console and Communications roles separately)
- Real-time sync with external changes
- Hot-plug device detection
- Dock/undock functionality
- Start with Windows option

## Technical Context

**Language/Version**: Rust 1.75+ (stable)
**Primary Dependencies**:
- `windows-rs` v0.58 - Windows API bindings (MMDevice API, IAudioEndpointVolume, WASAPI, COM)
- `eframe` v0.29 - UI Framework (egui + winit)
- `tray-icon` v0.17 - System tray icon
- `muda` v0.14 - Context menus (re-exported by tray-icon)
- `com-policy-config` v0.1 - IPolicyConfig interface for setting default device
- `thiserror` v1.0 - Error handling
- `tracing` v0.1 - Logging
**Storage**: Windows Registry (for "Start with Windows" setting only)
**Testing**: cargo test, windows integration tests
**Target Platform**: Windows 10 version 1809+, Windows 11 (x64)
**Project Type**: single (native Windows desktop application)
**Performance Goals**:
- Input level meters: 20Hz+ refresh rate (per SC-004)
- Tray icon update: <500ms (per SC-002)
- Volume slider response: <100ms (per SC-007)
- Startup time: <2 seconds (per SC-006)
**Constraints**:
- Memory: <75MB during normal operation (per SC-005)
- External change sync: <1 second (per SC-009)
- Device list update: <2 seconds (per SC-003)
**Scale/Scope**: Single-user desktop utility, 1-10 microphone devices typical

### Technology Decisions (from research.md)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI Framework | egui + eframe | Best for real-time level meters, ~65MB memory, fast startup, excellent custom widget support |
| System Tray | tray-icon crate | Provides click position/rect for flyout positioning, integrated menu support, Tauri-maintained |
| Audio APIs | windows-rs | Official Microsoft bindings, full API coverage, type-safe |
| Default Device | com-policy-config | Wraps undocumented IPolicyConfig COM interface |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research (Phase 0)
**Status**: PASS (no specific gates defined)

The project constitution (`.specify/memory/constitution.md`) is currently a template with no specific principles or constraints defined.

### Post-Design (Phase 1)
**Status**: PASS

**Implicit Best Practices Applied**:
- Single-purpose application (microphone management only)
- No unnecessary abstractions (direct Windows API calls, no ORM, no complex patterns)
- Platform-native approach (Windows APIs via windows-rs)
- Standard Rust project structure (single crate, modular organization)
- Minimal dependencies (only essential crates selected)

**Design Verification**:
- [x] Data model matches specification requirements (MicrophoneDevice has all required fields)
- [x] Service contracts are minimal and focused
- [x] No over-engineering (no repository pattern, no dependency injection framework)
- [x] Performance targets are achievable with chosen technology stack

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
mic-manager-rs/
├── Cargo.toml           # Project manifest with dependencies
├── build.rs             # Build script (for Windows resources/manifest)
├── src/
│   ├── main.rs          # Application entry point
│   ├── lib.rs           # Library root (for testing)
│   ├── app.rs           # Application state and lifecycle
│   ├── audio/
│   │   ├── mod.rs       # Audio module root
│   │   ├── device.rs    # MicrophoneDevice model
│   │   ├── enumerator.rs # Device enumeration (MMDeviceEnumerator)
│   │   ├── volume.rs    # Volume control (IAudioEndpointVolume)
│   │   ├── capture.rs   # Audio capture for level metering (WASAPI)
│   │   ├── policy.rs    # IPolicyConfig COM interface for defaults
│   │   └── notifications.rs # Device change notifications
│   ├── ui/
│   │   ├── mod.rs       # UI module root
│   │   ├── tray.rs      # System tray icon and menu
│   │   ├── flyout.rs    # Flyout window
│   │   ├── components/  # Reusable UI components
│   │   │   ├── mod.rs
│   │   │   ├── device_row.rs    # Microphone device row
│   │   │   ├── level_meter.rs   # Audio level meter
│   │   │   └── volume_slider.rs # Volume slider
│   │   └── theme.rs     # Windows 11 styling/colors
│   └── platform/
│       ├── mod.rs       # Platform-specific module
│       ├── registry.rs  # Windows Registry (startup setting)
│       └── icons.rs     # Icon generation/loading
├── resources/
│   ├── app.manifest     # Windows application manifest
│   └── icons/           # Application icons
└── tests/
    ├── audio_tests.rs   # Audio API integration tests
    └── ui_tests.rs      # UI component tests
```

**Structure Decision**: Single Rust binary crate with modular source organization. The `audio/` module encapsulates all Windows Core Audio API interactions. The `ui/` module handles the system tray and flyout window. The `platform/` module contains Windows-specific utilities (registry, icons).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations to justify. The design follows minimal complexity principles:
- Single crate (no workspace complexity)
- Direct API calls (no abstraction layers)
- Standard module organization
