using MicrophoneManager.Interop;
using MicrophoneManager.Models;
using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace MicrophoneManager.Services;

/// <summary>
/// Audio device service implementation that uses the Rust FFI engine.
/// This replaces the NAudio-based AudioDeviceService with Rust core logic.
/// </summary>
public sealed class RustAudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly MicEngine _engine;
    private bool _disposed;

    // Event support
    public event EventHandler? DevicesChanged;
    public event EventHandler? DefaultDeviceChanged;
    public event EventHandler<AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    public event EventHandler<AudioDeviceService.DefaultMicrophoneInputLevelChangedEventArgs>? DefaultMicrophoneInputLevelChanged;

    public RustAudioDeviceService()
    {
        try
        {
            _engine = new MicEngine();
            Debug.WriteLine($"[RustAudioDeviceService] Rust engine initialized, version: {MicEngine.GetVersion()}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] Failed to initialize Rust engine: {ex.Message}");
            throw;
        }
    }

    public List<MicrophoneDevice> GetMicrophones()
    {
        ThrowIfDisposed();

        try
        {
            var rustDevices = _engine.GetDevices();
            return rustDevices.Select(ConvertToMicrophoneDevice).ToList();
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] GetMicrophones failed: {ex.Message}");
            return new List<MicrophoneDevice>();
        }
    }

    public string? GetDefaultDeviceId(Role role)
    {
        ThrowIfDisposed();

        try
        {
            var devices = _engine.GetDevices();
            return role switch
            {
                Role.Console => devices.FirstOrDefault(d => d.IsDefault)?.Id,
                Role.Communications => devices.FirstOrDefault(d => d.IsDefaultCommunication)?.Id,
                _ => null
            };
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] GetDefaultDeviceId failed: {ex.Message}");
            return null;
        }
    }

    public MicrophoneDevice? GetDefaultMicrophone()
    {
        ThrowIfDisposed();

        try
        {
            var devices = _engine.GetDevices();
            var defaultDevice = devices.FirstOrDefault(d => d.IsDefault);
            return defaultDevice != null ? ConvertToMicrophoneDevice(defaultDevice) : null;
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] GetDefaultMicrophone failed: {ex.Message}");
            return null;
        }
    }

    public void SetDefaultMicrophone(string deviceId)
    {
        ThrowIfDisposed();

        try
        {
            _engine.SetDefaultDeviceAllRoles(deviceId);
            DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] SetDefaultMicrophone failed: {ex.Message}");
            throw;
        }
    }

    public void SetMicrophoneForRole(string deviceId, Role role)
    {
        ThrowIfDisposed();

        try
        {
            var rustRole = role switch
            {
                Role.Console => MicDeviceRole.Console,
                Role.Communications => MicDeviceRole.Communications,
                Role.Multimedia => MicDeviceRole.Multimedia,
                _ => MicDeviceRole.Console
            };

            _engine.SetDefaultDevice(deviceId, rustRole);
            DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] SetMicrophoneForRole failed: {ex.Message}");
            throw;
        }
    }

    public void SetDefaultMicrophoneVolumePercent(double volumePercent)
    {
        ThrowIfDisposed();

        try
        {
            var defaultDeviceId = GetDefaultDeviceId(Role.Console);
            if (defaultDeviceId != null)
            {
                var volumeScalar = (float)(volumePercent / 100.0);
                _engine.SetVolume(defaultDeviceId, volumeScalar);
                DefaultMicrophoneVolumeChanged?.Invoke(this,
                    new AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs(volumeScalar, false));
            }
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] SetDefaultMicrophoneVolumePercent failed: {ex.Message}");
            throw;
        }
    }

    public void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar)
    {
        ThrowIfDisposed();

        try
        {
            _engine.SetVolume(deviceId, volumeLevelScalar);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] SetMicrophoneVolumeLevelScalar failed: {ex.Message}");
            throw;
        }
    }

    public bool ToggleMute(string deviceId)
    {
        ThrowIfDisposed();

        try
        {
            return _engine.ToggleMute(deviceId);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] ToggleMute failed: {ex.Message}");
            throw;
        }
    }

    public bool IsMuted(string deviceId)
    {
        ThrowIfDisposed();

        try
        {
            return _engine.IsMuted(deviceId);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] IsMuted failed: {ex.Message}");
            return false;
        }
    }

    public bool ToggleDefaultMicrophoneMute()
    {
        ThrowIfDisposed();

        try
        {
            var defaultDeviceId = GetDefaultDeviceId(Role.Console);
            if (defaultDeviceId != null)
            {
                var newState = _engine.ToggleMute(defaultDeviceId);
                var device = _engine.GetDevice(defaultDeviceId);
                if (device != null)
                {
                    DefaultMicrophoneVolumeChanged?.Invoke(this,
                        new AudioDeviceService.DefaultMicrophoneVolumeChangedEventArgs(device.VolumeLevel, newState));
                }
                return newState;
            }
            return false;
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] ToggleDefaultMicrophoneMute failed: {ex.Message}");
            return false;
        }
    }

    public bool IsDefaultMicrophoneMuted()
    {
        ThrowIfDisposed();

        try
        {
            var defaultDeviceId = GetDefaultDeviceId(Role.Console);
            return defaultDeviceId != null && _engine.IsMuted(defaultDeviceId);
        }
        catch (MicEngineException ex)
        {
            Debug.WriteLine($"[RustAudioDeviceService] IsDefaultMicrophoneMuted failed: {ex.Message}");
            return false;
        }
    }

    private static MicrophoneDevice ConvertToMicrophoneDevice(MicDeviceDto dto)
    {
        return new MicrophoneDevice
        {
            Id = dto.Id,
            Name = dto.Name,
            IsDefault = dto.IsDefault,
            IsDefaultCommunication = dto.IsDefaultCommunication,
            IsMuted = dto.IsMuted,
            VolumeLevel = dto.VolumeLevel,
            FormatTag = dto.AudioFormat?.ToString() ?? "",
            InputLevelPercent = 0 // Input level not yet supported via FFI
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RustAudioDeviceService));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
