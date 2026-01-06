using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.WinUI.Models;
using MicrophoneManager.WinUI.Services;

namespace MicrophoneManager.WinUI.ViewModels;

public partial class MicrophoneListViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _suppressVolumeWrite;
    private bool _suppressInputMeterReset;

    private DispatcherQueueTimer? _peakHoldTimer;
    private DispatcherQueueTimer? _meterTimer;
    private DateTime _peakHoldUntilUtc;
    private DateTime _lastPeakTickUtc;

    private double _peakMicDbFs = -96.0;

    private bool _meteringEnabled;
    private bool _disposed;

    private readonly EventHandler _devicesChangedHandler;
    private readonly EventHandler _defaultDeviceChangedHandler;
    private readonly EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs> _defaultVolumeChangedHandler;
    private readonly EventHandler<AudioDeviceService.MicrophoneVolumeChangedEventArgs> _microphoneVolumeChangedHandler;
    private readonly EventHandler<AudioDeviceService.DefaultMicrophoneInputLevelChangedEventArgs> _defaultInputLevelChangedHandler;
    private readonly EventHandler<AudioDeviceService.MicrophoneFormatChangedEventArgs> _formatChangedHandler;

    private const int PeakHoldMilliseconds = 5000;
    private const double PeakDecayDbPerSecond = 20.0;

    [ObservableProperty]
    private ObservableCollection<MicrophoneEntryViewModel> _microphones = new();

    [ObservableProperty]
    private MicrophoneEntryViewModel? _selectedMicrophone;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _currentMicLevelPercent;

    [ObservableProperty]
    private double _currentMicInputLevelPercent;

    [ObservableProperty]
    private double _currentMicInputLevelDbFs;

    [ObservableProperty]
    private double _peakMicInputLevelPercent;

    [ObservableProperty]
    private double _peakMicInputLevelDbFs;

    [ObservableProperty]
    private string? _errorMessage;

    private DispatcherQueueTimer? _errorDismissTimer;
    private const int ErrorDismissMilliseconds = 5000;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public void ShowError(string message)
    {
        ErrorMessage = message;
        OnPropertyChanged(nameof(HasError));

        // Auto-dismiss after 5 seconds
        if (_dispatcherQueue != null)
        {
            _errorDismissTimer?.Stop();
            _errorDismissTimer = _dispatcherQueue.CreateTimer();
            _errorDismissTimer.Interval = TimeSpan.FromMilliseconds(ErrorDismissMilliseconds);
            _errorDismissTimer.IsRepeating = false;
            _errorDismissTimer.Tick += (s, e) =>
            {
                ErrorMessage = null;
                OnPropertyChanged(nameof(HasError));
                _errorDismissTimer?.Stop();
            };
            _errorDismissTimer.Start();
        }
    }

    public void DismissError()
    {
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));
        _errorDismissTimer?.Stop();
    }

    private void InvokeOnUiThread(Action action)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => action());
            return;
        }

        // Unit tests (and some startup paths) may not have a DispatcherQueue
        action();
    }

    public MicrophoneListViewModel(IAudioDeviceService audioService)
    {
        _audioService = audioService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _peakHoldUntilUtc = DateTime.MinValue;
        _lastPeakTickUtc = DateTime.UtcNow;

        _devicesChangedHandler = (s, e) => InvokeOnUiThread(RefreshDevices);
        _defaultDeviceChangedHandler = (s, e) => InvokeOnUiThread(RefreshDevices);
        _defaultVolumeChangedHandler = (s, e) =>
            InvokeOnUiThread(() =>
            {
                // Only reflect live updates for the current default microphone.
                var defaultId = _audioService.GetDefaultDeviceId(NAudio.CoreAudioApi.Role.Console);
                if (defaultId == null || e.DeviceId != defaultId) return;

                _suppressVolumeWrite = true;
                try
                {
                    var volumePercent = Math.Round(e.VolumeLevelScalar * 100.0, 2);
                    CurrentMicLevelPercent = volumePercent;
                    IsMuted = e.IsMuted;

                    // TODO: Update TrayViewModel if needed via App static reference
                    // if (App.TrayViewModel != null)
                    // {
                    //     App.TrayViewModel.IsMuted = IsMuted;
                    // }

                    var defaultVm = Microphones.FirstOrDefault(m => m.Id == defaultId);
                    defaultVm?.ApplyVolumeFromSystem(volumePercent);
                    if (defaultVm != null)
                    {
                        defaultVm.IsMuted = e.IsMuted;
                    }
                }
                finally
                {
                    _suppressVolumeWrite = false;
                }
            });

        _microphoneVolumeChangedHandler = (s, e) =>
            InvokeOnUiThread(() =>
            {
                var vm = Microphones.FirstOrDefault(m => m.Id == e.DeviceId);
                if (vm == null)
                {
                    return;
                }

                var volumePercent = Math.Round(e.VolumeLevelScalar * 100.0, 2);
                vm.ApplyVolumeFromSystem(volumePercent);
                vm.IsMuted = e.IsMuted;
            });

        _defaultInputLevelChangedHandler = (s, e) =>
            InvokeOnUiThread(() =>
            {
                var defaultId = _audioService.GetDefaultDeviceId(NAudio.CoreAudioApi.Role.Console);
                if (defaultId == null || e.DeviceId != defaultId) return;

                // If the default mic is muted, present meters as silent.
                if (IsMuted)
                {
                    CurrentMicInputLevelPercent = 0;
                    CurrentMicInputLevelDbFs = -96;
                    PeakMicInputLevelPercent = 0;
                    PeakMicInputLevelDbFs = -96;
                    _peakHoldUntilUtc = DateTime.MinValue;
                    _peakMicDbFs = -96;
                    return;
                }

                _suppressInputMeterReset = true;
                try
                {
                    CurrentMicInputLevelPercent = e.InputLevelPercent;
                    CurrentMicInputLevelDbFs = e.InputLevelDbFs;

                    UpdatePeakHold(e.InputLevelPercent, e.InputLevelDbFs);
                }
                finally
                {
                    _suppressInputMeterReset = false;
                }
            });

        _formatChangedHandler = (s, e) =>
            InvokeOnUiThread(() =>
            {
                var vm = Microphones.FirstOrDefault(m => m.Id == e.DeviceId);
                if (vm != null)
                {
                    vm.FormatTag = e.FormatTag;
                }
            });

            // Subscribe to changes
            _audioService.DevicesChanged += _devicesChangedHandler;
            _audioService.DefaultDeviceChanged += _defaultDeviceChangedHandler;
            _audioService.DefaultMicrophoneVolumeChanged += _defaultVolumeChangedHandler;
            _audioService.MicrophoneVolumeChanged += _microphoneVolumeChangedHandler;
            _audioService.DefaultMicrophoneInputLevelChanged += _defaultInputLevelChangedHandler;
            _audioService.MicrophoneFormatChanged += _formatChangedHandler;

        // Initial load
        RefreshDevices();

        // Meter refresh is started/stopped by the host UI (tray flyout window).
        // Default is OFF to avoid background 60Hz enumeration when no UI is visible.
        SetMeteringEnabled(false);
    }

    public void SetMeteringEnabled(bool enabled)
    {
        if (_disposed) return;
        if (_meteringEnabled == enabled) return;

        _meteringEnabled = enabled;

        if (_dispatcherQueue == null)
        {
            return;
        }

        if (enabled)
        {
            if (_peakHoldTimer == null)
            {
                _peakHoldTimer = _dispatcherQueue.CreateTimer();
                _peakHoldTimer.Interval = TimeSpan.FromMilliseconds(33);
                _peakHoldTimer.Tick += (s, e) => TickPeakHold();
            }
            _peakHoldTimer.Start();

            if (_meterTimer == null)
            {
                _meterTimer = _dispatcherQueue.CreateTimer();
                // Enumerating all capture devices is relatively expensive; 10Hz is plenty for UI.
                _meterTimer.Interval = TimeSpan.FromMilliseconds(100);
                _meterTimer.Tick += (s, e) => RefreshMeters();
            }
            _meterTimer.Start();
        }
        else
        {
            try { _meterTimer?.Stop(); } catch { }
            try { _peakHoldTimer?.Stop(); } catch { }
        }
    }

    public void RefreshDevices()
    {
        var devices = _audioService.GetMicrophones();

        var existingById = Microphones.ToDictionary(m => m.Id, m => m);
        var seenIds = new HashSet<string>();

        foreach (var device in devices)
        {
            if (existingById.TryGetValue(device.Id, out var vm))
            {
                vm.UpdateFrom(device);
            }
            else
            {
                Microphones.Add(new MicrophoneEntryViewModel(device, _audioService, ShowError));
            }

            seenIds.Add(device.Id);
        }

        var toRemove = Microphones.Where(m => !seenIds.Contains(m.Id)).ToList();
        foreach (var remove in toRemove)
        {
            Microphones.Remove(remove);
        }

        SelectedMicrophone = Microphones.FirstOrDefault(m => m.IsDefault);
        IsMuted = _audioService.IsDefaultMicrophoneMuted();

        _suppressVolumeWrite = true;
        try
        {
            // Reflect the current default microphone level into the UI (0-100)
            CurrentMicLevelPercent = SelectedMicrophone?.VolumePercent ?? 100.0;
        }
        finally
        {
            _suppressVolumeWrite = false;
        }

        if (!_suppressInputMeterReset)
        {
            // Reset meter visuals on device refresh; live capture will repopulate almost immediately.
            CurrentMicInputLevelPercent = 0;
            CurrentMicInputLevelDbFs = -60;
            PeakMicInputLevelPercent = 0;
            PeakMicInputLevelDbFs = -60;
            _peakHoldUntilUtc = DateTime.MinValue;
        }

        OnPropertyChanged(nameof(HasMicrophones));
        OnPropertyChanged(nameof(HasNoMicrophones));
    }

    partial void OnCurrentMicLevelPercentChanged(double value)
    {
        if (_suppressVolumeWrite) return;

        // Slider drives the current default microphone volume.
        _audioService.SetDefaultMicrophoneVolumePercent(value);
    }

    [RelayCommand]
    private async Task ToggleMuteAsync()
    {
        try
        {
            IsMuted = await _audioService.ToggleDefaultMicrophoneMuteAsync(CancellationToken.None);

            if (IsMuted)
            {
                CurrentMicInputLevelPercent = 0;
                CurrentMicInputLevelDbFs = -96;
                PeakMicInputLevelPercent = 0;
                PeakMicInputLevelDbFs = -96;
                _peakHoldUntilUtc = DateTime.MinValue;
                _peakMicDbFs = -96;
            }

            // TODO: Update the TrayViewModel as well via App static reference
            // if (App.TrayViewModel != null)
            // {
            //     App.TrayViewModel.IsMuted = IsMuted;
            // }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleMuteAsync failed: {ex}");
            ShowError("Failed to toggle mute");
        }
    }

    private void UpdatePeakHold(double currentPercent, double currentDbFs)
    {
        var clampedPercent = Math.Max(0.0, Math.Min(100.0, currentPercent));
        if (clampedPercent >= PeakMicInputLevelPercent)
        {
            PeakMicInputLevelPercent = clampedPercent;
            PeakMicInputLevelDbFs = currentDbFs;
            _peakHoldUntilUtc = DateTime.UtcNow.AddMilliseconds(PeakHoldMilliseconds);

            _peakMicDbFs = ObsMeterMath.ClampMeterDb(currentDbFs);
        }
    }

    private void TickPeakHold()
    {
        var nowUtc = DateTime.UtcNow;
        var dt = nowUtc - _lastPeakTickUtc;
        _lastPeakTickUtc = nowUtc;

        if (dt.TotalSeconds <= 0) return;
        if (nowUtc <= _peakHoldUntilUtc) return;

        _peakMicDbFs -= PeakDecayDbPerSecond * dt.TotalSeconds;
        _peakMicDbFs = Math.Max(CurrentMicInputLevelDbFs, _peakMicDbFs);
        _peakMicDbFs = ObsMeterMath.ClampMeterDb(_peakMicDbFs);

        var newPeakPercent = ObsMeterMath.DbToPercent(_peakMicDbFs);
        if (Math.Abs(newPeakPercent - PeakMicInputLevelPercent) < 0.001) return;

        PeakMicInputLevelPercent = newPeakPercent;
        PeakMicInputLevelDbFs = _peakMicDbFs;
    }

    public bool HasMicrophones => Microphones.Count > 0;
    public bool HasNoMicrophones => Microphones.Count == 0;

    private void RefreshMeters()
    {
        if (!_meteringEnabled) return;

        // Only refresh the meter values to avoid excessive UI/layout churn.
        // Device name/default/mute/volume changes are handled by other subscriptions.
        var devices = _audioService.GetMicrophones();
        var inputById = devices.ToDictionary(d => d.Id, d => d.InputLevelPercent);
        var muteById = devices.ToDictionary(d => d.Id, d => d.IsMuted);

        foreach (var vm in Microphones)
        {
            if (inputById.TryGetValue(vm.Id, out var inputPercent))
            {
                // Use the service's current mute state (not just vm.IsMuted) to ensure we're in sync
                var shouldMute = muteById.TryGetValue(vm.Id, out var isMuted) && isMuted;
                var finalLevel = shouldMute ? 0 : inputPercent;
                vm.UpdateMeter(finalLevel);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { SetMeteringEnabled(false); } catch { }

        try { _audioService.DevicesChanged -= _devicesChangedHandler; } catch { }
        try { _audioService.DefaultDeviceChanged -= _defaultDeviceChangedHandler; } catch { }
        try { _audioService.DefaultMicrophoneVolumeChanged -= _defaultVolumeChangedHandler; } catch { }
        try { _audioService.MicrophoneVolumeChanged -= _microphoneVolumeChangedHandler; } catch { }
        try { _audioService.DefaultMicrophoneInputLevelChanged -= _defaultInputLevelChangedHandler; } catch { }
        try { _audioService.MicrophoneFormatChanged -= _formatChangedHandler; } catch { }
    }
}
