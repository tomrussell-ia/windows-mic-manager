using Microsoft.UI.Dispatching;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrophoneManager.WinUI.Services;

namespace MicrophoneManager.WinUI.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly IAudioDeviceService _audioService;
    private readonly Action<bool> _updateIconCallback;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs> _defaultVolumeChangedHandler;

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

        // Subscribe to default mic volume/mute changes (including external changes)
        _defaultVolumeChangedHandler = (s, e) => InvokeOnUiThread(UpdateState);
        _audioService.DefaultMicrophoneVolumeChanged += _defaultVolumeChangedHandler;

        // Initial state
        UpdateState();

        // Check startup state
        IsStartupEnabled = StartupService.IsStartupEnabled();
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
        InvokeOnUiThread(UpdateState);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        InvokeOnUiThread(UpdateState);
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
