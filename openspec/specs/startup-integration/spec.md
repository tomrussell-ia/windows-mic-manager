# startup-integration Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Start with Windows Option

The system SHALL provide toggleable option to launch automatically at Windows startup.

#### Scenario: Enable auto-start
- **WHEN** user right-clicks tray icon and opens context menu
- **THEN** menu MUST include "Start with Windows" option
- **WHEN** user clicks to enable
- **THEN** option MUST show checkmark
- **AND** application MUST register in Windows startup registry
- **AND** setting MUST persist across application restarts

#### Scenario: Disable auto-start
- **WHEN** "Start with Windows" is enabled
- **AND** user clicks option again
- **THEN** checkmark MUST disappear
- **AND** application MUST remove itself from startup registry
- **AND** application SHALL NOT launch on next Windows boot

### Requirement: Auto-Launch on Boot

The system SHALL launch automatically when Windows starts if enabled.

#### Scenario: Launch on Windows startup
- **WHEN** "Start with Windows" is enabled
- **AND** Windows starts
- **THEN** application MUST launch automatically
- **AND** tray icon MUST appear within 5-10 seconds of desktop ready
- **AND** application MUST be fully functional after auto-start

### Requirement: Startup Status Checking

The system SHALL check and reflect actual startup status.

#### Scenario: Check registry status
- **WHEN** application launches
- **THEN** application MUST check if startup is enabled in registry
- **AND** context menu checkmark MUST reflect actual state
- **AND** if registry entry exists but wrong path, application SHOULD update it

### Requirement: Standard User Permissions

The system SHALL enable startup without requiring administrator privileges.

#### Scenario: Work with standard permissions
- **WHEN** enabling or disabling startup
- **THEN** operation MUST work with standard user permissions
- **AND** registry key MUST be in HKEY_CURRENT_USER (not HKEY_LOCAL_MACHINE)
- **AND** if registry access fails, error banner MUST be shown

