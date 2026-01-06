using MicrophoneManager.WinUI.Models;
using NAudio.CoreAudioApi;

namespace MicrophoneManager.WinUI.Services;

public interface IAudioDeviceService : IDisposable
{
    event EventHandler? DevicesChanged;
    event EventHandler? DefaultDeviceChanged;
    event EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    event EventHandler<AudioDeviceService.MicrophoneVolumeChangedEventArgs>? MicrophoneVolumeChanged;
    event EventHandler<AudioDeviceService.DefaultMicrophoneInputLevelChangedEventArgs>? DefaultMicrophoneInputLevelChanged;
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

    // Async methods to prevent UI thread blocking
    Task<List<MicrophoneDevice>> GetMicrophonesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultDeviceIdAsync(Role role, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultMicrophoneAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> SetMicrophoneForRoleAsync(string deviceId, Role role, CancellationToken cancellationToken = default);
    Task<bool> ToggleMuteAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default);
}
