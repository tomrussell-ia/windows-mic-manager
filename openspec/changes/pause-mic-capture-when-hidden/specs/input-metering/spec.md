## ADDED Requirements

### Requirement: Microphone Capture Suspended When Not Visible

The system SHALL stop all live microphone capture (the WASAPI streams that feed input level meters) when no metering UI is visible, and SHALL resume capture when a metering UI becomes visible again.

#### Scenario: No capture at idle startup
- **WHEN** the application starts and no flyout or docked window is open
- **THEN** no microphone capture stream MUST be active
- **AND** the Windows microphone-in-use indicator MUST NOT be shown for the application

#### Scenario: Capture starts when flyout opens
- **WHEN** the user opens the flyout from the tray icon
- **THEN** microphone capture MUST start for all active devices
- **AND** input level meters MUST begin updating

#### Scenario: Capture stops when flyout closes
- **WHEN** the flyout closes or loses focus
- **THEN** microphone capture MUST stop for all devices
- **AND** the Windows microphone-in-use indicator MUST clear
