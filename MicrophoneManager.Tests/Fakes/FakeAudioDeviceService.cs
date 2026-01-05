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

    public void SetDefaultMicrophone(string deviceId)
    {
        SetMicrophoneForRole(deviceId, Role.Console);
        SetMicrophoneForRole(deviceId, Role.Communications);
    }

    public void SetMicrophoneForRole(string deviceId, Role role)
    {
        if (!_microphones.ContainsKey(deviceId)) return;

        if (role == Role.Console)
        {
            DefaultConsoleId = deviceId;
        }
        else if (role == Role.Communications)
        {
            DefaultCommunicationsId = deviceId;
        }

        DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
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
