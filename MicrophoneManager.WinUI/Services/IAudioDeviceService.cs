using MicrophoneManager.WinUI.Models;
using NAudio.CoreAudioApi;

namespace MicrophoneManager.WinUI.Services;

public interface IAudioDeviceService : IDisposable
{
    event EventHandler? DevicesChanged;
    event EventHandler? DefaultDeviceChanged;
    event EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    event EventHandler<AudioDeviceService.MicrophoneVolumeChangedEventArgs>? MicrophoneVolumeChanged;
    event EventHandler<AudioDeviceService.MicrophoneInputLevelChangedEventArgs>? MicrophoneInputLevelChanged;
    event EventHandler<AudioDeviceService.MicrophoneFormatChangedEventArgs>? MicrophoneFormatChanged;

    List<MicrophoneDevice> GetMicrophones();
    string? GetDefaultDeviceId(Role role);
    MicrophoneDevice? GetDefaultMicrophone();
    bool SetDefaultMicrophone(string deviceId);
    bool SetMicrophoneForRole(string deviceId, Role role);
    void SetDefaultMicrophoneVolumePercent(double volumePercent);
    void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar);
    bool ToggleMute(string deviceId);
    bool IsMuted(string deviceId);
    bool ToggleDefaultMicrophoneMute();
    bool IsDefaultMicrophoneMuted();

    /// <summary>
    /// Signals that a metering UI is visible. Reference-counted: capture starts on the
    /// first acquire and only stops once every acquire has a matching release.
    /// </summary>
    void AcquireMetering();

    /// <summary>
    /// Signals that a metering UI is no longer visible. Stops capture once the last
    /// outstanding acquire is released.
    /// </summary>
    void ReleaseMetering();

    // Async methods to prevent UI thread blocking
    Task<List<MicrophoneDevice>> GetMicrophonesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultDeviceIdAsync(Role role, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultMicrophoneAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> SetMicrophoneForRoleAsync(string deviceId, Role role, CancellationToken cancellationToken = default);
    Task<bool> ToggleMuteAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default);
}
