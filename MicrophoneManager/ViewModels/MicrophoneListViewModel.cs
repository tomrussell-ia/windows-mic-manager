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

    [ObservableProperty]
    private ObservableCollection<MicrophoneDevice> _microphones = new();

    [ObservableProperty]
    private MicrophoneDevice? _selectedMicrophone;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private ObservableCollection<AudioSession> _activeSessions = new();

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

        // Refresh active sessions (apps using microphone)
        RefreshActiveSessions();
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
