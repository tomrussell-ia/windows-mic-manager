# hot-plug-detection Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Device Connection Detection

The system SHALL automatically detect when microphones are connected.

#### Scenario: Device connected
- **WHEN** user connects new microphone (e.g., plugs in USB mic)
- **THEN** device MUST appear in flyout list within 2 seconds
- **AND** device MUST show current properties (volume, mute, format)

#### Scenario: Flyout updates automatically
- **WHEN** flyout is open and device is connected
- **THEN** device list MUST update automatically without closing flyout
- **AND** user SHALL NOT need to manually refresh

### Requirement: Device Disconnection Detection

The system SHALL automatically detect when microphones are disconnected.

#### Scenario: Device disconnected
- **WHEN** user disconnects microphone
- **THEN** device MUST be removed from flyout list immediately
- **AND** if device was selected, selection MUST clear

#### Scenario: Default device disconnected
- **WHEN** currently selected default microphone is disconnected
- **THEN** Windows MUST automatically select new default
- **AND** flyout MUST reflect new default within 1 second
- **AND** tray icon tooltip MUST update to show new default name

### Requirement: Detection Speed

The system SHALL detect device changes within measurable time bounds.

#### Scenario: Detection within 2 seconds
- **WHEN** microphone is connected or disconnected
- **THEN** device list MUST update within 2 seconds
- **AND** detection MUST use Windows IMMNotificationClient callbacks (not polling)

