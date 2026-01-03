using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.Models;
using MicrophoneManager.Services;
using Application = System.Windows.Application;

namespace MicrophoneManager.ViewModels;

public partial class MicrophoneListViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private bool _suppressVolumeWrite;
    private bool _suppressInputMeterReset;

    private readonly DispatcherTimer _peakHoldTimer;
    private DateTime _peakHoldUntilUtc;
    private DateTime _lastPeakTickUtc;

    private double _peakMicDbFs = -96.0;

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

    private static void InvokeOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            dispatcher.BeginInvoke(action);
            return;
        }

        // Unit tests (and some startup paths) may not have a WPF Application/Dispatcher.
        action();
    }

    public MicrophoneListViewModel(IAudioDeviceService audioService)
    {
        _audioService = audioService;

        _peakHoldUntilUtc = DateTime.MinValue;
        _lastPeakTickUtc = DateTime.UtcNow;
        _peakHoldTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _peakHoldTimer.Tick += (_, _) => TickPeakHold();
        _peakHoldTimer.Start();

        // Subscribe to changes
        _audioService.DevicesChanged += (s, e) => InvokeOnUiThread(RefreshDevices);
        _audioService.DefaultDeviceChanged += (s, e) => InvokeOnUiThread(RefreshDevices);

        _audioService.DefaultMicrophoneVolumeChanged += (s, e) =>
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

                    if (App.TrayViewModel != null)
                    {
                        App.TrayViewModel.IsMuted = IsMuted;
                    }

                    var defaultVm = Microphones.FirstOrDefault(m => m.Id == defaultId);
                    defaultVm?.ApplyVolumeFromSystem(volumePercent);
                }
                finally
                {
                    _suppressVolumeWrite = false;
                }
            });

        _audioService.DefaultMicrophoneInputLevelChanged += (s, e) =>
            InvokeOnUiThread(() =>
            {
                var defaultId = _audioService.GetDefaultDeviceId(NAudio.CoreAudioApi.Role.Console);
                if (defaultId == null || e.DeviceId != defaultId) return;

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

        // Initial load
        RefreshDevices();

        // Poll meters so each device shows live input activity and peaks.
        var meterTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        meterTimer.Tick += (_, _) => RefreshMeters();
        meterTimer.Start();
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
                Microphones.Add(new MicrophoneEntryViewModel(device, _audioService));
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
    private void ToggleMute()
    {
        IsMuted = _audioService.ToggleDefaultMicrophoneMute();

        // Update the TrayViewModel as well
        if (App.TrayViewModel != null)
        {
            App.TrayViewModel.IsMuted = IsMuted;
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

            _peakMicDbFs = MicrophoneManager.Services.ObsMeterMath.ClampMeterDb(currentDbFs);
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
        _peakMicDbFs = MicrophoneManager.Services.ObsMeterMath.ClampMeterDb(_peakMicDbFs);

        var newPeakPercent = MicrophoneManager.Services.ObsMeterMath.DbToPercent(_peakMicDbFs);
        if (Math.Abs(newPeakPercent - PeakMicInputLevelPercent) < 0.001) return;

        PeakMicInputLevelPercent = newPeakPercent;
        PeakMicInputLevelDbFs = _peakMicDbFs;
    }

    public bool HasMicrophones => Microphones.Count > 0;
    public bool HasNoMicrophones => Microphones.Count == 0;

    private void RefreshMeters()
    {
        // Only refresh the meter values to avoid excessive UI/layout churn.
        // Device name/default/mute/volume changes are handled by other subscriptions.
        var devices = _audioService.GetMicrophones();
        var inputById = devices.ToDictionary(d => d.Id, d => d.InputLevelPercent);

        foreach (var vm in Microphones)
        {
            if (inputById.TryGetValue(vm.Id, out var inputPercent))
            {
                vm.UpdateMeter(inputPercent);
            }
        }
    }
}
