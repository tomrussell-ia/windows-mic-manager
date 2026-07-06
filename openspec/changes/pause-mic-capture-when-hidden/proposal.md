# Change: Suspend microphone capture when no metering UI is visible

## Why
`AudioDeviceService` opens a real `WasapiCapture` stream per active microphone unconditionally at startup and never stops it while the process is alive, purely to drive the VU meters. This means the app holds the microphone open (and the Windows mic-in-use privacy indicator stays lit) for its entire runtime, even when the tray flyout is closed, no window is open, or a docked window is minimized. A tray-only app should not hold a system resource like the microphone when nothing needs it.

## What Changes
- `AudioDeviceService` no longer starts capture in its constructor; capture is now reference-counted and only starts/stops via new `AcquireMetering()` / `ReleaseMetering()` methods on `IAudioDeviceService`.
- `MicrophoneListViewModel.SetMeteringEnabled(bool)` now calls `AcquireMetering()`/`ReleaseMetering()` in addition to its existing UI peak-hold timer toggle, so capture tracks actual UI visibility.
- The docked window (a normal minimizable window) now detects minimize/restore and suspends/resumes capture accordingly; the flyout already suspends on close/deactivate.
- Device enumeration, mute/volume tracking, and the tray tooltip's default-device tracking are unaffected — they use `AudioEndpointVolume`, not capture, and continue running for the whole process lifetime.

## Impact
- Affected specs: `input-metering`, `window-docking`
- Affected code: `MicrophoneManager.WinUI/Services/AudioDeviceService.cs`, `MicrophoneManager.WinUI/Services/IAudioDeviceService.cs`, `MicrophoneManager.WinUI/ViewModels/MicrophoneListViewModel.cs`, `MicrophoneManager.WinUI/Views/MicrophoneWindow.xaml.cs`
