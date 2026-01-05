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

    List<MicrophoneDevice> GetMicrophones();
    string? GetDefaultDeviceId(Role role);
    MicrophoneDevice? GetDefaultMicrophone();
    void SetDefaultMicrophone(string deviceId);
    void SetMicrophoneForRole(string deviceId, Role role);
    void SetDefaultMicrophoneVolumePercent(double volumePercent);
    void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar);
    bool ToggleMute(string deviceId);
    bool IsMuted(string deviceId);
    bool ToggleDefaultMicrophoneMute();
    bool IsDefaultMicrophoneMuted();
}
