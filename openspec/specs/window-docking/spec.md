# window-docking Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Dock Flyout as Persistent Window

The system SHALL allow transforming the flyout into a dockable persistent window.

#### Scenario: Dock button available
- **WHEN** viewing flyout in tray mode (default)
- **THEN** flyout MUST display "Dock" button in header
- **AND** button MUST be clearly labeled or use recognizable icon

#### Scenario: Dock flyout
- **WHEN** flyout is open and user clicks "Dock" button
- **THEN** flyout MUST transform into dockable window
- **AND** window MUST remain persistent (not auto-close on focus loss)
- **AND** window MUST remain topmost
- **AND** "Dock" button MUST change to "Undock" button

### Requirement: Docked Window Persistence

The system SHALL keep docked window open when user clicks outside.

#### Scenario: Docked window stays open
- **WHEN** window is docked
- **AND** user clicks outside window
- **THEN** window MUST remain visible and open
- **AND** window SHALL NOT auto-close

### Requirement: Undock to Flyout Mode

The system SHALL allow returning docked window to flyout behavior.

#### Scenario: Undock window
- **WHEN** window is docked and user clicks "Undock" button
- **THEN** window MUST return to flyout behavior
- **AND** window MUST auto-close on focus loss (resume tray mode)
- **AND** "Undock" button MUST change back to "Dock" button

### Requirement: Tray Icon with Docked Window

The system SHALL maintain tray icon functionality when window is docked.

#### Scenario: Tray icon remains functional
- **WHEN** flyout is docked as persistent window
- **THEN** tray icon MUST remain in system tray
- **AND** clicking tray icon MUST toggle docked window visibility
- **AND** "Exit" in context menu MUST close application

