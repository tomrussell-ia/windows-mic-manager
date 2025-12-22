using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using MicrophoneManager.Models;

namespace MicrophoneManager.Services;

public class AudioDeviceService : IDisposable
{
    private static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly object _defaultVolumeNotificationLock = new();
    private string? _defaultCaptureDeviceIdWithVolumeNotifications;
    private AudioEndpointVolume? _defaultCaptureEndpointVolume;

    private readonly object _defaultInputMeterLock = new();
    private string? _defaultCaptureDeviceIdWithInputMeter;
    private WasapiCapture? _defaultCapture;
    private DateTime _lastInputMeterRaisedAtUtc = DateTime.MinValue;
    private bool _disposed;

    public event EventHandler? DevicesChanged;
    public event EventHandler? DefaultDeviceChanged;
    public event EventHandler<DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    public event EventHandler<DefaultMicrophoneInputLevelChangedEventArgs>? DefaultMicrophoneInputLevelChanged;

    public AudioDeviceService()
    {
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new DeviceNotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);

        // Track default microphone volume changes (e.g., changed by other apps)
        UpdateDefaultMicrophoneVolumeNotificationSubscription();

        // Track default microphone input level (real-time meter)
        UpdateDefaultMicrophoneInputMeterSubscription();
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
        UpdateDefaultMicrophoneInputMeterSubscription();
        DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDefaultMicrophoneInputMeterSubscription()
    {
        lock (_defaultInputMeterLock)
        {
            var defaultDeviceId = GetDefaultDeviceId(Role.Console);

            if (_defaultCaptureDeviceIdWithInputMeter == defaultDeviceId && _defaultCapture != null)
            {
                return;
            }

            if (_defaultCapture != null)
            {
                try
                {
                    _defaultCapture.DataAvailable -= OnDefaultCaptureDataAvailable;
                    _defaultCapture.RecordingStopped -= OnDefaultCaptureRecordingStopped;
                }
                catch { }

                try
                {
                    _defaultCapture.StopRecording();
                }
                catch { }

                try
                {
                    _defaultCapture.Dispose();
                }
                catch { }

                _defaultCapture = null;
                _defaultCaptureDeviceIdWithInputMeter = null;
            }

            if (defaultDeviceId == null) return;

            var device = GetDeviceById(defaultDeviceId);
            if (device == null) return;

            try
            {
                var capture = new WasapiCapture(device);
                capture.DataAvailable += OnDefaultCaptureDataAvailable;
                capture.RecordingStopped += OnDefaultCaptureRecordingStopped;
                capture.StartRecording();

                _defaultCapture = capture;
                _defaultCaptureDeviceIdWithInputMeter = defaultDeviceId;
            }
            catch
            {
                _defaultCapture = null;
                _defaultCaptureDeviceIdWithInputMeter = null;
            }
        }
    }

    private void OnDefaultCaptureRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // If recording stops unexpectedly, try to restart on the current default.
        // (Guarded to avoid tight loops)
        try
        {
            UpdateDefaultMicrophoneInputMeterSubscription();
        }
        catch { }
    }

    private void OnDefaultCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        string? deviceId;
        WasapiCapture? capture;

        lock (_defaultInputMeterLock)
        {
            deviceId = _defaultCaptureDeviceIdWithInputMeter;
            capture = _defaultCapture;
        }

        if (deviceId == null || capture == null) return;

        // Throttle UI-facing events to ~30Hz.
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastInputMeterRaisedAtUtc).TotalMilliseconds < 33)
        {
            return;
        }

        var peak = CalculatePeakAmplitude(e.Buffer, e.BytesRecorded, capture.WaveFormat);

        // Convert to dBFS and map [-60dB..0dB] => [0..100]
        var peakDb = peak <= 0 ? -60.0 : 20.0 * Math.Log10(Math.Max(peak, 1e-20));
        peakDb = Math.Max(-60.0, Math.Min(0.0, peakDb));
        var percent = (peakDb + 60.0) / 60.0 * 100.0;

        _lastInputMeterRaisedAtUtc = nowUtc;
        DefaultMicrophoneInputLevelChanged?.Invoke(
            this,
            new DefaultMicrophoneInputLevelChangedEventArgs(deviceId, percent, peakDb));
    }

    private static double CalculatePeakAmplitude(byte[] buffer, int bytesRecorded, WaveFormat waveFormat)
    {
        if (bytesRecorded <= 0) return 0.0;

        var blockAlign = waveFormat.BlockAlign;
        if (blockAlign <= 0) return 0.0;

        var usableBytes = bytesRecorded - (bytesRecorded % blockAlign);
        if (usableBytes <= 0) return 0.0;

        var encoding = waveFormat.Encoding;

        // Handle extensible formats (common for WASAPI shared mode)
        if (encoding == WaveFormatEncoding.Extensible && waveFormat is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == SubtypeIeeeFloat)
            {
                encoding = WaveFormatEncoding.IeeeFloat;
            }
            else if (extensible.SubFormat == SubtypePcm)
            {
                encoding = WaveFormatEncoding.Pcm;
            }
        }

        var channels = Math.Max(1, waveFormat.Channels);
        var bits = waveFormat.BitsPerSample;

        double peak = 0.0;

        if (encoding == WaveFormatEncoding.IeeeFloat && bits == 32)
        {
            var span = buffer.AsSpan(0, usableBytes);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            for (var i = 0; i < floats.Length; i++)
            {
                var v = Math.Abs(floats[i]);
                if (v > peak) peak = v;
            }
            return Math.Min(1.0, peak);
        }

        if (encoding == WaveFormatEncoding.Pcm && bits == 16)
        {
            var span = buffer.AsSpan(0, usableBytes);
            for (var i = 0; i < span.Length; i += 2)
            {
                var sample = (short)(span[i] | (span[i + 1] << 8));
                var v = Math.Abs(sample / 32768.0);
                if (v > peak) peak = v;
            }
            return Math.Min(1.0, peak);
        }

        if (encoding == WaveFormatEncoding.Pcm && bits == 24)
        {
            var span = buffer.AsSpan(0, usableBytes);
            for (var i = 0; i < span.Length; i += 3)
            {
                // 24-bit little endian signed
                var sample = span[i] | (span[i + 1] << 8) | (span[i + 2] << 16);
                if ((sample & 0x800000) != 0)
                {
                    sample |= unchecked((int)0xFF000000);
                }
                var v = Math.Abs(sample / 8388608.0);
                if (v > peak) peak = v;
            }
            return Math.Min(1.0, peak);
        }

        if (encoding == WaveFormatEncoding.Pcm && bits == 32)
        {
            var span = buffer.AsSpan(0, usableBytes);
            for (var i = 0; i < span.Length; i += 4)
            {
                var sample = span[i] | (span[i + 1] << 8) | (span[i + 2] << 16) | (span[i + 3] << 24);
                var v = Math.Abs(sample / 2147483648.0);
                if (v > peak) peak = v;
            }
            return Math.Min(1.0, peak);
        }

        // Fallback: treat as silence if we can't decode
        _ = channels;
        return 0.0;
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

        lock (_defaultInputMeterLock)
        {
            if (_defaultCapture != null)
            {
                try
                {
                    _defaultCapture.DataAvailable -= OnDefaultCaptureDataAvailable;
                    _defaultCapture.RecordingStopped -= OnDefaultCaptureRecordingStopped;
                }
                catch { }

                try
                {
                    _defaultCapture.StopRecording();
                }
                catch { }

                try
                {
                    _defaultCapture.Dispose();
                }
                catch { }

                _defaultCapture = null;
                _defaultCaptureDeviceIdWithInputMeter = null;
            }
        }

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

    public sealed class DefaultMicrophoneInputLevelChangedEventArgs : EventArgs
    {
        public DefaultMicrophoneInputLevelChangedEventArgs(string deviceId, double inputLevelPercent, double inputLevelDbFs)
        {
            DeviceId = deviceId;
            InputLevelPercent = inputLevelPercent;
            InputLevelDbFs = inputLevelDbFs;
        }

        public string DeviceId { get; }

        /// <summary>
        /// Meter percent mapped from dBFS range [-60..0] => [0..100].
        /// </summary>
        public double InputLevelPercent { get; }

        /// <summary>
        /// Peak level in dBFS (clamped to [-60..0]).
        /// </summary>
        public double InputLevelDbFs { get; }
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
