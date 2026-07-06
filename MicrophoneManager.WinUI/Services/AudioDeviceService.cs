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
    private readonly SemaphoreSlim _pollSemaphore = new(1, 1);

    private readonly object _capturesLock = new();
    private readonly Dictionary<string, MicrophoneCaptureState> _capturesByDeviceId = new();
    private readonly SemaphoreSlim _captureUpdateSemaphore = new(1, 1);
    private int _meteringRefCount;
    private volatile bool _disposed;

    private const double CaptureRestartBackoffSeconds = 5.0;

    private sealed class MicrophoneCaptureState
    {
        public required WasapiCapture Capture { get; init; }
        public required MMDevice Device { get; init; }
        public required string DeviceId { get; init; }
        public DateTime LastEventRaisedAtUtc { get; set; } = DateTime.MinValue;
        public double AccumulatedPeak { get; set; } = 0.0;
        public required string DeviceFormatSignature { get; init; }
        public bool IsStopped { get; set; } = false;
        public DateTime LastStopTimeUtc { get; set; } = DateTime.MinValue;
    }

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
    public event EventHandler<MicrophoneInputLevelChangedEventArgs>? MicrophoneInputLevelChanged;
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

        // Live input-level capture (WASAPI) is started on demand via AcquireMetering(),
        // not here — holding the microphone open for the whole process lifetime defeats
        // the point of a tray app and keeps the OS mic-in-use indicator lit.
    }

    /// <summary>
    /// Signals that a metering UI is visible. Starts microphone capture on the first
    /// acquire; nested acquires just increment the reference count.
    /// </summary>
    public void AcquireMetering()
    {
        bool shouldStart;
        lock (_capturesLock)
        {
            if (_disposed) return;
            shouldStart = _meteringRefCount == 0;
            _meteringRefCount++;
        }

        if (shouldStart)
        {
            _ = UpdateAllMicrophoneMeterSubscriptionsAsync();
        }
    }

    /// <summary>
    /// Signals that a metering UI is no longer visible. Stops microphone capture once
    /// the last outstanding acquire is released.
    /// </summary>
    public void ReleaseMetering()
    {
        bool shouldStop;
        lock (_capturesLock)
        {
            if (_meteringRefCount == 0) return;
            _meteringRefCount--;
            shouldStop = _meteringRefCount == 0;
        }

        if (shouldStop)
        {
            // Fire-and-forget: do not block the caller (may be on UI thread).
            // Re-check the ref count after acquiring the semaphore — a new AcquireMetering()
            // could have arrived between our decrement and when we get the semaphore; if so,
            // leave captures running.
            _ = Task.Run(async () =>
            {
                await _captureUpdateSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    lock (_capturesLock)
                    {
                        if (_meteringRefCount > 0) return;
                    }
                    StopAllCaptures();
                }
                finally
                {
                    _captureUpdateSemaphore.Release();
                }
            });
        }
    }

    private void StopAllCaptures()
    {
        List<MicrophoneCaptureState> capturesToDispose;
        lock (_capturesLock)
        {
            capturesToDispose = new List<MicrophoneCaptureState>(_capturesByDeviceId.Values);
            _capturesByDeviceId.Clear();
        }

        foreach (var state in capturesToDispose)
        {
            DisposeCapture(state);
        }
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

        // Drop this tick if a previous poll is still running to prevent concurrent dictionary mutation
        if (!_pollSemaphore.Wait(0)) return;

        List<MMDevice> devices;
        try
        {
            devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        }
        catch
        {
            _pollSemaphore.Release();
            return;
        }

        try
        {
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
                string deviceId = device.ID;

                try
                {
                    var endpoint = device.AudioEndpointVolume;
                    if (endpoint == null) continue;
                    volume = endpoint.MasterVolumeLevelScalar;
                    muted = endpoint.Mute;
                    formatTag = GetDeviceFormatInfo(device).Tag;
                }
                catch
                {
                    continue;
                }

                var hasVolumeChanged = false;
                var hasFormatChanged = false;

                if (_lastKnownStateById.TryGetValue(deviceId, out var prior))
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

                _lastKnownStateById[deviceId] = (volume, muted, formatTag);

                if (hasVolumeChanged)
                {
                    // Post events to UI thread
                    if (_syncContext != null)
                    {
                        var volumeArgs = new MicrophoneVolumeChangedEventArgs(deviceId, volume, muted);
                        _syncContext.Post(_ => MicrophoneVolumeChanged?.Invoke(this, volumeArgs), null);

                        if (defaultId != null && deviceId == defaultId)
                        {
                            var defaultVolumeArgs = new DefaultMicrophoneVolumeChangedEventArgs(deviceId, volume, muted);
                            _syncContext.Post(_ => DefaultMicrophoneVolumeChanged?.Invoke(this, defaultVolumeArgs), null);
                        }
                    }
                    else
                    {
                        MicrophoneVolumeChanged?.Invoke(
                            this,
                            new MicrophoneVolumeChangedEventArgs(deviceId, volume, muted));

                        if (defaultId != null && deviceId == defaultId)
                        {
                            DefaultMicrophoneVolumeChanged?.Invoke(
                                this,
                                new DefaultMicrophoneVolumeChangedEventArgs(deviceId, volume, muted));
                        }
                    }
                }

                if (hasFormatChanged)
                {
                    // Recreate captures when format changes
                    _ = UpdateAllMicrophoneMeterSubscriptionsAsync();

                    // Post events to UI thread
                    if (_syncContext != null)
                    {
                        var formatArgs = new MicrophoneFormatChangedEventArgs(deviceId, formatTag);
                        _syncContext.Post(_ => MicrophoneFormatChanged?.Invoke(this, formatArgs), null);
                    }
                    else
                    {
                        MicrophoneFormatChanged?.Invoke(
                            this,
                            new MicrophoneFormatChangedEventArgs(deviceId, formatTag));
                    }
                }
            }

            // Trigger capture restart if any devices have stopped captures past their backoff window
            bool hasStoppedCaptures;
            lock (_capturesLock)
            {
                hasStoppedCaptures = _capturesByDeviceId.Values.Any(s => s.IsStopped);
            }
            if (hasStoppedCaptures)
                _ = UpdateAllMicrophoneMeterSubscriptionsAsync();
        }
        finally
        {
            foreach (var device in devices)
            {
                try { device.Dispose(); } catch { }
            }
            _pollSemaphore.Release();
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
        using var device = GetDeviceById(deviceId);
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
                var formatInfo = GetDeviceFormatInfo(device);
                var mic = new MicrophoneDevice
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    IsDefault = device.ID == defaultId,
                    IsDefaultCommunication = device.ID == defaultCommId,
                    IsMuted = GetDeviceMuteState(device),
                    VolumeLevel = GetDeviceVolume(device),
                    FormatTag = formatInfo.Tag,
                    SampleRateHz = formatInfo.SampleRateHz,
                    BitsPerSample = formatInfo.BitsPerSample,
                    FidelityTier = ComputeFidelityTier(formatInfo.SampleRateHz, formatInfo.BitsPerSample),
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
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role);
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
        using var device = GetDeviceById(deviceId);
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
        using var device = GetDeviceById(deviceId);
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

    private static (string Tag, int SampleRateHz, int BitsPerSample) GetDeviceFormatInfo(MMDevice device)
    {
        try
        {
            var format = device.AudioClient?.MixFormat;
            if (format == null) return ("Unknown format", 0, 0);

            var sampleRateKhz = format.SampleRate / 1000.0;
            var bits = format.BitsPerSample;
            var channels = format.Channels;

            var channelLabel = channels switch
            {
                1 => "Mono",
                2 => "Stereo",
                _ => $"{channels}-ch"
            };

            return ($"{sampleRateKhz:0.#} kHz {bits}-bit {channelLabel}", format.SampleRate, bits);
        }
        catch
        {
            return ("Unknown format", 0, 0);
        }
    }

    private static Models.FidelityTier ComputeFidelityTier(int sampleRateHz, int bitsPerSample)
    {
        if (sampleRateHz >= 88200 || (sampleRateHz >= 44100 && bitsPerSample >= 24))
            return Models.FidelityTier.Studio;
        if (sampleRateHz >= 44100)
            return Models.FidelityTier.High;
        if (sampleRateHz >= 22050 && bitsPerSample >= 16)
            return Models.FidelityTier.Standard;
        return Models.FidelityTier.Reduced;
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

            // Update meter subscriptions when devices added/removed
            await UpdateAllMicrophoneMeterSubscriptionsAsync().ConfigureAwait(false);

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

    private async Task UpdateAllMicrophoneMeterSubscriptionsAsync()
    {
        // Prevent concurrent capture reconciliation tasks from piling up
        if (!_captureUpdateSemaphore.Wait(0)) return;

        try
        {
            await Task.Run(() =>
            {
                // No metering UI is visible — don't (re)create captures. StopAllCaptures()
                // already tore down anything that was running when the last release fired.
                lock (_capturesLock)
                {
                    if (_meteringRefCount == 0) return;
                }

                // Get all active capture devices
                List<MMDevice> activeDevices;
                try
                {
                    activeDevices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                }
                catch { return; }

                var activeIds = new HashSet<string>(activeDevices.Select(d => d.ID));

                // Collect captures to dispose (do not dispose while holding _capturesLock to
                // avoid deadlock with OnCaptureRecordingStopped which also acquires _capturesLock)
                var capturesToDispose = new List<MicrophoneCaptureState>();

                lock (_capturesLock)
                {
                    var removedIds = _capturesByDeviceId.Keys.Where(id => !activeIds.Contains(id)).ToList();
                    foreach (var deviceId in removedIds)
                    {
                        if (_capturesByDeviceId.TryGetValue(deviceId, out var state))
                        {
                            capturesToDispose.Add(state);
                            _capturesByDeviceId.Remove(deviceId);
                        }
                    }
                }

                // Dispose removed captures outside the lock
                foreach (var state in capturesToDispose)
                    DisposeCapture(state);
                capturesToDispose.Clear();

                // Add/update captures for active devices
                foreach (var device in activeDevices)
                {
                    var formatSig = GetDeviceFormatSignature(device);
                    MicrophoneCaptureState? toDispose = null;
                    bool shouldCreate = false;

                    lock (_capturesLock)
                    {
                        if (_capturesByDeviceId.TryGetValue(device.ID, out var existingState))
                        {
                            bool isRunning = !existingState.IsStopped;
                            if (existingState.DeviceFormatSignature == formatSig && isRunning)
                            {
                                // Capture is healthy — dispose the temporary device reference
                                try { device.Dispose(); } catch { }
                                continue;
                            }

                            // If stopped, enforce backoff before recreating
                            if (existingState.IsStopped && existingState.LastStopTimeUtc != DateTime.MinValue)
                            {
                                var elapsed = (DateTime.UtcNow - existingState.LastStopTimeUtc).TotalSeconds;
                                if (elapsed < CaptureRestartBackoffSeconds)
                                {
                                    try { device.Dispose(); } catch { }
                                    continue;
                                }
                            }

                            toDispose = existingState;
                            _capturesByDeviceId.Remove(device.ID);
                        }
                        else
                        {
                            shouldCreate = true;
                        }
                    }

                    // Dispose old capture outside the lock
                    if (toDispose != null)
                    {
                        DisposeCapture(toDispose);
                        shouldCreate = true;
                    }

                    if (!shouldCreate)
                    {
                        try { device.Dispose(); } catch { }
                        continue;
                    }

                    // Create new capture — device ownership transfers to MicrophoneCaptureState
                    WasapiCapture? newCapture = null;
                    try
                    {
                        newCapture = new WasapiCapture(device, true, 5);
                        newCapture.DataAvailable += OnCaptureDataAvailable;
                        newCapture.RecordingStopped += OnCaptureRecordingStopped;
                        newCapture.StartRecording();

                        lock (_capturesLock)
                        {
                            _capturesByDeviceId[device.ID] = new MicrophoneCaptureState
                            {
                                Capture = newCapture,
                                Device = device,
                                DeviceId = device.ID,
                                DeviceFormatSignature = formatSig
                            };
                        }
                    }
                    catch
                    {
                        // Creation failed — clean up both the capture and device
                        if (newCapture != null)
                        {
                            try { newCapture.DataAvailable -= OnCaptureDataAvailable; } catch { }
                            try { newCapture.RecordingStopped -= OnCaptureRecordingStopped; } catch { }
                            try { newCapture.Dispose(); } catch { }
                        }
                        try { device.Dispose(); } catch { }
                    }
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            _captureUpdateSemaphore.Release();
        }
    }

    private static string GetDeviceFormatSignature(MMDevice device)
    {
        try
        {
            var format = device.AudioClient?.MixFormat;
            if (format == null) return "Unknown";
            return $"{format.SampleRate}:{format.BitsPerSample}:{format.Channels}:{format.Encoding}";
        }
        catch { return "Unknown"; }
    }

    private void DisposeCapture(MicrophoneCaptureState state)
    {
        try { state.Capture.DataAvailable -= OnCaptureDataAvailable; } catch { }
        try { state.Capture.RecordingStopped -= OnCaptureRecordingStopped; } catch { }
        try { state.Capture.StopRecording(); } catch { }
        try { state.Capture.Dispose(); } catch { }
        try { state.Device.Dispose(); } catch { }
    }

    private void OnCaptureRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is WasapiCapture capture)
        {
            lock (_capturesLock)
            {
                var state = _capturesByDeviceId.Values.FirstOrDefault(s => ReferenceEquals(s.Capture, capture));
                if (state != null)
                {
                    state.IsStopped = true;
                    state.LastStopTimeUtc = DateTime.UtcNow;
                }
            }
        }
        _ = UpdateAllMicrophoneMeterSubscriptionsAsync();
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!(sender is WasapiCapture capture)) return;

        MicrophoneCaptureState? state = null;
        lock (_capturesLock)
        {
            state = _capturesByDeviceId.Values.FirstOrDefault(s => ReferenceEquals(s.Capture, capture));
        }
        if (state == null || state.IsStopped) return;

        // Accumulate peak
        var bufferPeak = CalculatePeakAmplitude(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        state.AccumulatedPeak = Math.Max(state.AccumulatedPeak, bufferPeak);

        // Throttle to ~120Hz per device
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - state.LastEventRaisedAtUtc).TotalMilliseconds < 8)
            return;

        var peak = state.AccumulatedPeak;
        state.AccumulatedPeak = 0.0;
        state.LastEventRaisedAtUtc = nowUtc;

        // Convert to dBFS and percent
        var peakDb = ObsMeterMath.ClampMeterDb(ObsMeterMath.MulToDb(peak));
        var percent = ObsMeterMath.DbToPercent(peakDb);

        var args = new MicrophoneInputLevelChangedEventArgs(state.DeviceId, percent, peakDb);
        if (_syncContext != null)
            _syncContext.Post(_ => MicrophoneInputLevelChanged?.Invoke(this, args), null);
        else
            MicrophoneInputLevelChanged?.Invoke(this, args);
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
        lock (_capturesLock)
        {
            if (_disposed) return;
            _disposed = true;
        }
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

        _captureUpdateSemaphore.Wait();
        try
        {
            StopAllCaptures();
        }
        finally
        {
            _captureUpdateSemaphore.Release();
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

    public sealed class MicrophoneInputLevelChangedEventArgs : EventArgs
    {
        public MicrophoneInputLevelChangedEventArgs(string deviceId, double inputLevelPercent, double inputLevelDbFs)
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
