using System.Collections.ObjectModel;
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
    private readonly AudioDeviceService _audioService;
    private bool _suppressVolumeWrite;
    private bool _suppressInputMeterReset;

    private readonly DispatcherTimer _peakHoldTimer;
    private DateTime _peakHoldUntilUtc;
    private DateTime _lastPeakTickUtc;

    private const int PeakHoldMilliseconds = 750;
    private const double PeakDecayPercentPerSecond = 35.0;

    [ObservableProperty]
    private ObservableCollection<MicrophoneDevice> _microphones = new();

    [ObservableProperty]
    private MicrophoneDevice? _selectedMicrophone;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private ObservableCollection<AudioSession> _activeSessions = new();

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

    public bool HasActiveSessions => ActiveSessions.Count > 0;
    public bool HasNoActiveSessions => ActiveSessions.Count == 0;

    public MicrophoneListViewModel(AudioDeviceService audioService)
    {
        _audioService = audioService;

        _peakHoldUntilUtc = DateTime.MinValue;
        _lastPeakTickUtc = DateTime.UtcNow;
        _peakHoldTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _peakHoldTimer.Tick += (_, _) => TickPeakHold();
        _peakHoldTimer.Start();

        // Subscribe to changes
        _audioService.DevicesChanged += (s, e) =>
            Application.Current?.Dispatcher?.BeginInvoke(RefreshDevices);
        _audioService.DefaultDeviceChanged += (s, e) =>
            Application.Current?.Dispatcher?.BeginInvoke(RefreshDevices);

        _audioService.DefaultMicrophoneVolumeChanged += (s, e) =>
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                // Only reflect live updates for the current default microphone.
                var defaultId = _audioService.GetDefaultDeviceId(NAudio.CoreAudioApi.Role.Console);
                if (defaultId == null || e.DeviceId != defaultId) return;

                _suppressVolumeWrite = true;
                try
                {
                    CurrentMicLevelPercent = e.VolumeLevelScalar * 100.0;
                    IsMuted = e.IsMuted;

                    if (App.TrayViewModel != null)
                    {
                        App.TrayViewModel.IsMuted = IsMuted;
                    }
                }
                finally
                {
                    _suppressVolumeWrite = false;
                }
            });

        _audioService.DefaultMicrophoneInputLevelChanged += (s, e) =>
            Application.Current?.Dispatcher?.BeginInvoke(() =>
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
    }

    public void RefreshDevices()
    {
        var devices = _audioService.GetMicrophones();

        Microphones.Clear();
        foreach (var device in devices)
        {
            Microphones.Add(device);
        }

        SelectedMicrophone = Microphones.FirstOrDefault(m => m.IsDefault);
        IsMuted = _audioService.IsDefaultMicrophoneMuted();

        _suppressVolumeWrite = true;
        try
        {
            // Reflect the current default microphone level into the UI (0-100)
            CurrentMicLevelPercent = (SelectedMicrophone?.VolumeLevel ?? 1.0f) * 100.0;
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

        // Refresh active sessions (apps using microphone)
        RefreshActiveSessions();
    }

    partial void OnCurrentMicLevelPercentChanged(double value)
    {
        if (_suppressVolumeWrite) return;

        // Slider drives the current default microphone volume.
        _audioService.SetDefaultMicrophoneVolumePercent(value);
    }

    public void RefreshActiveSessions()
    {
        var sessions = _audioService.GetActiveMicrophoneSessions();

        ActiveSessions.Clear();
        foreach (var session in sessions)
        {
            ActiveSessions.Add(session);
        }

        // Notify that the computed properties have changed
        OnPropertyChanged(nameof(HasActiveSessions));
        OnPropertyChanged(nameof(HasNoActiveSessions));
    }

    [RelayCommand]
    private void SelectMicrophone(MicrophoneDevice? device)
    {
        if (device == null || device.IsDefault) return;

        _audioService.SetDefaultMicrophone(device.Id);

        // Update selection state
        foreach (var mic in Microphones)
        {
            mic.IsDefault = mic.Id == device.Id;
            mic.IsDefaultCommunication = mic.Id == device.Id;
        }

        SelectedMicrophone = device;

        // Force UI refresh
        var temp = Microphones.ToList();
        Microphones.Clear();
        foreach (var mic in temp)
        {
            Microphones.Add(mic);
        }
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
        }
    }

    private void TickPeakHold()
    {
        var nowUtc = DateTime.UtcNow;
        var dt = nowUtc - _lastPeakTickUtc;
        _lastPeakTickUtc = nowUtc;

        if (dt.TotalSeconds <= 0) return;
        if (nowUtc <= _peakHoldUntilUtc) return;

        var decay = PeakDecayPercentPerSecond * dt.TotalSeconds;
        var newPeak = PeakMicInputLevelPercent - decay;
        newPeak = Math.Max(CurrentMicInputLevelPercent, newPeak);
        newPeak = Math.Max(0.0, Math.Min(100.0, newPeak));

        if (Math.Abs(newPeak - PeakMicInputLevelPercent) < 0.001) return;

        PeakMicInputLevelPercent = newPeak;

        // Keep dBFS label consistent with our meter mapping (-60..0 dBFS -> 0..100%).
        PeakMicInputLevelDbFs = -60.0 + (PeakMicInputLevelPercent / 100.0) * 60.0;
    }
}
