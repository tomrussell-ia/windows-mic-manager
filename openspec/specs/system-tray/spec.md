# system-tray Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Tray Icon Presence

The system SHALL display an icon in the Windows system tray notification area.

#### Scenario: Icon appears on startup
- **WHEN** application is launched
- **THEN** microphone icon MUST appear in system tray
- **AND** icon MUST appear within 2 seconds of launch
- **AND** icon MUST use native Windows styling (Segoe MDL2 Assets font)

#### Scenario: No taskbar presence
- **WHEN** application is running
- **THEN** application MUST NOT appear in Windows taskbar
- **AND** only system tray icon SHALL be visible

#### Scenario: Application exit
- **WHEN** user selects "Exit" from context menu
- **THEN** tray icon MUST disappear within 1 second
- **AND** no ghost icon SHALL remain in tray

### Requirement: Tray Icon Tooltip

The system SHALL display tooltip showing current Default Device name and mute status.

#### Scenario: Tooltip shows device name
- **WHEN** user hovers over tray icon
- **THEN** tooltip MUST show Default Device name
- **AND** if muted, tooltip MUST append "(Muted)"
- **AND** format: "{Device Name}" or "{Device Name} (Muted)"

#### Scenario: Tooltip updates on change
- **WHEN** default device changes or mute state changes
- **THEN** tooltip MUST update to reflect new state

### Requirement: Tray Icon Interactions

The system SHALL respond to left-click and right-click on tray icon.

#### Scenario: Left-click opens flyout
- **WHEN** user left-clicks tray icon
- **THEN** flyout window MUST open within 200ms
- **AND** flyout MUST be positioned near tray icon

#### Scenario: Right-click shows context menu
- **WHEN** user right-clicks tray icon
- **THEN** context menu MUST appear with:
  - "Open" (opens flyout)
  - "Start with Windows" (toggleable with checkmark)
  - "Exit" (close application)
- **AND** menu MUST use standard Windows styling

