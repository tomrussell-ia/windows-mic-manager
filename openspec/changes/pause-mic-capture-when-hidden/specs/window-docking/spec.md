## ADDED Requirements

### Requirement: Minimized Docked Window Suspends Capture

The system SHALL stop microphone capture when the docked window is minimized and SHALL resume capture when the docked window is restored.

#### Scenario: Minimize stops capture
- **WHEN** the docked window is minimized
- **THEN** microphone capture MUST stop for all devices
- **AND** the Windows microphone-in-use indicator MUST clear

#### Scenario: Restore resumes capture
- **WHEN** a minimized docked window is restored
- **THEN** microphone capture MUST resume for all active devices
- **AND** input level meters MUST repopulate
