using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.Services;
using Application = System.Windows.Application;

namespace MicrophoneManager.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly AudioDeviceService _audioService;
    private readonly Action<bool> _updateIconCallback;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private string _tooltipText = "Microphone Manager";

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isStartupEnabled;

    public string StartupMenuText => IsStartupEnabled ? "✓ Start with Windows" : "Start with Windows";

    public TrayViewModel(AudioDeviceService audioService, Action<bool> updateIconCallback)
    {
        _audioService = audioService;
        _updateIconCallback = updateIconCallback;
        _dispatcher = Application.Current.Dispatcher;

        // Subscribe to device changes
        _audioService.DefaultDeviceChanged += OnDefaultDeviceChanged;
        _audioService.DevicesChanged += OnDevicesChanged;

        // Initial state
        UpdateState();

        // Check startup state
        IsStartupEnabled = StartupService.IsStartupEnabled();
    }

    private void UpdateState()
    {
        var defaultMic = _audioService.GetDefaultMicrophone();
        if (defaultMic != null)
        {
            IsMuted = _audioService.IsDefaultMicrophoneMuted();
            TooltipText = IsMuted
                ? $"{defaultMic.Name} (Muted)"
                : defaultMic.Name;
        }
        else
        {
            TooltipText = "No microphone detected";
            IsMuted = false;
        }

        _updateIconCallback?.Invoke(IsMuted);
    }

    private void OnDefaultDeviceChanged(object? sender, EventArgs e)
    {
        _dispatcher?.BeginInvoke(UpdateState);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _dispatcher?.BeginInvoke(UpdateState);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = _audioService.ToggleDefaultMicrophoneMute();
        UpdateState();
    }

    [RelayCommand]
    private void ToggleStartup()
    {
        IsStartupEnabled = StartupService.ToggleStartup();
        OnPropertyChanged(nameof(StartupMenuText));
    }

    [RelayCommand]
    private void Exit()
    {
        Application.Current.Shutdown();
    }
}
