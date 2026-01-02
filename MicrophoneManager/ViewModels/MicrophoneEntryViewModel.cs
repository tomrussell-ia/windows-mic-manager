using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.Models;
using MicrophoneManager.Services;

namespace MicrophoneManager.ViewModels;

public partial class MicrophoneEntryViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private bool _suppressVolumeWrite;
    private DateTime _peakHoldUntilUtc;
    private DateTime _lastPeakTickUtc;

    private const int PeakHoldMilliseconds = 750;
    private const double PeakDecayPercentPerSecond = 35.0;

    public MicrophoneEntryViewModel(MicrophoneDevice device, IAudioDeviceService audioService)
    {
        _audioService = audioService;
        _lastPeakTickUtc = DateTime.UtcNow;
        UpdateFrom(device);
    }

    public string Id { get; private set; } = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isDefaultCommunication;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _volumePercent;

    [ObservableProperty]
    private string _formatTag = string.Empty;

    [ObservableProperty]
    private double _inputLevelPercent;

    [ObservableProperty]
    private double _peakLevelPercent;

    public void UpdateFrom(MicrophoneDevice device)
    {
        Id = device.Id;
        Name = device.Name;
        IsDefault = device.IsDefault;
        IsDefaultCommunication = device.IsDefaultCommunication;
        IsMuted = device.IsMuted;
        ApplyVolumeFromSystem(device.VolumeLevel * 100.0);
        FormatTag = device.FormatTag;
        UpdateMeter(device.InputLevelPercent);
    }

    public void UpdateMeter(double inputPercent)
    {
        var clamped = Math.Max(0, Math.Min(100.0, inputPercent));
        InputLevelPercent = clamped;
        var nowUtc = DateTime.UtcNow;

        if (clamped >= PeakLevelPercent)
        {
            PeakLevelPercent = clamped;
            _peakHoldUntilUtc = nowUtc.AddMilliseconds(PeakHoldMilliseconds);
        }

        TickPeak(nowUtc);
    }

    public void TickPeak(DateTime nowUtc)
    {
        var dt = nowUtc - _lastPeakTickUtc;
        _lastPeakTickUtc = nowUtc;

        if (dt.TotalSeconds <= 0) return;
        if (nowUtc <= _peakHoldUntilUtc) return;

        var decay = PeakDecayPercentPerSecond * dt.TotalSeconds;
        var newPeak = PeakLevelPercent - decay;
        newPeak = Math.Max(InputLevelPercent, newPeak);
        newPeak = Math.Max(0.0, Math.Min(100.0, newPeak));

        if (Math.Abs(newPeak - PeakLevelPercent) > 0.001)
        {
            PeakLevelPercent = newPeak;
        }
    }

    public void ApplyVolumeFromSystem(double percent)
    {
        _suppressVolumeWrite = true;
        try
        {
            VolumePercent = percent;
        }
        finally
        {
            _suppressVolumeWrite = false;
        }
    }

    [RelayCommand]
    private void SetDefault()
    {
        _audioService.SetMicrophoneForRole(Id, NAudio.CoreAudioApi.Role.Console);
    }

    [RelayCommand]
    private void SetDefaultCommunication()
    {
        _audioService.SetMicrophoneForRole(Id, NAudio.CoreAudioApi.Role.Communications);
    }

    [RelayCommand]
    private void SetBoth()
    {
        _audioService.SetDefaultMicrophone(Id);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = _audioService.ToggleMute(Id);
    }

    partial void OnVolumePercentChanged(double value)
    {
        if (_suppressVolumeWrite) return;
        var clamped = Math.Max(0.0, Math.Min(100.0, value));
        _audioService.SetMicrophoneVolumeLevelScalar(Id, (float)(clamped / 100.0));
    }
}
