# input-metering Specification

## Purpose
TBD - created by archiving change document-base-specifications. Update Purpose after archive.
## Requirements
### Requirement: Real-Time Input Level Display

The system SHALL display real-time input level meters for each microphone.

#### Scenario: Meter per device
- **WHEN** viewing any microphone in flyout
- **THEN** real-time input level meter MUST be displayed
- **AND** meter MUST show horizontal bar indicating current level
- **AND** meter MUST update at least 20 times per second (20Hz minimum)

#### Scenario: Independent meter activity
- **WHEN** multiple microphones receive audio
- **THEN** each meter MUST independently show its own input level
- **AND** meters MUST NOT interfere with each other

#### Scenario: Identify active non-default mic
- **WHEN** application uses non-default microphone
- **THEN** user MUST see activity on that specific device's meter
- **AND** this allows troubleshooting audio routing

### Requirement: Peak Hold Indicator

The system SHALL display peak hold indicators on input level meters.

#### Scenario: Peak indicator display
- **WHEN** input level reaches peak
- **THEN** peak indicator MUST display at peak position (e.g., vertical line)
- **AND** peak MUST hold for 2-3 seconds
- **AND** after hold duration, peak MUST decay/disappear
- **AND** new peaks MUST update indicator position

### Requirement: OBS-Style Color Zones

The system SHALL use color coding to indicate signal strength and clipping risk.

#### Scenario: Color-coded zones
- **WHEN** displaying input level meter
- **THEN** meter MUST use color zones:
  - **Green**: -90 dBFS to -20 dBFS (safe)
  - **Yellow**: -20 dBFS to -9 dBFS (approaching limit)
  - **Red**: -9 dBFS to 0 dBFS (clipping risk)
- **AND** color MUST transition immediately when crossing boundaries

### Requirement: Meter Performance

The system SHALL maintain performance while displaying multiple meters.

#### Scenario: Meter update frequency
- **WHEN** displaying multiple device meters
- **THEN** all meters MUST update at least 20Hz
- **AND** meters MUST maintain smooth animation (preferably 60 FPS)
- **AND** UI MUST remain responsive (no lag)

#### Scenario: Memory impact
- **WHEN** monitoring multiple devices
- **THEN** application memory MUST remain under 75MB
- **AND** CPU usage for metering MUST NOT exceed 2%

