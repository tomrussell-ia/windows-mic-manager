using System.Collections.ObjectModel;
using System.Windows;
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

    public bool HasActiveSessions => ActiveSessions.Count > 0;
    public bool HasNoActiveSessions => ActiveSessions.Count == 0;

    public MicrophoneListViewModel(AudioDeviceService audioService)
    {
        _audioService = audioService;

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
}
