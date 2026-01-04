using Microsoft.UI.Dispatching;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.Core.Services;

namespace MicrophoneManager.Core.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private readonly Action<bool> _updateIconCallback;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _tooltipText = "Microphone Manager";

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isStartupEnabled;

    public string StartupMenuText => IsStartupEnabled ? "✓ Start with Windows" : "Start with Windows";

    public TrayViewModel(IAudioDeviceService audioService, Action<bool> updateIconCallback)
    {
        _audioService = audioService;
        _updateIconCallback = updateIconCallback;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

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
        _dispatcherQueue?.TryEnqueue(UpdateState);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue?.TryEnqueue(UpdateState);
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
        // WinUI 3 - use Application.Current.Exit()
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
