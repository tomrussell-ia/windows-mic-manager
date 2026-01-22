# microphone-display Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Display All Active Microphones

The system SHALL display all active capture devices in a flyout window accessible from the system tray.

#### Scenario: Open flyout to view microphones
- **WHEN** user left-clicks the system tray icon
- **THEN** flyout window MUST appear near the tray icon
- **AND** flyout MUST list all active microphones
- **AND** flyout MUST appear within 200ms of click

#### Scenario: Flyout closes on focus loss
- **WHEN** flyout is open and user clicks outside the flyout
- **THEN** flyout MUST close automatically

#### Scenario: Device list updates within time bound
- **WHEN** flyout opens
- **THEN** device list MUST be fully populated within 300ms

### Requirement: Per-Device Control Display

The system SHALL display individual controls for each microphone including level meter, volume slider, mute button, and format tag.

#### Scenario: Each device shows all controls
- **WHEN** viewing any microphone entry in the flyout
- **THEN** entry MUST display:
  - Device friendly name
  - Real-time input level meter
  - Volume slider (0-100%)
  - Mute toggle button
  - Audio format tag (sample rate, bit depth, channels)

#### Scenario: Controls for non-default devices
- **WHEN** an application is using a non-default microphone
- **THEN** that microphone MUST appear in the list
- **AND** all controls MUST be functional (meter shows activity, volume/mute work)
- **AND** user MUST be able to control it without making it default

### Requirement: Empty State Handling

The system SHALL display appropriate message when no microphones are available.

#### Scenario: No microphones connected
- **WHEN** no microphones are connected to the system
- **THEN** flyout MUST display "No microphones detected" message
- **AND** device controls MUST be disabled or hidden
- **AND** no error banner SHALL be shown (expected state)

