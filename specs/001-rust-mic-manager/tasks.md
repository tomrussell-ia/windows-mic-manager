# Tasks: Windows Microphone Manager (Rust Rebuild)

**Input**: Design documents from `/specs/001-rust-mic-manager/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Not explicitly requested - test tasks omitted.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
```text
mic-manager-rs/
├── Cargo.toml
├── build.rs
├── src/
│   ├── main.rs, lib.rs, app.rs
│   ├── audio/ (device.rs, enumerator.rs, volume.rs, capture.rs, policy.rs, notifications.rs)
│   ├── ui/ (tray.rs, flyout.rs, theme.rs, components/)
│   └── platform/ (registry.rs, icons.rs)
├── resources/ (app.manifest, icons/)
└── tests/
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create project directory `mic-manager-rs/` at repository root
- [X] T002 Create `mic-manager-rs/Cargo.toml` with all dependencies from quickstart.md
- [X] T003 [P] Create `mic-manager-rs/build.rs` for Windows manifest embedding
- [X] T004 [P] Create `mic-manager-rs/resources/app.manifest` with DPI awareness settings
- [X] T005 [P] Create directory structure: `src/audio/`, `src/ui/`, `src/ui/components/`, `src/platform/`, `resources/icons/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

### Data Models

- [X] T006 [P] Create `mic-manager-rs/src/audio/mod.rs` with module exports
- [X] T007 [P] Implement `MicrophoneDevice`, `AudioFormat` structs in `mic-manager-rs/src/audio/device.rs` per data-model.md
- [X] T008 [P] Implement `DeviceRole`, `DeviceState`, `DeviceEvent` enums in `mic-manager-rs/src/audio/device.rs`
- [X] T009 [P] Implement `AudioError` enum in `mic-manager-rs/src/audio/device.rs` per audio-service contract
- [X] T010 [P] Create `mic-manager-rs/src/ui/mod.rs` with module exports
- [X] T011 [P] Implement `TrayState`, `TrayEvent`, `TrayError`, `Rect`, `Position`, `MenuItemId` in `mic-manager-rs/src/ui/tray.rs` per tray-service contract
- [X] T012 [P] Create `mic-manager-rs/src/platform/mod.rs` with module exports
- [X] T013 [P] Implement `UserPreferences`, `WindowMode`, `PreferencesError` in `mic-manager-rs/src/platform/registry.rs` per preferences-service contract
- [X] T014 [P] Create `mic-manager-rs/src/ui/components/mod.rs` with component exports

### Core Audio Infrastructure

- [X] T015 Implement COM initialization wrapper in `mic-manager-rs/src/audio/enumerator.rs`
- [X] T016 Implement `IMMDeviceEnumerator` wrapper and device enumeration in `mic-manager-rs/src/audio/enumerator.rs`
- [X] T017 Implement `get_devices()`, `get_device()` methods in `mic-manager-rs/src/audio/enumerator.rs`

### Application Shell

- [X] T018 Implement `AppState` struct in `mic-manager-rs/src/app.rs` per data-model.md
- [X] T019 Implement basic tray icon creation (unmuted state only) in `mic-manager-rs/src/ui/tray.rs`
- [X] T020 Implement basic flyout window shell using eframe in `mic-manager-rs/src/ui/flyout.rs`
- [X] T021 Create `mic-manager-rs/src/lib.rs` with module declarations
- [X] T022 Create `mic-manager-rs/src/main.rs` entry point with COM init, tray creation, and event loop

**Checkpoint**: Foundation ready - application launches with tray icon, flyout opens on click (empty)

---

## Phase 3: User Story 1 + 2 - View All Microphones + Default Device Selection (Priority: P1)

**Goal**: Show all microphones in flyout with individual controls; allow setting default device independently

**Independent Test**: Launch app, click tray icon, verify device list shows with all controls; click to set default device

### Implementation for User Story 1 + 2

- [X] T023 [P] [US1] Implement device row component UI shell in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T024 [P] [US1] Implement volume slider UI component (display only) in `mic-manager-rs/src/ui/components/volume_slider.rs`
- [X] T025 [P] [US1] Implement level meter UI component (display only) in `mic-manager-rs/src/ui/components/level_meter.rs`
- [X] T026 [P] [US1] Implement mute toggle button UI (display only) in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T027 [P] [US1] Implement format tag UI (display only) in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T028 [P] [US2] Implement default device indicator icons (Console, Communications) in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T029 [US1] Wire flyout to display device list using device_row components in `mic-manager-rs/src/ui/flyout.rs`
- [X] T030 [US1] Implement "No microphones detected" empty state in `mic-manager-rs/src/ui/flyout.rs`
- [X] T031 [US2] Implement IPolicyConfig wrapper for `set_default_device()` in `mic-manager-rs/src/audio/policy.rs`
- [X] T032 [US2] Implement `get_default_device_id()` for Console and Communications roles in `mic-manager-rs/src/audio/enumerator.rs`
- [X] T033 [US2] Add click handlers for "Set as Default", "Set as Communication", "Set as Both" in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T034 [US2] Wire default device selection to IPolicyConfig in `mic-manager-rs/src/ui/flyout.rs`

**Checkpoint**: Flyout shows all microphones with UI controls; default device can be set

---

## Phase 4: User Story 3 + 4 - Mute/Unmute + System Tray Presence (Priority: P2)

**Goal**: Mute any microphone individually; tray icon reflects default device mute state

**Independent Test**: Click mute button on any mic, verify mute state changes; verify tray icon updates for default device

### Implementation for User Story 3 + 4

- [X] T035 [P] [US3] Implement `IAudioEndpointVolume` wrapper in `mic-manager-rs/src/audio/volume.rs`
- [X] T036 [P] [US3] Implement `get_mute()`, `set_mute()`, `toggle_mute()` in `mic-manager-rs/src/audio/volume.rs`
- [X] T037 [P] [US4] Create muted/unmuted icon assets (RGBA data or ICO) in `mic-manager-rs/src/platform/icons.rs`
- [X] T038 [US3] Wire mute toggle buttons to volume.rs mute functions in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T039 [US4] Implement `set_icon(muted: bool)` to update tray icon dynamically in `mic-manager-rs/src/ui/tray.rs`
- [X] T040 [US4] Implement `set_tooltip()` with "{DeviceName}" or "{DeviceName} (Muted)" format in `mic-manager-rs/src/ui/tray.rs`
- [X] T041 [US4] Implement tray context menu with "Exit" option in `mic-manager-rs/src/ui/tray.rs`
- [X] T042 [US3] Update tray icon when default device mute state changes in `mic-manager-rs/src/app.rs`
- [X] T043 [US1] Implement flyout close-on-click-outside behavior in `mic-manager-rs/src/ui/flyout.rs`

**Checkpoint**: Mute toggles work; tray icon shows mute state; tooltip shows device name; Exit menu works

---

## Phase 5: User Story 5 + 6 + 10 - Volume + Level Meters + Format Tags (Priority: P3)

**Goal**: Adjust volume for any mic; see real-time input levels at 20Hz+; display audio format

**Independent Test**: Drag volume slider, verify system volume changes; speak and see level meters respond; verify format tags display

### Implementation for User Story 5 + 6 + 10

- [X] T044 [P] [US5] Implement `get_volume()`, `set_volume()` in `mic-manager-rs/src/audio/volume.rs`
- [X] T045 [P] [US10] Implement `get_audio_format()` to query WAVEFORMATEX in `mic-manager-rs/src/audio/enumerator.rs`
- [X] T046 [P] [US6] Implement `IAudioMeterInformation` wrapper in `mic-manager-rs/src/audio/capture.rs`
- [X] T047 [P] [US6] Implement `get_peak_level()` using GetPeakValue() in `mic-manager-rs/src/audio/capture.rs`
- [X] T048 [US5] Wire volume slider drag events to `set_volume()` in `mic-manager-rs/src/ui/components/volume_slider.rs`
- [X] T049 [US5] Update slider position when volume changes externally in `mic-manager-rs/src/ui/components/volume_slider.rs`
- [X] T050 [US6] Implement 20Hz+ polling loop for level metering in `mic-manager-rs/src/app.rs`
- [X] T051 [US6] Update level meter component with real-time input_level values in `mic-manager-rs/src/ui/components/level_meter.rs`
- [X] T052 [US6] Implement peak hold indicator with decay in `mic-manager-rs/src/ui/components/level_meter.rs`
- [X] T053 [US6] Add dB scale markers (-60dB to 0dB) to level meter in `mic-manager-rs/src/ui/components/level_meter.rs`
- [X] T054 [US10] Display audio format tag (e.g., "48kHz/24-bit") in device row in `mic-manager-rs/src/ui/components/device_row.rs`
- [X] T055 [US10] Handle missing format gracefully (no tag shown) in `mic-manager-rs/src/ui/components/device_row.rs`

**Checkpoint**: Volume sliders control system volume; level meters animate in real-time; format tags display

---

## Phase 6: User Story 11 - Real-Time Sync with External Changes (Priority: P3)

**Goal**: Reflect audio setting changes made by other programs within 1 second

**Independent Test**: Change default mic or volume in Windows Settings, verify flyout updates automatically

### Implementation for User Story 11

- [X] T056 [US11] Implement `IMMNotificationClient` trait using `#[implement]` macro in `mic-manager-rs/src/audio/notifications.rs`
- [X] T057 [US11] Implement `OnDefaultDeviceChanged` callback in `mic-manager-rs/src/audio/notifications.rs`
- [X] T058 [US11] Implement `OnDeviceStateChanged` callback in `mic-manager-rs/src/audio/notifications.rs`
- [X] T059 [US11] Implement `IAudioEndpointVolumeCallback` for volume/mute change notifications in `mic-manager-rs/src/audio/notifications.rs`
- [X] T060 [US11] Create event channel (mpsc) for DeviceEvent delivery in `mic-manager-rs/src/audio/notifications.rs`
- [X] T061 [US11] Register notification callbacks with `IMMDeviceEnumerator` and `IAudioEndpointVolume` in `mic-manager-rs/src/audio/enumerator.rs`
- [X] T062 [US11] Handle DeviceEvent::VolumeChanged to update UI in `mic-manager-rs/src/app.rs`
- [X] T063 [US11] Handle DeviceEvent::DefaultDeviceChanged to update UI and tray in `mic-manager-rs/src/app.rs`
- [X] T064 [US11] Handle external format changes by updating format tag in `mic-manager-rs/src/app.rs`

**Checkpoint**: UI syncs with Windows Settings changes within 1 second

---

## Phase 7: User Story 7 - Hot-Plug Device Detection (Priority: P4)

**Goal**: Automatically detect when microphones are connected or disconnected

**Independent Test**: Connect/disconnect USB mic while flyout is open, verify list updates

### Implementation for User Story 7

- [X] T065 [US7] Implement `OnDeviceAdded` callback in `mic-manager-rs/src/audio/notifications.rs`
- [X] T066 [US7] Implement `OnDeviceRemoved` callback in `mic-manager-rs/src/audio/notifications.rs`
- [X] T067 [US7] Handle DeviceEvent::DeviceAdded to refresh device list in `mic-manager-rs/src/app.rs`
- [X] T068 [US7] Handle DeviceEvent::DeviceRemoved to refresh device list in `mic-manager-rs/src/app.rs`
- [X] T069 [US7] Update tray tooltip when default device is disconnected in `mic-manager-rs/src/app.rs`
- [X] T070 [US7] Stop level metering for removed devices gracefully in `mic-manager-rs/src/app.rs`

**Checkpoint**: Device list updates within 2 seconds of hot-plug events

---

## Phase 8: User Story 8 - Start with Windows (Priority: P5)

**Goal**: Option to start application automatically when Windows starts

**Independent Test**: Enable "Start with Windows", restart computer, verify app launches

### Implementation for User Story 8

- [X] T071 [P] [US8] Implement registry read/write helpers in `mic-manager-rs/src/platform/registry.rs`
- [X] T072 [P] [US8] Implement `is_startup_enabled()` checking `HKCU\...\Run\MicrophoneManager` in `mic-manager-rs/src/platform/registry.rs`
- [X] T073 [US8] Implement `set_startup_enabled(bool)` to add/remove Run key in `mic-manager-rs/src/platform/registry.rs`
- [X] T074 [US8] Add "Start with Windows" menu item with checkmark to tray context menu in `mic-manager-rs/src/ui/tray.rs`
- [X] T075 [US8] Handle MenuItemId::StartWithWindows click to toggle startup in `mic-manager-rs/src/app.rs`

**Checkpoint**: "Start with Windows" toggle works; registry key is set/removed correctly

---

## Phase 9: User Story 9 - Dock/Undock Flyout Window (Priority: P5)

**Goal**: Dock flyout as persistent window that doesn't auto-close

**Independent Test**: Click dock button, verify window persists; click undock, verify flyout behavior returns

### Implementation for User Story 9

- [X] T076 [P] [US9] Implement `load()`, `save()` for UserPreferences in `mic-manager-rs/src/platform/registry.rs`
- [X] T077 [US9] Add dock/undock button to flyout header in `mic-manager-rs/src/ui/flyout.rs`
- [X] T078 [US9] Implement WindowMode toggle logic in `mic-manager-rs/src/app.rs`
- [X] T079 [US9] Disable close-on-click-outside when WindowMode::Docked in `mic-manager-rs/src/ui/flyout.rs`
- [X] T080 [US9] Persist WindowMode to registry on change in `mic-manager-rs/src/app.rs`
- [X] T081 [US9] Load WindowMode from registry on startup in `mic-manager-rs/src/app.rs`

**Checkpoint**: Dock/undock toggle works; mode persists across sessions

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T082 [P] Implement Windows 11 styling/theme colors in `mic-manager-rs/src/ui/theme.rs`
- [X] T083 [P] Add Mica/Acrylic backdrop effect using DwmSetWindowAttribute in `mic-manager-rs/src/ui/flyout.rs`
- [X] T084 Implement inline error message display in flyout for failures in `mic-manager-rs/src/ui/flyout.rs`
- [X] T085 Handle Windows audio service unavailable: display error, implement background retry in `mic-manager-rs/src/app.rs`
- [X] T086 Add tracing/logging throughout application using `tracing` crate
- [X] T087 Profile memory usage during normal operation; verify <75MB constraint (SC-005)
- [X] T088 Validate timing constraints: tray icon <500ms (SC-002), volume slider <100ms (SC-007), external sync <1s (SC-009)
- [X] T089 Validate all success criteria from spec.md (SC-001 through SC-011)
- [X] T090 Run quickstart.md verification steps
- [X] T091 Code cleanup: run `cargo fmt` and `cargo clippy`, fix warnings
- [X] T092 Build release binary: `cargo build --release`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories proceed in priority order (P1 -> P2 -> P3 -> P4 -> P5)
- **Polish (Phase 10)**: Depends on all user stories being complete

### User Story Dependencies

| Story | Priority | Depends On | Notes |
|-------|----------|------------|-------|
| US1+US2 | P1 | Foundational | Flyout device list + default selection |
| US3+US4 | P2 | US1+US2 | Mute relies on device row; tray needs default device |
| US5+US6+US10 | P3 | US3+US4 | Volume/levels build on volume.rs foundation |
| US11 | P3 | US5+US6 | Sync requires components to update |
| US7 | P4 | US11 | Hot-plug uses same notification infrastructure |
| US8 | P5 | US4 | Startup requires tray context menu |
| US9 | P5 | US1 | Dock/undock requires flyout window |

### Within Each Phase

- Models before services
- Services before UI components
- UI components before integration
- All [P] tasks within a phase can run in parallel

### Parallel Opportunities

- All Setup tasks T003-T005 can run in parallel
- All data model tasks T006-T014 can run in parallel
- All UI component tasks T023-T028 can run in parallel
- Independent service implementations marked [P] can run in parallel

---

## Parallel Example: Phase 2 Data Models

```bash
# Launch all data model tasks together:
Task: "Implement MicrophoneDevice, AudioFormat structs in mic-manager-rs/src/audio/device.rs"
Task: "Implement DeviceRole, DeviceState, DeviceEvent enums in mic-manager-rs/src/audio/device.rs"
Task: "Implement TrayState, TrayEvent, TrayError in mic-manager-rs/src/ui/tray.rs"
Task: "Implement UserPreferences, WindowMode in mic-manager-rs/src/platform/registry.rs"
```

## Parallel Example: Phase 3 UI Components

```bash
# Launch all UI component shell tasks together:
Task: "Implement device row component UI shell in mic-manager-rs/src/ui/components/device_row.rs"
Task: "Implement volume slider UI component in mic-manager-rs/src/ui/components/volume_slider.rs"
Task: "Implement level meter UI component in mic-manager-rs/src/ui/components/level_meter.rs"
```

---

## Implementation Strategy

### MVP First (US1+US2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: US1+US2 (Flyout with device list + default selection)
4. **STOP and VALIDATE**: Can show devices and set defaults
5. Demo if ready (basic functionality working)

### Incremental Delivery

1. Setup + Foundational -> App launches with tray icon
2. Add US1+US2 -> Device list + default selection (MVP!)
3. Add US3+US4 -> Mute controls + tray state
4. Add US5+US6+US10 -> Volume sliders + level meters + format
5. Add US11 -> External sync
6. Add US7 -> Hot-plug detection
7. Add US8+US9 -> Startup + dock/undock
8. Each phase adds value without breaking previous functionality

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story phase should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate current functionality
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
