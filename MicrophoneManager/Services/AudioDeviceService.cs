using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using MicrophoneManager.Models;

namespace MicrophoneManager.Services;

public class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly object _defaultVolumeNotificationLock = new();
    private string? _defaultCaptureDeviceIdWithVolumeNotifications;
    private AudioEndpointVolume? _defaultCaptureEndpointVolume;
    private bool _disposed;

    public event EventHandler? DevicesChanged;
    public event EventHandler? DefaultDeviceChanged;
    public event EventHandler<DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;

    public AudioDeviceService()
    {
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new DeviceNotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);

        // Track default microphone volume changes (e.g., changed by other apps)
        UpdateDefaultMicrophoneVolumeNotificationSubscription();
    }

    /// <summary>
    /// Sets the volume of the current default microphone (0-100).
    /// </summary>
    public void SetDefaultMicrophoneVolumePercent(double volumePercent)
    {
        var defaultId = GetDefaultDeviceId(Role.Console);
        if (defaultId == null) return;

        var clampedPercent = Math.Max(0.0, Math.Min(100.0, volumePercent));
        var scalar = (float)(clampedPercent / 100.0);
        SetMicrophoneVolumeLevelScalar(defaultId, scalar);
    }

    /// <summary>
    /// Sets the volume scalar (0.0 - 1.0) for a specific microphone device.
    /// </summary>
    public void SetMicrophoneVolumeLevelScalar(string deviceId, float volumeLevelScalar)
    {
        var device = GetDeviceById(deviceId);
        if (device?.AudioEndpointVolume == null) return;

        var clampedScalar = Math.Max(0.0f, Math.Min(1.0f, volumeLevelScalar));

        try
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = clampedScalar;
        }
        catch
        {
            // Ignore failures (device could disappear, access denied, etc.)
        }
    }

    /// <summary>
    /// Gets all active capture (microphone) devices.
    /// </summary>
    public List<MicrophoneDevice> GetMicrophones()
    {
        var devices = new List<MicrophoneDevice>();
        var defaultId = GetDefaultDeviceId(Role.Console);
        var defaultCommId = GetDefaultDeviceId(Role.Communications);

        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var mic = new MicrophoneDevice
            {
                Id = device.ID,
                Name = device.FriendlyName,
                IsDefault = device.ID == defaultId,
                IsDefaultCommunication = device.ID == defaultCommId,
                IsMuted = GetDeviceMuteState(device),
                VolumeLevel = GetDeviceVolume(device)
            };
            devices.Add(mic);
        }

        return devices;
    }

    /// <summary>
    /// Gets the device ID of the default capture device for the specified role.
    /// </summary>
    public string? GetDefaultDeviceId(Role role)
    {
        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role);
            return device?.ID;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the default microphone device.
    /// </summary>
    public MicrophoneDevice? GetDefaultMicrophone()
    {
        var defaultId = GetDefaultDeviceId(Role.Console);
        if (defaultId == null) return null;

        var devices = GetMicrophones();
        return devices.FirstOrDefault(d => d.Id == defaultId);
    }

    /// <summary>
    /// Sets the specified device as the default microphone for all roles.
    /// </summary>
    public void SetDefaultMicrophone(string deviceId)
    {
        PolicyConfigService.SetDefaultDeviceForAllRoles(deviceId);
    }

    /// <summary>
    /// Toggles the mute state of the specified device.
    /// </summary>
    public bool ToggleMute(string deviceId)
    {
        var device = GetDeviceById(deviceId);
        if (device?.AudioEndpointVolume == null) return false;

        var newMuteState = !device.AudioEndpointVolume.Mute;
        device.AudioEndpointVolume.Mute = newMuteState;
        return newMuteState;
    }

    /// <summary>
    /// Gets the mute state of the specified device.
    /// </summary>
    public bool IsMuted(string deviceId)
    {
        var device = GetDeviceById(deviceId);
        return device?.AudioEndpointVolume?.Mute ?? false;
    }

    /// <summary>
    /// Toggles mute on the current default microphone.
    /// </summary>
    public bool ToggleDefaultMicrophoneMute()
    {
        var defaultId = GetDefaultDeviceId(Role.Console);
        if (defaultId == null) return false;
        return ToggleMute(defaultId);
    }

    /// <summary>
    /// Gets the mute state of the default microphone.
    /// </summary>
    public bool IsDefaultMicrophoneMuted()
    {
        var defaultId = GetDefaultDeviceId(Role.Console);
        if (defaultId == null) return false;
        return IsMuted(defaultId);
    }

    /// <summary>
    /// Gets all active audio sessions on capture devices (apps using the microphone).
    /// </summary>
    public List<AudioSession> GetActiveMicrophoneSessions()
    {
        var sessions = new List<AudioSession>();

        try
        {
            // Get all capture devices and check sessions on each
            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Checking sessions on device: {device.FriendlyName}");

                    var sessionManager = device.AudioSessionManager;
                    if (sessionManager?.Sessions == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  No session manager or sessions for {device.FriendlyName}");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"  Found {sessionManager.Sessions.Count} sessions");

                    for (int i = 0; i < sessionManager.Sessions.Count; i++)
                    {
                        try
                        {
                            var session = sessionManager.Sessions[i];
                            if (session == null) continue;

                            var state = session.State;
                            var processId = session.GetProcessID;
                            var isSystemSound = session.IsSystemSoundsSession;

                            System.Diagnostics.Debug.WriteLine($"  Session {i}: PID={processId}, State={state}, IsSystemSound={isSystemSound}");

                            // Include Active and Inactive sessions (apps that have mic open)
                            // Expired sessions are ones that were closed
                            bool isRelevant = state == AudioSessionState.AudioSessionStateActive ||
                                              state == AudioSessionState.AudioSessionStateInactive;

                            if (isRelevant && !isSystemSound && processId > 0)
                            {
                                var audioSession = CreateAudioSession(processId, isSystemSound);
                                if (audioSession != null)
                                {
                                    audioSession.IsActive = (state == AudioSessionState.AudioSessionStateActive);
                                    sessions.Add(audioSession);
                                    System.Diagnostics.Debug.WriteLine($"    Added session for PID {processId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error reading session {i}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing sessions on device {device.FriendlyName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enumerating capture devices for sessions: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine($"Total sessions found: {sessions.Count}");

        // Remove duplicates (same process might appear on multiple devices)
        return sessions.DistinctBy(s => s.ProcessId).ToList();
    }

    private AudioSession? CreateAudioSession(uint processId, bool isSystemSound)
    {
        try
        {
            var process = Process.GetProcessById((int)processId);
            var displayName = GetProcessDisplayName(process);
            var icon = GetProcessIcon(process);

            return new AudioSession
            {
                ProcessId = processId,
                ProcessName = process.ProcessName,
                DisplayName = displayName,
                Icon = icon,
                IsActive = true,
                IsSystemSound = isSystemSound
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating audio session for PID {processId}: {ex.Message}");
            return null;
        }
    }

    private static string GetProcessDisplayName(Process process)
    {
        try
        {
            // Try to get the main window title first
            if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
            {
                return process.MainWindowTitle;
            }

            // Try to get the file description
            var module = process.MainModule;
            if (module?.FileVersionInfo?.FileDescription != null &&
                !string.IsNullOrWhiteSpace(module.FileVersionInfo.FileDescription))
            {
                return module.FileVersionInfo.FileDescription;
            }

            // Fall back to process name
            return process.ProcessName;
        }
        catch
        {
            return process.ProcessName;
        }
    }

    private static ImageSource? GetProcessIcon(Process process)
    {
        try
        {
            var module = process.MainModule;
            if (module?.FileName == null) return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(module.FileName);
            if (icon == null) return null;

            // Convert System.Drawing.Icon to WPF ImageSource
            var bitmap = icon.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
                bitmap.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting icon for process {process.ProcessName}: {ex.Message}");
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private MMDevice? GetDeviceById(string deviceId)
    {
        try
        {
            return _enumerator.GetDevice(deviceId);
        }
        catch
        {
            return null;
        }
    }

    private static bool GetDeviceMuteState(MMDevice device)
    {
        try
        {
            return device.AudioEndpointVolume?.Mute ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static float GetDeviceVolume(MMDevice device)
    {
        try
        {
            return device.AudioEndpointVolume?.MasterVolumeLevelScalar ?? 1.0f;
        }
        catch
        {
            return 1.0f;
        }
    }

    internal void OnDevicesChanged()
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void OnDefaultDeviceChanged()
    {
        UpdateDefaultMicrophoneVolumeNotificationSubscription();
        DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDefaultMicrophoneVolumeNotificationSubscription()
    {
        lock (_defaultVolumeNotificationLock)
        {
            var defaultDeviceId = GetDefaultDeviceId(Role.Console);

            if (_defaultCaptureDeviceIdWithVolumeNotifications == defaultDeviceId && _defaultCaptureEndpointVolume != null)
            {
                return;
            }

            if (_defaultCaptureEndpointVolume != null)
            {
                try
                {
                    _defaultCaptureEndpointVolume.OnVolumeNotification -= OnDefaultMicrophoneVolumeNotification;
                }
                catch { }

                _defaultCaptureEndpointVolume = null;
                _defaultCaptureDeviceIdWithVolumeNotifications = null;
            }

            if (defaultDeviceId == null) return;

            var device = GetDeviceById(defaultDeviceId);
            var endpointVolume = device?.AudioEndpointVolume;
            if (endpointVolume == null) return;

            _defaultCaptureDeviceIdWithVolumeNotifications = defaultDeviceId;
            _defaultCaptureEndpointVolume = endpointVolume;

            try
            {
                _defaultCaptureEndpointVolume.OnVolumeNotification += OnDefaultMicrophoneVolumeNotification;
            }
            catch
            {
                _defaultCaptureEndpointVolume = null;
                _defaultCaptureDeviceIdWithVolumeNotifications = null;
            }
        }
    }

    private void OnDefaultMicrophoneVolumeNotification(AudioVolumeNotificationData data)
    {
        string? deviceId;
        lock (_defaultVolumeNotificationLock)
        {
            deviceId = _defaultCaptureDeviceIdWithVolumeNotifications;
        }

        if (deviceId == null) return;

        DefaultMicrophoneVolumeChanged?.Invoke(
            this,
            new DefaultMicrophoneVolumeChangedEventArgs(deviceId, data.MasterVolume, data.Muted));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_defaultVolumeNotificationLock)
        {
            if (_defaultCaptureEndpointVolume != null)
            {
                try
                {
                    _defaultCaptureEndpointVolume.OnVolumeNotification -= OnDefaultMicrophoneVolumeNotification;
                }
                catch { }

                _defaultCaptureEndpointVolume = null;
                _defaultCaptureDeviceIdWithVolumeNotifications = null;
            }
        }

        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        }
        catch { }

        _enumerator?.Dispose();
    }

    public sealed class DefaultMicrophoneVolumeChangedEventArgs : EventArgs
    {
        public DefaultMicrophoneVolumeChangedEventArgs(string deviceId, float volumeLevelScalar, bool isMuted)
        {
            DeviceId = deviceId;
            VolumeLevelScalar = volumeLevelScalar;
            IsMuted = isMuted;
        }

        public string DeviceId { get; }
        public float VolumeLevelScalar { get; }
        public bool IsMuted { get; }
    }

    /// <summary>
    /// Internal notification client for device change events.
    /// </summary>
    private class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly AudioDeviceService _service;

        public DeviceNotificationClient(AudioDeviceService service)
        {
            _service = service;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            _service.OnDevicesChanged();
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _service.OnDevicesChanged();
        }

        public void OnDeviceRemoved(string deviceId)
        {
            _service.OnDevicesChanged();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Capture)
            {
                _service.OnDefaultDeviceChanged();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            // Could track mute/volume changes here if needed
        }
    }
}
