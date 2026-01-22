# external-sync Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Default Device Change Sync

The system SHALL detect and reflect default device changes made externally.

#### Scenario: External default change
- **WHEN** user changes default microphone in Windows Settings
- **THEN** application MUST detect change within 100ms (via Windows notification)
- **AND** flyout device list MUST update default indicator within 200ms
- **AND** tray icon MUST update if new default has different mute state
- **AND** tray tooltip MUST update to show new default device name

### Requirement: Volume Change Sync

The system SHALL detect and reflect volume changes made externally.

#### Scenario: External volume change
- **WHEN** user changes microphone volume in Windows Settings or another app
- **THEN** application MUST detect change within 1 second (via polling)
- **AND** flyout volume slider MUST update to show new value
- **AND** slider update MUST NOT trigger additional volume change (prevent feedback loop)

### Requirement: Mute State Sync

The system SHALL detect and reflect mute state changes made externally.

#### Scenario: External mute change
- **WHEN** user mutes/unmutes microphone in Windows Settings or another app
- **THEN** application MUST detect change within 1 second (via polling)
- **AND** flyout mute button state MUST update
- **AND** if default device, tray icon MUST update to reflect new mute state
- **AND** tray tooltip MUST update "(Muted)" suffix accordingly

### Requirement: Audio Format Change Sync

The system SHALL detect and reflect audio format changes made externally.

#### Scenario: External format change
- **WHEN** user changes audio format in Windows Settings
- **THEN** application MUST detect change within 1 second
- **AND** flyout format tag MUST update to show new format

### Requirement: Rapid Change Handling

The system SHALL handle multiple rapid external changes without freezing.

#### Scenario: Multiple rapid changes
- **WHEN** multiple external changes occur rapidly
- **THEN** application MUST handle all changes without freezing
- **AND** final state MUST match actual Windows state after changes settle
- **AND** debouncing (50ms) MAY be used to prevent excessive UI updates

### Requirement: Sync Speed Guarantee

The system SHALL reflect external changes within measurable time bound.

#### Scenario: External change sync within 1 second
- **WHEN** any external audio setting change occurs
- **THEN** application MUST reflect change in UI within 1 second for all affected devices
- **AND** no stale information SHALL be displayed

