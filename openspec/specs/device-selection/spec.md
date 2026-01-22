# device-selection Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Independent Role Selection

The system SHALL allow users to set Default Device and Default Communication Device independently.

#### Scenario: Display separate role indicators
- **WHEN** viewing any microphone in flyout
- **THEN** device MUST show indicator if it is Default Device
- **AND** device MUST show separate indicator if it is Default Communication Device
- **AND** indicators MUST be visually distinct (different icons or labels)

#### Scenario: Set Default Device only
- **WHEN** user clicks "Set as Default" on a microphone
- **THEN** only the Default Device (Console) role MUST change
- **AND** Default Communication Device role MUST remain unchanged
- **AND** operation MUST complete within 500ms

#### Scenario: Set Default Communication Device only
- **WHEN** user clicks "Set as Communication" on a microphone
- **THEN** only the Default Communication Device (Communications) role MUST change
- **AND** Default Device role MUST remain unchanged
- **AND** operation MUST complete within 500ms

#### Scenario: Same device for both roles
- **WHEN** user wants same microphone for both Default and Communication
- **THEN** user MUST be able to click both buttons to set both roles
- **AND** both role indicators MUST be displayed on that device

#### Scenario: Different devices for different roles
- **WHEN** Microphone A is Default and Microphone B is Communication
- **THEN** general apps MUST use Microphone A by default
- **AND** VoIP apps (Teams, Zoom) MUST use Microphone B by default
- **AND** both devices MUST show their respective indicators clearly

### Requirement: Error Handling for Selection Failures

The system SHALL display error message when setting default device fails.

#### Scenario: Set default operation fails
- **WHEN** setting default device fails (Windows API error)
- **THEN** flyout MUST display inline error banner
- **AND** error MUST auto-dismiss after 5 seconds
- **AND** previous default device MUST remain unchanged

### Requirement: Role Distinction Clarity

The system SHALL make it easy for users to understand which device has which role.

#### Scenario: Distinguish roles within 2 seconds
- **WHEN** user views flyout for first time
- **THEN** user MUST clearly distinguish Default vs Communication Device within 2 seconds
- **AND** visual indicators MUST be unambiguous

