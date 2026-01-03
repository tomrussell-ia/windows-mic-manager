using System.Text.Json;

namespace MicrophoneManager.Interop;

/// <summary>
/// Exception thrown when a Rust engine operation fails.
/// </summary>
public class MicEngineException : Exception
{
    public int ErrorCode { get; }

    public MicEngineException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// High-level wrapper for the Rust mic engine FFI.
/// Thread-safe: each call initializes COM for the calling thread.
/// </summary>
public sealed class MicEngine : IDisposable
{
    private readonly MicEngineSafeHandle _handle;
    private bool _disposed;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Create a new mic engine instance.
    /// </summary>
    /// <param name="configJson">Optional JSON configuration</param>
    /// <exception cref="MicEngineException">Thrown if engine creation fails</exception>
    public MicEngine(string? configJson = null)
    {
        var ptr = MicEngineNative.Create(configJson);
        if (ptr == IntPtr.Zero)
        {
            ThrowLastError("Failed to create mic engine");
        }
        _handle = new MicEngineSafeHandle(ptr);
    }

    /// <summary>
    /// Get the Rust library version.
    /// </summary>
    public static string GetVersion()
    {
        using var rustStr = new RustString(MicEngineNative.Version());
        return rustStr.AsString() ?? "unknown";
    }

    /// <summary>
    /// Get all microphone devices.
    /// </summary>
    /// <returns>List of microphone devices</returns>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public List<MicDeviceDto> GetDevices()
    {
        ThrowIfDisposed();

        IntPtr ptr;
        lock (_lock)
        {
            ptr = MicEngineNative.GetDevices(_handle.DangerousGetHandle());
        }

        if (ptr == IntPtr.Zero)
        {
            ThrowLastError("Failed to get devices");
        }

        using var rustStr = new RustString(ptr);
        var json = rustStr.AsString();
        if (string.IsNullOrEmpty(json))
        {
            return new List<MicDeviceDto>();
        }

        var response = JsonSerializer.Deserialize<DeviceListResponseDto>(json, JsonOptions);
        return response?.Devices ?? new List<MicDeviceDto>();
    }

    /// <summary>
    /// Get a specific microphone device by ID.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <returns>Device info, or null if not found</returns>
    /// <exception cref="MicEngineException">Thrown if operation fails (except not found)</exception>
    public MicDeviceDto? GetDevice(string deviceId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceId);

        IntPtr ptr;
        lock (_lock)
        {
            ptr = MicEngineNative.GetDevice(_handle.DangerousGetHandle(), deviceId);
        }

        if (ptr == IntPtr.Zero)
        {
            // Check if it's a "not found" error (code -3)
            var errorCode = MicEngineNative.LastErrorCode();
            if (errorCode == -3) // DeviceNotFound
            {
                return null;
            }
            ThrowLastError("Failed to get device");
        }

        using var rustStr = new RustString(ptr);
        var json = rustStr.AsString();
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var response = JsonSerializer.Deserialize<DeviceResponseDto>(json, JsonOptions);
        return response?.Device;
    }

    /// <summary>
    /// Set a device as the default for a specific role.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="role">Device role</param>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public void SetDefaultDevice(string deviceId, MicDeviceRole role)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceId);

        int result;
        lock (_lock)
        {
            result = MicEngineNative.SetDefaultDevice(
                _handle.DangerousGetHandle(),
                deviceId,
                (uint)role);
        }

        if (result != 0)
        {
            ThrowLastError($"Failed to set default device (role={role})");
        }
    }

    /// <summary>
    /// Set a device as the default for all roles (Console and Communications).
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public void SetDefaultDeviceAllRoles(string deviceId)
    {
        SetDefaultDevice(deviceId, MicDeviceRole.Console);
        SetDefaultDevice(deviceId, MicDeviceRole.Communications);
    }

    /// <summary>
    /// Set the volume level for a device.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="volume">Volume level (0.0 to 1.0)</param>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public void SetVolume(string deviceId, float volume)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceId);

        volume = Math.Clamp(volume, 0f, 1f);

        int result;
        lock (_lock)
        {
            result = MicEngineNative.SetVolume(
                _handle.DangerousGetHandle(),
                deviceId,
                volume);
        }

        if (result != 0)
        {
            ThrowLastError("Failed to set volume");
        }
    }

    /// <summary>
    /// Toggle the mute state of a device.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <returns>The new mute state (true = muted)</returns>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public bool ToggleMute(string deviceId)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceId);

        IntPtr ptr;
        lock (_lock)
        {
            ptr = MicEngineNative.ToggleMute(_handle.DangerousGetHandle(), deviceId);
        }

        if (ptr == IntPtr.Zero)
        {
            ThrowLastError("Failed to toggle mute");
        }

        using var rustStr = new RustString(ptr);
        var json = rustStr.AsString();
        if (string.IsNullOrEmpty(json))
        {
            throw new MicEngineException(-1, "Empty response from toggle mute");
        }

        var response = JsonSerializer.Deserialize<OperationResultDto>(json, JsonOptions);
        if (response?.Success != true)
        {
            throw new MicEngineException(-1, response?.Error ?? "Toggle mute failed");
        }

        return response.IsMuted ?? false;
    }

    /// <summary>
    /// Set the mute state of a device.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="muted">True to mute, false to unmute</param>
    /// <exception cref="MicEngineException">Thrown if operation fails</exception>
    public void SetMute(string deviceId, bool muted)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(deviceId);

        int result;
        lock (_lock)
        {
            result = MicEngineNative.SetMute(
                _handle.DangerousGetHandle(),
                deviceId,
                muted ? 1 : 0);
        }

        if (result != 0)
        {
            ThrowLastError("Failed to set mute state");
        }
    }

    /// <summary>
    /// Get the mute state of a device.
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <returns>True if muted, false otherwise</returns>
    public bool IsMuted(string deviceId)
    {
        var device = GetDevice(deviceId);
        return device?.IsMuted ?? false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MicEngine));
        }
    }

    private static void ThrowLastError(string context)
    {
        var errorCode = MicEngineNative.LastErrorCode();
        using var msgPtr = new RustString(MicEngineNative.LastErrorMessage());
        var message = msgPtr.AsString() ?? "Unknown error";

        throw new MicEngineException(errorCode, $"{context}: {message} (code={errorCode})");
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _handle.Dispose();
        }
    }
}
