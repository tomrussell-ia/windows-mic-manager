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
    private DateTime _lastMeterUpdateUtc;

    private double _peakDbFs = -96.0;
    private double _smoothedDbFs = -96.0;

    private const int PeakHoldMilliseconds = 5000;
    private const double PeakDecayDbPerSecond = 20.0;

    // OBS-style ballistics: instant attack, exponential release (~300ms time constant).
    private const double MeterReleaseTimeMs = 300.0;

    public MicrophoneEntryViewModel(MicrophoneDevice device, IAudioDeviceService audioService)
    {
        _audioService = audioService;
        _lastPeakTickUtc = DateTime.UtcNow;
        _lastMeterUpdateUtc = DateTime.UtcNow;
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
        ApplyVolumeFromSystem(Math.Round(device.VolumeLevel * 100.0, 2));
        FormatTag = device.FormatTag;
        UpdateMeter(device.InputLevelPercent);
    }

    public void UpdateMeter(double inputPercent)
    {
        var clamped = Math.Max(0, Math.Min(100.0, inputPercent));
        var nowUtc = DateTime.UtcNow;
        var dtMs = (nowUtc - _lastMeterUpdateUtc).TotalMilliseconds;
        _lastMeterUpdateUtc = nowUtc;

        // Interpret UI percent as OBS deflection (0..100) and run peak-hold/decay in dB.
        var inputDbFs = Services.ObsMeterMath.PercentToDb(clamped);

        // OBS-style ballistics: instant attack, exponential release.
        if (inputDbFs >= _smoothedDbFs)
        {
            _smoothedDbFs = inputDbFs;
        }
        else if (dtMs > 0)
        {
            var alpha = 1.0 - Math.Exp(-dtMs / MeterReleaseTimeMs);
            _smoothedDbFs += (inputDbFs - _smoothedDbFs) * alpha;
        }

        var smoothedPercent = Services.ObsMeterMath.DbToPercent(_smoothedDbFs);
        InputLevelPercent = smoothedPercent;

        // Peak hold tracks the RAW input (not smoothed) so transients register.
        if (inputDbFs >= _peakDbFs)
        {
            _peakDbFs = inputDbFs;
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

        // Decay the peak in dB/sec, then convert back to UI deflection percent.
        _peakDbFs -= PeakDecayDbPerSecond * dt.TotalSeconds;

        var inputDbFs = Services.ObsMeterMath.PercentToDb(InputLevelPercent);
        _peakDbFs = Math.Max(inputDbFs, _peakDbFs);
        _peakDbFs = Services.ObsMeterMath.ClampMeterDb(_peakDbFs);

        var newPeakPercent = Services.ObsMeterMath.DbToPercent(_peakDbFs);
        if (Math.Abs(newPeakPercent - PeakLevelPercent) > 0.001)
        {
            PeakLevelPercent = newPeakPercent;
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
