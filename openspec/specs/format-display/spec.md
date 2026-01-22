# format-display Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Audio Format Tag Display

The system SHALL display audio format information for each microphone as a tag.

#### Scenario: Format tag shows sample rate, bit depth, channels
- **WHEN** viewing microphone entry in flyout
- **THEN** format tag MUST display in pattern: "{rate} kHz {bits}-bit {channels}"
- **AND** example formats: "48 kHz 16-bit Mono", "44.1 kHz 24-bit Stereo"

#### Scenario: Current format displayed
- **WHEN** microphone supports multiple audio formats
- **THEN** format tag MUST display current/default format Windows is using
- **AND** not all possible formats

### Requirement: Format Unavailable Handling

The system SHALL handle cases where format information is unavailable gracefully.

#### Scenario: Format unavailable
- **WHEN** audio format cannot be determined
- **THEN** no format tag SHALL be displayed
- **AND** no error message SHALL be shown (graceful degradation)
- **AND** device entry MUST still be functional

### Requirement: Format Change Detection

The system SHALL detect when audio format changes externally.

#### Scenario: External format change
- **WHEN** user changes audio format in Windows Settings
- **THEN** format tag MUST update within 1 second
- **AND** metering MUST adapt to new format automatically

### Requirement: Format Coverage

The system SHALL display format tags for devices that report format information.

#### Scenario: Format display coverage
- **WHEN** devices report format information
- **THEN** format tags MUST be displayed for 100% of such devices
- **AND** format parsing MUST support common Windows audio formats

