using MicrophoneManager.WinUI.Models;
using MicrophoneManager.WinUI.Services;
using NAudio.CoreAudioApi;

namespace MicrophoneManager.Tests.Fakes;

public class FakeAudioDeviceService : IAudioDeviceService
{
    private readonly Dictionary<string, FakeMicrophone> _microphones = new();

    public string? DefaultConsoleId { get; set; }
    public string? DefaultCommunicationsId { get; set; }

    public event EventHandler? DevicesChanged;
    public event EventHandler? DefaultDeviceChanged;
    public event EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    public event EventHandler<AudioDeviceService.MicrophoneVolumeChangedEventArgs>? MicrophoneVolumeChanged;
    public event EventHandler<AudioDeviceService.DefaultMicrophoneInputLevelChangedEventArgs>? DefaultMicrophoneInputLevelChanged;
    public event EventHandler<AudioDeviceService.MicrophoneFormatChangedEventArgs>? MicrophoneFormatChanged;

    public void AddOrUpdateMicrophone(FakeMicrophone microphone)
    {
        _microphones[microphone.Id] = microphone;
    }

    public void RemoveMicrophone(string id)
    {
        _microphones.Remove(id);
    }

    public List<MicrophoneDevice> GetMicrophones()
    {
        return _microphones.Values
            .Select(m => m.ToSnapshot(m.Id == DefaultConsoleId, m.Id == DefaultCommunicationsId))
            .ToList();
    }

    public string? GetDefaultDeviceId(Role role)
    {
        return role == Role.Console ? DefaultConsoleId : DefaultCommunicationsId;
    }

    public MicrophoneDevice? GetDefaultMicrophone()
    {
        return GetMicrophones().FirstOrDefault(m => m.Id == DefaultConsoleId);
    }

    public bool SetDefaultMicrophone(string deviceId)
    {
        var consoleSuccess = SetMicrophoneForRole(deviceId, Role.Console);
        var commSuccess = SetMicrophoneForRole(deviceId, Role.Communications);
        return consoleSuccess && commSuccess;
    }

    public bool SetMicrophoneForRole(string deviceId, Role role)
    {
        if (!_microphones.ContainsKey(deviceId)) return false;

        if (role == Role.Console)
        {
            DefaultConsoleId = deviceId;
        }
        else if (role == Role.Communications)
        {
            DefaultCommunicationsId = deviceId;
        }

        DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetDefaultMicrophoneVolumePercent(double volumePercent)
    {
        var defaultId = DefaultConsoleId;
        if (defaultId == null) return;

        SetMicrophoneVolumeLevelScalar(defaultId, (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0));
        if (_microphones.TryGetValue(defaultId, out var mic))
        {
            DefaultMicrophoneVolumeChanged?.Invoke(
                this,
                new AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs(defaultId, (float)mic.VolumeScalar, mic.IsMuted));
        }
    }

    public void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar)
    {
        if (_microphones.TryGetValue(deviceId, out var mic))
        {
            mic.VolumeScalar = Math.Clamp(volumeLevelScalar, 0.0f, 1.0f);
        }
    }

    public bool ToggleMute(string deviceId)
    {
        if (!_microphones.TryGetValue(deviceId, out var mic)) return false;

        mic.IsMuted = !mic.IsMuted;
        return mic.IsMuted;
    }

    public bool IsMuted(string deviceId)
    {
        return _microphones.TryGetValue(deviceId, out var mic) && mic.IsMuted;
    }

    public bool ToggleDefaultMicrophoneMute()
    {
        var defaultId = DefaultConsoleId;
        if (defaultId == null) return false;

        var isMuted = ToggleMute(defaultId);
        if (_microphones.TryGetValue(defaultId, out var mic))
        {
            DefaultMicrophoneVolumeChanged?.Invoke(
                this,
                new AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs(defaultId, (float)mic.VolumeScalar, isMuted));
        }

        return isMuted;
    }

    public bool IsDefaultMicrophoneMuted()
    {
        var defaultId = DefaultConsoleId;
        if (defaultId == null) return false;

        return IsMuted(defaultId);
    }

    public void RaiseDevicesChanged()
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseDefaultDeviceChanged()
    {
        DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseDefaultVolumeChanged(string deviceId, float volumeLevelScalar, bool isMuted)
    {
        DefaultMicrophoneVolumeChanged?.Invoke(
            this,
            new AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs(deviceId, volumeLevelScalar, isMuted));

        MicrophoneVolumeChanged?.Invoke(
            this,
            new AudioDeviceService.MicrophoneVolumeChangedEventArgs(deviceId, volumeLevelScalar, isMuted));
    }

    public void RaiseMicrophoneVolumeChanged(string deviceId, float volumeLevelScalar, bool isMuted)
    {
        MicrophoneVolumeChanged?.Invoke(
            this,
            new AudioDeviceService.MicrophoneVolumeChangedEventArgs(deviceId, volumeLevelScalar, isMuted));
    }

    public void RaiseInputLevelChanged(string deviceId, double inputPercent, double inputDbFs)
    {
        DefaultMicrophoneInputLevelChanged?.Invoke(
            this,
            new AudioDeviceService.DefaultMicrophoneInputLevelChangedEventArgs(deviceId, inputPercent, inputDbFs));
    }

    public void RaiseFormatChanged(string deviceId, string formatTag)
    {
        MicrophoneFormatChanged?.Invoke(
            this,
            new AudioDeviceService.MicrophoneFormatChangedEventArgs(deviceId, formatTag));
    }

    // Async methods - in tests, these just wrap synchronous versions
    public Task<List<MicrophoneDevice>> GetMicrophonesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetMicrophones());
    }

    public Task<string?> GetDefaultDeviceIdAsync(Role role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetDefaultDeviceId(role));
    }

    public Task<bool> SetDefaultMicrophoneAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SetDefaultMicrophone(deviceId));
    }

    public Task<bool> SetMicrophoneForRoleAsync(string deviceId, Role role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SetMicrophoneForRole(deviceId, role));
    }

    public Task<bool> ToggleMuteAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ToggleMute(deviceId));
    }

    public Task<bool> ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ToggleDefaultMicrophoneMute());
    }

    public void Dispose()
    {
    }

    public class FakeMicrophone
    {
        public FakeMicrophone(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public bool IsMuted { get; set; }
        public double VolumeScalar { get; set; } = 1.0;
        public string FormatTag { get; set; } = "48 kHz 24-bit Stereo";
        public double InputLevelPercent { get; set; }

        public MicrophoneDevice ToSnapshot(bool isDefault, bool isDefaultCommunication)
        {
            return new MicrophoneDevice
            {
                Id = Id,
                Name = Name,
                IsMuted = IsMuted,
                IsDefault = isDefault,
                IsDefaultCommunication = isDefaultCommunication,
                VolumeLevel = (float)VolumeScalar,
                FormatTag = FormatTag,
                InputLevelPercent = InputLevelPercent
            };
        }
    }
}
