# Feature Specification: Windows Microphone Manager (Rust Rebuild)

**Feature Branch**: `001-rust-mic-manager`
**Created**: 2026-01-01
**Status**: Draft
**Input**: User description: "Rebuild the existing Windows Microphone Manager proof-of-concept application in Rust. The application is a system tray utility for quick microphone selection and control on Windows 11."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View All Microphones with Full Controls (Priority: P1)

As a user, I want to see all available microphones with their individual level meters, volume sliders, mute buttons, and format tags, so that I can monitor and control any microphone - not just the default - especially when applications are using non-default devices.

**Why this priority**: This is the core functionality of the application. Users need visibility into ALL microphones because applications may use non-default devices (e.g., a game using a headset mic while the default is a desk mic). Without per-device controls, users cannot manage microphones that programs are actively using.

**Independent Test**: Can be fully tested by launching the application, clicking the tray icon, and verifying each microphone shows its own level meter, volume slider, mute toggle, and format tag.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** the user left-clicks the system tray icon, **Then** a flyout window appears showing all available microphones with individual controls
2. **Given** the flyout is open, **When** viewing any microphone entry, **Then** it displays: device name, real-time input level meter, volume slider, mute toggle, and audio format tag
3. **Given** an application is using a non-default microphone, **When** viewing the flyout, **Then** the user can see that microphone's input level activity and adjust its volume/mute
4. **Given** the flyout is open showing the microphone list, **When** the user clicks outside the flyout, **Then** the flyout closes

---

### User Story 2 - Select Default Device and Communication Device (Priority: P1)

As a user, I want to independently set which microphone is the "Default Device" (for general apps) and which is the "Default Communication Device" (for Teams, Zoom, etc.), so that I can use different microphones for different purposes.

**Why this priority**: Windows maintains two separate default designations. Many users want their headset for calls but a high-quality desk mic for recordings. This distinction is essential for proper microphone management.

**Independent Test**: Can be tested by setting one microphone as Default Device and a different one as Default Communication Device, then verifying apps use the correct one.

**Acceptance Scenarios**:

1. **Given** the flyout is open, **When** viewing a microphone, **Then** it shows separate indicators for "Default" (general) and "Communication" (headset/VoIP) status
2. **Given** the flyout is open, **When** the user clicks to set a microphone as Default Device, **Then** only the Default Device designation changes (Communication Device unchanged)
3. **Given** the flyout is open, **When** the user clicks to set a microphone as Default Communication Device, **Then** only the Communication Device designation changes (Default Device unchanged)
4. **Given** the flyout is open, **When** the user wants to set both defaults to the same device, **Then** a "Set as Both" option is available
5. **Given** different microphones are set as Default vs Communication, **When** viewing the flyout, **Then** both designations are clearly distinguishable (e.g., different icons or labels)

---

### User Story 3 - Mute/Unmute Any Microphone (Priority: P2)

As a user, I want to mute or unmute any microphone individually from the flyout, so that I can control audio privacy for specific devices regardless of which is the default.

**Why this priority**: Muting is critical for privacy. Users may need to mute a specific microphone that an application is using, even if it's not the default device.

**Independent Test**: Can be tested by opening the flyout, clicking the mute button on any microphone, verifying that specific device's mute state changes.

**Acceptance Scenarios**:

1. **Given** the flyout is open, **When** the user clicks the mute button on any microphone, **Then** that specific microphone becomes muted
2. **Given** the flyout is open with a muted microphone, **When** the user clicks the unmute button, **Then** that specific microphone becomes unmuted
3. **Given** the Default Device microphone is muted, **When** viewing the system tray, **Then** the tray icon reflects the muted state
4. **Given** a non-default microphone is muted, **When** viewing the system tray, **Then** the tray icon shows the Default Device's mute state (not affected by non-default)

---

### User Story 4 - System Tray Presence (Priority: P2)

As a user, I want the application to run in the system tray with minimal footprint, so that it is always accessible without cluttering my taskbar or desktop.

**Why this priority**: The system tray presence is essential for quick access and must be implemented alongside P1 functionality. It shares P2 because mute controls depend on tray visibility.

**Independent Test**: Can be tested by launching the application and verifying the tray icon appears, shows correct state, and responds to clicks.

**Acceptance Scenarios**:

1. **Given** the application is launched, **When** it finishes starting up, **Then** a microphone icon appears in the system tray
2. **Given** the application is running in the tray, **When** hovering over the tray icon, **Then** a tooltip shows the current Default Device microphone name (and "(Muted)" if muted)
3. **Given** the application is running, **When** the user right-clicks the tray icon, **Then** a context menu appears with at least an "Exit" option

---

### User Story 5 - Adjust Any Microphone Volume (Priority: P3)

As a user, I want to adjust the volume level of any microphone individually through the flyout, so that I can fine-tune audio input for specific devices that applications may be using.

**Why this priority**: Volume control enhances the user experience. Per-device volume is important because applications may use non-default microphones at different levels.

**Independent Test**: Can be tested by opening the flyout, moving the volume slider on any microphone, and verifying that specific device's system volume changes.

**Acceptance Scenarios**:

1. **Given** the flyout is open, **When** viewing any microphone entry, **Then** it displays a volume slider showing that device's current level (0-100%)
2. **Given** the flyout is open, **When** the user drags the volume slider on any microphone, **Then** that specific microphone's volume changes in real-time
3. **Given** another application changes a microphone's volume, **When** the flyout is open, **Then** that device's slider updates to reflect the new volume
4. **Given** a non-default microphone is being used by an application, **When** adjusting its volume slider, **Then** the application immediately receives audio at the new level

---

### User Story 6 - Real-Time Input Level Monitor for All Devices (Priority: P3)

As a user, I want to see real-time audio input levels for every microphone, so that I can verify which microphones are receiving audio and at what level - especially for devices that applications are actively using.

**Why this priority**: Per-device level meters are critical for troubleshooting. Users often don't know which microphone an application is actually using until they can see activity on the meter.

**Independent Test**: Can be tested by opening the flyout and speaking - all microphones picking up sound should show activity on their individual meters.

**Acceptance Scenarios**:

1. **Given** the flyout is open, **When** viewing any microphone entry, **Then** it displays a real-time input level meter for that specific device
2. **Given** multiple microphones can hear audio, **When** sound is present, **Then** each microphone's meter independently shows its input level
3. **Given** an application is using a non-default microphone, **When** viewing the flyout, **Then** the user can see activity on that specific device's meter
4. **Given** any microphone's meter is displaying, **When** the input level reaches a peak, **Then** a peak indicator holds momentarily before decaying
5. **Given** the meter is displaying, **When** viewing the scale, **Then** dB markers are shown (-60dB to 0dB range)

---

### User Story 7 - Hot-Plug Device Detection (Priority: P4)

As a user, I want the application to automatically detect when microphones are connected or disconnected, so that the device list stays current without restarting the application.

**Why this priority**: Hot-plug detection improves user experience but users can manually refresh or restart the app as a workaround.

**Independent Test**: Can be tested by connecting or disconnecting a USB microphone while the flyout is open and verifying the list updates.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** a new microphone device is connected, **Then** it appears in the microphone list within a few seconds
2. **Given** the application is running, **When** a microphone device is disconnected, **Then** it is removed from the microphone list
3. **Given** a microphone set as default is disconnected, **When** viewing the tray icon tooltip, **Then** it reflects the new default or indicates no microphone available

---

### User Story 8 - Start with Windows (Priority: P5)

As a user, I want the option to have the application start automatically when Windows starts, so that it is always available without manual launch.

**Why this priority**: Auto-start is a convenience feature that can be added after core functionality is complete.

**Independent Test**: Can be tested by enabling the option, restarting the computer, and verifying the application starts automatically.

**Acceptance Scenarios**:

1. **Given** the tray context menu is open, **When** the user clicks "Start with Windows", **Then** the option is enabled and indicated with a checkmark
2. **Given** "Start with Windows" is enabled, **When** Windows starts, **Then** the application launches automatically
3. **Given** "Start with Windows" is enabled, **When** the user clicks the option again, **Then** the auto-start is disabled

---

### User Story 9 - Dock/Undock Flyout Window (Priority: P5)

As a user, I want to be able to dock the flyout as a persistent window, so that I can keep microphone controls visible while working.

**Why this priority**: Docking mode is an advanced preference feature that provides flexibility but is not essential for basic operation.

**Independent Test**: Can be tested by clicking the dock button and verifying the flyout becomes a persistent window that stays open.

**Acceptance Scenarios**:

1. **Given** the flyout is open in tray mode, **When** the user clicks the dock button, **Then** the flyout transforms into a dockable window that persists
2. **Given** the window is in docked mode, **When** the user clicks the undock button, **Then** the window returns to flyout behavior
3. **Given** the window is in docked mode, **When** the user clicks outside the window, **Then** the window remains visible (does not auto-close)

---

### User Story 10 - Display Audio Quality Format (Priority: P3)

As a user, I want to see the audio format (sample rate and bit depth) of each microphone displayed as a tag, so that I can identify the quality capabilities of my devices at a glance.

**Why this priority**: Audio format information helps users make informed decisions when selecting microphones, especially for recording or professional use. It enhances device selection but is not essential for basic switching.

**Independent Test**: Can be tested by opening the flyout and verifying each microphone displays its audio format (e.g., "48kHz/24-bit" or "44.1kHz/16-bit").

**Acceptance Scenarios**:

1. **Given** the flyout is open with microphones listed, **When** viewing a microphone entry, **Then** a format tag displays the sample rate and bit depth (e.g., "48kHz/24-bit")
2. **Given** a microphone supports multiple formats, **When** viewing the device, **Then** the current/default format is displayed
3. **Given** the format information is unavailable, **When** viewing the device, **Then** no format tag is shown (graceful degradation)

---

### User Story 11 - Real-Time Sync with External Changes (Priority: P3)

As a user, I want the application to immediately reflect any audio setting changes made by other programs or Windows settings, so that the displayed state is always accurate.

**Why this priority**: Keeping the UI synchronized with system state is important for user trust and preventing confusion, especially when other applications or system dialogs modify audio settings.

**Independent Test**: Can be tested by opening Windows Sound Settings, changing the default microphone or volume, and verifying the flyout updates without user interaction.

**Acceptance Scenarios**:

1. **Given** the flyout is open, **When** another application changes the default microphone, **Then** the checkmark moves to the new default device within 1 second
2. **Given** the flyout is open, **When** Windows Settings or another app changes the microphone volume, **Then** the volume slider updates to the new value
3. **Given** the flyout is open, **When** another application mutes/unmutes the microphone, **Then** the mute button state and tray icon update accordingly
4. **Given** the flyout is open, **When** another application changes the audio format of the default device, **Then** the format tag updates to reflect the change

---

### Edge Cases

- What happens when no microphones are connected?
  - The flyout displays an inline message: "No microphones detected" and disables selection controls
- What happens when the currently selected default microphone is disconnected?
  - The system will select a new default; the application should reflect this change
- What happens when another application changes the microphone volume?
  - The volume slider should update to reflect the external change
- What happens when the user tries to mute while no microphone is available?
  - The mute button should be disabled with visual indication (grayed out)
- What happens when the Windows audio service is unavailable?
  - The flyout displays an inline error message and retries connection in background
- What happens when setting a default device fails (IPolicyConfig error)?
  - The flyout displays an inline error message; the previous default remains unchanged

## Requirements *(mandatory)*

### Functional Requirements

**Core Display**
- **FR-001**: System MUST display a system tray icon that indicates the Default Device microphone's mute state
- **FR-002**: System MUST show a flyout window when the user left-clicks the tray icon
- **FR-003**: System MUST list all active capture (microphone) devices in the flyout with individual controls

**Per-Device Controls**
- **FR-004**: System MUST display a real-time audio input level meter for EACH microphone device
- **FR-005**: System MUST display a volume slider (0-100%) for EACH microphone device
- **FR-006**: System MUST provide a mute toggle button for EACH microphone device
- **FR-007**: System MUST display the audio format (sample rate and bit depth) tag for EACH microphone device
- **FR-008**: System MUST show a peak-hold indicator on each device's input level meter

**Default Device Selection**
- **FR-009**: System MUST distinguish between "Default Device" (Console) and "Default Communication Device" (Communications)
- **FR-010**: System MUST allow users to set a microphone as Default Device independently
- **FR-011**: System MUST allow users to set a microphone as Default Communication Device independently
- **FR-012**: System MUST provide an option to set a microphone as both Default and Communication Device together
- **FR-013**: System MUST visually indicate Default Device status with a distinct marker (e.g., icon or label)
- **FR-014**: System MUST visually indicate Default Communication Device status with a distinct marker

**Tray Icon Behavior**
- **FR-015**: System MUST update the tray icon to reflect the Default Device's mute/unmute state
- **FR-016**: System MUST display a tooltip on the tray icon showing the Default Device name (and "(Muted)" if muted)
- **FR-017**: System MUST provide a right-click context menu on the tray icon with "Exit" option

**Device Detection**
- **FR-018**: System MUST automatically detect when microphones are connected or disconnected
- **FR-019**: System MUST automatically detect when the default device changes (e.g., changed by another app)

**External Change Sync**
- **FR-020**: System MUST handle volume changes made by external applications and update each device's UI
- **FR-021**: System MUST detect and reflect mute state changes made by external applications (per device)
- **FR-022**: System MUST detect and reflect default device changes made by external applications or Windows Settings
- **FR-023**: System MUST detect and reflect audio format changes made by external applications

**Window Behavior**
- **FR-024**: System MUST provide an option to enable/disable starting with Windows
- **FR-025**: System MUST allow the flyout window to be docked as a persistent window
- **FR-026**: System MUST close the flyout when the user clicks outside it (in tray mode)

### Key Entities

- **Microphone Device**: Represents a capture audio endpoint with:
  - Unique device ID
  - Friendly name
  - Default Device status (Console role - general apps)
  - Default Communication Device status (Communications role - Teams, Zoom, etc.)
  - Mute state
  - Volume level (0-100%)
  - Audio format (sample rate, bit depth, channels)
  - Real-time input level

- **Device Role**: The system-defined role for audio devices:
  - **Console (Default Device)**: Used by games, system sounds, most general applications
  - **Communications (Default Communication Device)**: Used by Teams, Zoom, Discord, and other VoIP applications

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can switch Default Device or Communication Device in under 3 clicks
- **SC-002**: Tray icon state updates within 500ms of Default Device mute state change
- **SC-003**: Device list updates within 2 seconds of a microphone being connected or disconnected
- **SC-004**: All per-device input meters update at least 20 times per second (20Hz refresh rate)
- **SC-005**: Application memory footprint remains under 75MB during normal operation (accounting for per-device audio capture)
- **SC-006**: Application startup time is under 2 seconds on standard hardware
- **SC-007**: Per-device volume slider changes reflect in system volume within 100ms of user interaction
- **SC-008**: 100% of users can identify Default Device mute state from tray icon without opening flyout
- **SC-009**: External setting changes (volume, mute, default device) are reflected in UI within 1 second for all affected devices
- **SC-010**: Audio format tags are displayed for 100% of devices that report format information
- **SC-011**: Users can clearly distinguish which microphone is Default vs Communication Device within 2 seconds of viewing flyout

## Clarifications

### Session 2026-01-01

- Q: Should the flyout support keyboard navigation for accessibility? → A: No keyboard support - mouse-only interaction
- Q: How should errors be communicated to the user? → A: Inline message in the flyout (banner or placeholder text)

## Assumptions

- Target platform is Windows 10 version 1809 or later (Windows 11 recommended)
- Keyboard accessibility is not required; the application is mouse-only for this release
- The application will use undocumented Windows APIs (IPolicyConfig) for setting default devices, as no public API exists
- Users have standard user permissions (no admin required for normal operation)
- Registry access is available for the "Start with Windows" feature
- The Segoe MDL2 Assets font is available for native Windows icons
