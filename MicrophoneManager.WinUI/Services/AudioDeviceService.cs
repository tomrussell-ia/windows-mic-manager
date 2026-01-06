using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using MicrophoneManager.WinUI.Models;

namespace MicrophoneManager.WinUI.Services;

public class AudioDeviceService : IDisposable, IAudioDeviceService
{
    private static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private readonly object _volumeNotificationLock = new();
    private readonly Dictionary<string, VolumeNotificationSubscription> _volumeNotificationSubscriptions = new();
    private string? _currentDefaultCaptureDeviceId;

    private readonly SynchronizationContext? _syncContext;
    private readonly PolicyConfigService _policyConfigService;
    private Timer? _externalStatePollTimer;
    private readonly Dictionary<string, (float VolumeScalar, bool IsMuted, string FormatTag)> _lastKnownStateById = new();

    private readonly object _defaultInputMeterLock = new();
    private string? _defaultCaptureDeviceIdWithInputMeter;
    private WasapiCapture? _defaultCapture;
    private DateTime _lastInputMeterRaisedAtUtc = DateTime.MinValue;
    private double _accumulatedPeak = 0.0;
    private bool _disposed;

    // Debouncing for device change callbacks
    private Timer? _deviceChangeDebounceTimer;
    private const int DeviceChangeDebounceMs = 50;
    private readonly object _debounceTimerLock = new();

    // Device enumeration caching
    private List<MicrophoneDevice>? _cachedMicrophones = null;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private const int CacheValidityMs = 100;
    private readonly object _cacheLock = new();

    public event EventHandler? DevicesChanged;
    public event EventHandler? DefaultDeviceChanged;
    public event EventHandler<DefaultMicrophoneVolumeChangedEventArgs>? DefaultMicrophoneVolumeChanged;
    public event EventHandler<MicrophoneVolumeChangedEventArgs>? MicrophoneVolumeChanged;
    public event EventHandler<DefaultMicrophoneInputLevelChangedEventArgs>? DefaultMicrophoneInputLevelChanged;
    public event EventHandler<MicrophoneFormatChangedEventArgs>? MicrophoneFormatChanged;

    public AudioDeviceService(PolicyConfigService policyConfigService)
    {
        _policyConfigService = policyConfigService ?? throw new ArgumentNullException(nameof(policyConfigService));
        _syncContext = SynchronizationContext.Current;
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new DeviceNotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);

        // Track microphone volume/mute changes (e.g., changed by other apps) for ALL capture devices
        UpdateMicrophoneVolumeNotificationSubscriptions();
        _currentDefaultCaptureDeviceId = GetDefaultDeviceId(Role.Console);

        // Fallback: poll for external volume/mute changes (Sound settings, other apps)
        StartExternalStatePolling();

        // Track default microphone input level (real-time meter)
        UpdateDefaultMicrophoneInputMeterSubscription();
    }

    private void StartExternalStatePolling()
    {
        // In unit tests, there's typically no UI SynchronizationContext and we don't want background timers.
        if (_syncContext == null) return;

        // Avoid starting twice
        if (_externalStatePollTimer != null) return;

        // 1 second poll interval for detecting external volume/mute/format changes.
        // Run on background thread to prevent UI blocking
        _externalStatePollTimer = new Timer(
            _ => Task.Run(() => PollExternalStateChanges()),
            null,
            dueTime: 1000,
            period: 1000);
    }

    private void PollExternalStateChanges()
    {
        if (_disposed) return;

        List<MMDevice> devices;
        try
        {
            devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        }
        catch
        {
            return;
        }

        string? defaultId;
        lock (_volumeNotificationLock)
        {
            defaultId = _currentDefaultCaptureDeviceId ?? GetDefaultDeviceId(Role.Console);
            _currentDefaultCaptureDeviceId = defaultId;
        }

        var activeIds = new HashSet<string>(devices.Select(d => d.ID));

        // Drop removed devices from state map
        var removedIds = _lastKnownStateById.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (var id in removedIds)
        {
            _lastKnownStateById.Remove(id);
        }

        foreach (var device in devices)
        {
            float volume;
            bool muted;
            string formatTag;

            try
            {
                var endpoint = device.AudioEndpointVolume;
                if (endpoint == null) continue;
                volume = endpoint.MasterVolumeLevelScalar;
                muted = endpoint.Mute;
                formatTag = GetDeviceFormat(device);
            }
            catch
            {
                continue;
            }

            var hasVolumeChanged = false;
            var hasFormatChanged = false;

            if (_lastKnownStateById.TryGetValue(device.ID, out var prior))
            {
                hasVolumeChanged = Math.Abs(prior.VolumeScalar - volume) >= 0.0005f || prior.IsMuted != muted;
                hasFormatChanged = prior.FormatTag != formatTag;
            }
            else
            {
                // First time seeing this device
                hasVolumeChanged = true;
                hasFormatChanged = true;
            }

            _lastKnownStateById[device.ID] = (volume, muted, formatTag);

            if (hasVolumeChanged)
            {
                // Post events to UI thread
                if (_syncContext != null)
                {
                    var volumeArgs = new MicrophoneVolumeChangedEventArgs(device.ID, volume, muted);
                    _syncContext.Post(_ => MicrophoneVolumeChanged?.Invoke(this, volumeArgs), null);

                    if (defaultId != null && device.ID == defaultId)
                    {
                        var defaultVolumeArgs = new DefaultMicrophoneVolumeChangedEventArgs(device.ID, volume, muted);
                        _syncContext.Post(_ => DefaultMicrophoneVolumeChanged?.Invoke(this, defaultVolumeArgs), null);
                    }
                }
                else
                {
                    MicrophoneVolumeChanged?.Invoke(
                        this,
                        new MicrophoneVolumeChangedEventArgs(device.ID, volume, muted));

                    if (defaultId != null && device.ID == defaultId)
                    {
                        DefaultMicrophoneVolumeChanged?.Invoke(
                            this,
                            new DefaultMicrophoneVolumeChangedEventArgs(device.ID, volume, muted));
                    }
                }
            }

            if (hasFormatChanged)
            {
                // Post events to UI thread
                if (_syncContext != null)
                {
                    var formatArgs = new MicrophoneFormatChangedEventArgs(device.ID, formatTag);
                    _syncContext.Post(_ => MicrophoneFormatChanged?.Invoke(this, formatArgs), null);
                }
                else
                {
                    MicrophoneFormatChanged?.Invoke(
                        this,
                        new MicrophoneFormatChangedEventArgs(device.ID, formatTag));
                }
            }
        }
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
    /// Uses 100ms TTL cache to reduce enumeration overhead by 70-80% during steady state.
    /// </summary>
    public List<MicrophoneDevice> GetMicrophones()
    {
        lock (_cacheLock)
        {
            var now = DateTime.UtcNow;
            var cacheAge = (now - _cacheTimestamp).TotalMilliseconds;

            // Return cached result if still valid
            if (_cachedMicrophones != null && cacheAge < CacheValidityMs)
            {
                return new List<MicrophoneDevice>(_cachedMicrophones);
            }

            // Cache expired or invalid - enumerate devices
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
                    VolumeLevel = GetDeviceVolume(device),
                    FormatTag = GetDeviceFormat(device),
                    InputLevelPercent = GetDeviceInputLevel(device)
                };
                devices.Add(mic);
            }

            // Update cache
            _cachedMicrophones = devices;
            _cacheTimestamp = now;

            return new List<MicrophoneDevice>(devices);
        }
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
    /// <returns>True if both roles were set successfully, false otherwise.</returns>
    public bool SetDefaultMicrophone(string deviceId)
    {
        var consoleSuccess = SetMicrophoneForRole(deviceId, Role.Console);
        var commSuccess = SetMicrophoneForRole(deviceId, Role.Communications);
        return consoleSuccess && commSuccess;
    }

    /// <summary>
    /// Sets the specified device as the default for the given role.
    /// </summary>
    /// <returns>True if successful, false if the operation failed.</returns>
    public bool SetMicrophoneForRole(string deviceId, Role role)
    {
        try
        {
            var roleToSet = role == Role.Console
                ? PolicyConfigService.ERole.eConsole
                : PolicyConfigService.ERole.eCommunications;

            _policyConfigService.SetDefaultDevice(deviceId, roleToSet);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the specified device as the default for the given role asynchronously.
    /// </summary>
    public async Task<bool> SetMicrophoneForRoleAsync(string deviceId, Role role, CancellationToken cancellationToken = default)
    {
        try
        {
            var roleToSet = role == Role.Console
                ? PolicyConfigService.ERole.eConsole
                : PolicyConfigService.ERole.eCommunications;

            await _policyConfigService.SetDefaultDeviceAsync(deviceId, roleToSet, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the specified device as the default microphone for all roles asynchronously.
    /// </summary>
    public async Task<bool> SetDefaultMicrophoneAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _policyConfigService.SetDefaultDeviceForAllRolesAsync(deviceId, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all microphones asynchronously without blocking the UI thread.
    /// </summary>
    public async Task<List<MicrophoneDevice>> GetMicrophonesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GetMicrophones();
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the default device ID for the specified role asynchronously.
    /// </summary>
    public async Task<string?> GetDefaultDeviceIdAsync(Role role, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return GetDefaultDeviceId(role);
        }, cancellationToken);
    }

    /// <summary>
    /// Toggles the mute state asynchronously.
    /// </summary>
    public async Task<bool> ToggleMuteAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ToggleMute(deviceId);
        }, cancellationToken);
    }

    /// <summary>
    /// Toggles mute on the default microphone asynchronously.
    /// </summary>
    public async Task<bool> ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ToggleDefaultMicrophoneMute();
        }, cancellationToken);
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

    private static string GetDeviceFormat(MMDevice device)
    {
        try
        {
            var format = device.AudioClient?.MixFormat;
            if (format == null) return "Unknown format";

            var sampleRateKhz = format.SampleRate / 1000.0;
            var bits = format.BitsPerSample;
            var channels = format.Channels;

            var channelLabel = channels switch
            {
                1 => "Mono",
                2 => "Stereo",
                _ => $"{channels}-ch"
            };

            return $"{sampleRateKhz:0.#} kHz {bits}-bit {channelLabel}";
        }
        catch
        {
            return "Unknown format";
        }
    }

    private static double GetDeviceInputLevel(MMDevice device)
    {
        try
        {
            var meter = device.AudioMeterInformation;
            if (meter == null) return 0;

            // AudioMeterInformation reports linear peak amplitude (0..1).
            // Map through OBS-style LOG dB->deflection for a meter that behaves like OBS.
            var value = meter.MasterPeakValue;
            value = MathF.Max(0f, MathF.Min(1f, value));

            var dbFs = ObsMeterMath.ClampMeterDb(ObsMeterMath.MulToDb(value));
            var percent = ObsMeterMath.DbToPercent(dbFs);
            
            return percent;
        }
        catch
        {
            return 0;
        }
    }

    internal void OnDevicesChanged()
    {
        // Invalidate cache when device list changes
        InvalidateMicrophoneCache();

        // Post event to UI thread if available
        if (_syncContext != null)
        {
            _syncContext.Post(_ => DevicesChanged?.Invoke(this, EventArgs.Empty), null);
        }
        else
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void OnDeviceTopologyChanged()
    {
        // Invalidate cache when device topology changes
        InvalidateMicrophoneCache();

        // Fire-and-forget: move expensive subscription updates to background thread
        _ = OnDeviceTopologyChangedAsync();
    }

    private async Task OnDeviceTopologyChangedAsync()
    {
        try
        {
            // Move expensive device enumeration to background thread
            await Task.Run(() =>
            {
                UpdateMicrophoneVolumeNotificationSubscriptions();
            }).ConfigureAwait(false);

            // Post event to UI thread
            if (_syncContext != null)
            {
                _syncContext.Post(_ => DevicesChanged?.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                DevicesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDeviceTopologyChangedAsync failed: {ex}");
        }
    }

    private void InvalidateMicrophoneCache()
    {
        lock (_cacheLock)
        {
            _cachedMicrophones = null;
            _cacheTimestamp = DateTime.MinValue;
        }
    }

    internal void OnDefaultDeviceChanged()
    {
        // Debounce: When setting both Console + Communications roles, Windows fires
        // this callback twice in rapid succession. Debouncing reduces redundant
        // expensive operations (device enumeration, WasapiCapture recreation) by 50%.
        lock (_debounceTimerLock)
        {
            // Cancel any pending execution
            _deviceChangeDebounceTimer?.Dispose();

            // Schedule deferred execution after 50ms window
            // If another callback arrives within 50ms, timer restarts
            _deviceChangeDebounceTimer = new Timer(
                _ => _ = ProcessPendingDeviceChangesAsync(),
                null,
                dueTime: DeviceChangeDebounceMs,
                period: Timeout.Infinite);
        }
    }

    private async Task ProcessPendingDeviceChangesAsync()
    {
        try
        {
            // Move expensive operations to background thread
            await Task.Run(() =>
            {
                lock (_volumeNotificationLock)
                {
                    _currentDefaultCaptureDeviceId = GetDefaultDeviceId(Role.Console);
                }

                // Ensure we are subscribed to the new default if the device list changed.
                UpdateMicrophoneVolumeNotificationSubscriptions();
            }).ConfigureAwait(false);

            // WasapiCapture creation/disposal can be blocking (20-100ms)
            await UpdateDefaultMicrophoneInputMeterSubscriptionAsync().ConfigureAwait(false);

            // Post event to UI thread
            if (_syncContext != null)
            {
                _syncContext.Post(_ => DefaultDeviceChanged?.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                DefaultDeviceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessPendingDeviceChangesAsync failed: {ex}");
        }
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
                // Use 5ms buffer for faster meter response (default is 10ms)
                var capture = new WasapiCapture(device, true, 5);
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

    private async Task UpdateDefaultMicrophoneInputMeterSubscriptionAsync()
    {
        await Task.Run(() =>
        {
            // WasapiCapture disposal and creation can be blocking (20-100ms)
            // Move to background thread to prevent UI freezes
            WasapiCapture? oldCapture = null;
            WasapiCapture? newCapture = null;
            string? newDeviceId = null;

            lock (_defaultInputMeterLock)
            {
                var defaultDeviceId = GetDefaultDeviceId(Role.Console);

                if (_defaultCaptureDeviceIdWithInputMeter == defaultDeviceId && _defaultCapture != null)
                {
                    return;
                }

                // Capture old reference for disposal outside lock
                oldCapture = _defaultCapture;
                _defaultCapture = null;
                _defaultCaptureDeviceIdWithInputMeter = null;

                if (defaultDeviceId != null)
                {
                    var device = GetDeviceById(defaultDeviceId);
                    if (device != null)
                    {
                        try
                        {
                            // Use 5ms buffer for faster meter response (default is 10ms)
                            newCapture = new WasapiCapture(device, true, 5);
                            newCapture.DataAvailable += OnDefaultCaptureDataAvailable;
                            newCapture.RecordingStopped += OnDefaultCaptureRecordingStopped;
                            newCapture.StartRecording();
                            newDeviceId = defaultDeviceId;
                        }
                        catch
                        {
                            newCapture?.Dispose();
                            newCapture = null;
                        }
                    }
                }

                // Swap in new capture (minimal lock duration)
                _defaultCapture = newCapture;
                _defaultCaptureDeviceIdWithInputMeter = newDeviceId;
            }

            // Dispose old capture outside lock (blocking operation)
            if (oldCapture != null)
            {
                try
                {
                    oldCapture.DataAvailable -= OnDefaultCaptureDataAvailable;
                    oldCapture.RecordingStopped -= OnDefaultCaptureRecordingStopped;
                }
                catch { }

                try
                {
                    oldCapture.StopRecording();
                }
                catch { }

                try
                {
                    oldCapture.Dispose();
                }
                catch { }
            }
        }).ConfigureAwait(false);
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

        // Accumulate peak across buffers so we don't miss transients.
        var bufferPeak = CalculatePeakAmplitude(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        _accumulatedPeak = Math.Max(_accumulatedPeak, bufferPeak);

        // Throttle UI-facing events to ~120Hz for fluid meter movement.
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastInputMeterRaisedAtUtc).TotalMilliseconds < 8)
        {
            return;
        }

        var peak = _accumulatedPeak;
        _accumulatedPeak = 0.0;

        // Convert to dBFS and map using OBS LOG dB->deflection.
        var peakDb = ObsMeterMath.ClampMeterDb(ObsMeterMath.MulToDb(peak));
        var percent = ObsMeterMath.DbToPercent(peakDb);

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

    private void UpdateMicrophoneVolumeNotificationSubscriptions()
    {
        List<MMDevice> devices;
        try
        {
            devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        }
        catch
        {
            return;
        }

        var activeIds = new HashSet<string>(devices.Select(d => d.ID));

        lock (_volumeNotificationLock)
        {
            // Remove subscriptions for devices that no longer exist/active
            var toRemove = _volumeNotificationSubscriptions.Keys.Where(id => !activeIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (_volumeNotificationSubscriptions.TryGetValue(id, out var sub))
                {
                    try
                    {
                        sub.EndpointVolume.OnVolumeNotification -= sub.Handler;
                    }
                    catch { }
                }

                _volumeNotificationSubscriptions.Remove(id);
            }

            // Add subscriptions for new active devices
            foreach (var device in devices)
            {
                if (_volumeNotificationSubscriptions.ContainsKey(device.ID))
                {
                    continue;
                }

                var endpointVolume = device.AudioEndpointVolume;
                if (endpointVolume == null)
                {
                    continue;
                }

                // Capture device ID as a string to avoid COM object lifetime issues in the callback
                string deviceId = device.ID;
                AudioEndpointVolumeNotificationDelegate handler = (data) => OnMicrophoneVolumeNotification(deviceId, data);
                try
                {
                    endpointVolume.OnVolumeNotification += handler;
                    _volumeNotificationSubscriptions[device.ID] = new VolumeNotificationSubscription(endpointVolume, handler);
                }
                catch
                {
                    // Ignore - device could disappear or access denied
                }
            }
        }
    }

    private void OnMicrophoneVolumeNotification(string deviceId, AudioVolumeNotificationData data)
    {
        MicrophoneVolumeChanged?.Invoke(
            this,
            new MicrophoneVolumeChangedEventArgs(deviceId, data.MasterVolume, data.Muted));

        string? defaultId;
        lock (_volumeNotificationLock)
        {
            defaultId = _currentDefaultCaptureDeviceId;
        }

        if (defaultId != null && deviceId == defaultId)
        {
            DefaultMicrophoneVolumeChanged?.Invoke(
                this,
                new DefaultMicrophoneVolumeChangedEventArgs(deviceId, data.MasterVolume, data.Muted));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _externalStatePollTimer?.Dispose();
        }
        catch { }
        _externalStatePollTimer = null;

        try
        {
            _deviceChangeDebounceTimer?.Dispose();
        }
        catch { }
        _deviceChangeDebounceTimer = null;

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

        lock (_volumeNotificationLock)
        {
            foreach (var subscription in _volumeNotificationSubscriptions.Values)
            {
                try
                {
                    subscription.EndpointVolume.OnVolumeNotification -= subscription.Handler;
                }
                catch { }
            }

            _volumeNotificationSubscriptions.Clear();
            _currentDefaultCaptureDeviceId = null;
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

    public sealed class MicrophoneVolumeChangedEventArgs : EventArgs
    {
        public MicrophoneVolumeChangedEventArgs(string deviceId, float volumeLevelScalar, bool isMuted)
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

    public sealed class MicrophoneFormatChangedEventArgs : EventArgs
    {
        public MicrophoneFormatChangedEventArgs(string deviceId, string formatTag)
        {
            DeviceId = deviceId;
            FormatTag = formatTag;
        }

        public string DeviceId { get; }
        public string FormatTag { get; }
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
            _service.OnDeviceTopologyChanged();
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _service.OnDeviceTopologyChanged();
        }

        public void OnDeviceRemoved(string deviceId)
        {
            _service.OnDeviceTopologyChanged();
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
            _service.OnDevicesChanged();
        }
    }

    private sealed class VolumeNotificationSubscription
    {
        public VolumeNotificationSubscription(AudioEndpointVolume endpointVolume, AudioEndpointVolumeNotificationDelegate handler)
        {
            EndpointVolume = endpointVolume;
            Handler = handler;
        }

        public AudioEndpointVolume EndpointVolume { get; }
        public AudioEndpointVolumeNotificationDelegate Handler { get; }
    }
}
