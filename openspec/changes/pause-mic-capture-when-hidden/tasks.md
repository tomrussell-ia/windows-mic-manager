## 1. Service capture gating
- [x] 1.1 Add `AcquireMetering()` / `ReleaseMetering()` to `IAudioDeviceService`
- [x] 1.2 Implement reference-counted capture start/stop in `AudioDeviceService`, extracting a shared `StopAllCaptures()` helper used by `ReleaseMetering()` and `Dispose()`
- [x] 1.3 Remove the unconditional capture start from the `AudioDeviceService` constructor
- [x] 1.4 Gate capture creation in `UpdateAllMicrophoneMeterSubscriptionsAsync()` on the metering ref count so the poll/format-change auto-heal paths cannot resurrect capture while metering is off

## 2. View model wiring
- [x] 2.1 Call `AcquireMetering()`/`ReleaseMetering()` from `MicrophoneListViewModel.SetMeteringEnabled(bool)`
- [x] 2.2 Verify `Dispose()` releases metering correctly when disposed while enabled

## 3. Docked window minimize/restore
- [x] 3.1 Keep a reference to the docked `OverlappedPresenter` and observe minimize/restore transitions
- [x] 3.2 Call `SetMeteringEnabled(false)` on minimize and `SetMeteringEnabled(true)` on restore
- [x] 3.3 Unsubscribe cleanly on window close

## 4. Tests
- [x] 4.1 Add/update `MicrophoneListViewModelTests` to assert `SetMeteringEnabled` acquires/releases metering exactly once per transition
