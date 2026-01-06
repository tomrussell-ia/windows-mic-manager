using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.WinUI.Models;
using MicrophoneManager.WinUI.Services;

namespace MicrophoneManager.WinUI.ViewModels;

public partial class MicrophoneEntryViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private readonly Action<string>? _onError;
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

    public MicrophoneEntryViewModel(MicrophoneDevice device, IAudioDeviceService audioService, Action<string>? onError = null)
    {
        _audioService = audioService;
        _onError = onError;
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
    private double _inputLevelDbFs;

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
        var inputDbFs = MicrophoneManager.WinUI.Services.ObsMeterMath.PercentToDb(clamped);

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

        var smoothedPercent = MicrophoneManager.WinUI.Services.ObsMeterMath.DbToPercent(_smoothedDbFs);
        InputLevelPercent = smoothedPercent;
        InputLevelDbFs = MicrophoneManager.WinUI.Services.ObsMeterMath.ClampMeterDb(_smoothedDbFs);

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

        var inputDbFs = MicrophoneManager.WinUI.Services.ObsMeterMath.PercentToDb(InputLevelPercent);
        _peakDbFs = Math.Max(inputDbFs, _peakDbFs);
        _peakDbFs = MicrophoneManager.WinUI.Services.ObsMeterMath.ClampMeterDb(_peakDbFs);

        var newPeakPercent = MicrophoneManager.WinUI.Services.ObsMeterMath.DbToPercent(_peakDbFs);
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

    [ObservableProperty]
    private bool _isChangingDevice;

    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        if (IsChangingDevice) return;

        try
        {
            IsChangingDevice = true;
            var success = await _audioService.SetMicrophoneForRoleAsync(Id, NAudio.CoreAudioApi.Role.Console, CancellationToken.None);
            if (!success)
            {
                _onError?.Invoke("Failed to set default device");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetDefaultAsync failed: {ex}");
            _onError?.Invoke("Failed to set default device");
        }
        finally
        {
            IsChangingDevice = false;
        }
    }

    [RelayCommand]
    private async Task SetDefaultCommunicationAsync()
    {
        if (IsChangingDevice) return;

        try
        {
            IsChangingDevice = true;
            var success = await _audioService.SetMicrophoneForRoleAsync(Id, NAudio.CoreAudioApi.Role.Communications, CancellationToken.None);
            if (!success)
            {
                _onError?.Invoke("Failed to set communication device");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetDefaultCommunicationAsync failed: {ex}");
            _onError?.Invoke("Failed to set communication device");
        }
        finally
        {
            IsChangingDevice = false;
        }
    }

    [RelayCommand]
    private async Task SetBothAsync()
    {
        if (IsChangingDevice) return;

        try
        {
            IsChangingDevice = true;
            var success = await _audioService.SetDefaultMicrophoneAsync(Id, CancellationToken.None);
            if (!success)
            {
                _onError?.Invoke("Failed to set default device");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetBothAsync failed: {ex}");
            _onError?.Invoke("Failed to set default device");
        }
        finally
        {
            IsChangingDevice = false;
        }
    }

    [RelayCommand]
    private async Task ToggleMuteAsync()
    {
        if (IsChangingDevice) return;

        try
        {
            IsChangingDevice = true;
            IsMuted = await _audioService.ToggleMuteAsync(Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleMuteAsync failed: {ex}");
            _onError?.Invoke("Failed to toggle mute");
        }
        finally
        {
            IsChangingDevice = false;
        }
    }

    partial void OnVolumePercentChanged(double value)
    {
        if (_suppressVolumeWrite) return;
        var clamped = Math.Max(0.0, Math.Min(100.0, value));
        _audioService.SetMicrophoneVolumeLevelScalar(Id, (float)(clamped / 100.0));
    }
}
