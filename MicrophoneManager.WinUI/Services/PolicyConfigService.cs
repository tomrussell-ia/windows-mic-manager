using System.Runtime.InteropServices;

namespace MicrophoneManager.WinUI.Services;

/// <summary>
/// Provides access to the undocumented IPolicyConfig COM interface
/// for setting the default audio device. Uses a dedicated STA thread
/// to prevent UI thread blocking.
/// </summary>
public class PolicyConfigService : IDisposable
{
    private readonly ComThreadService _comThread;
    private bool _disposed;

    // Device roles
    public enum ERole
    {
        eConsole = 0,         // Games, system sounds, voice commands
        eMultimedia = 1,      // Music, movies
        eCommunications = 2   // Voice chat, VoIP
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // Not used methods - must be here to maintain vtable order
        void Reserved1();
        void Reserved2();
        void Reserved3();
        void Reserved4();
        void Reserved5();
        void Reserved6();
        void Reserved7();
        void Reserved8();
        void Reserved9();
        void Reserved10();

        [PreserveSig]
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }

    public PolicyConfigService(ComThreadService comThread)
    {
        _comThread = comThread ?? throw new ArgumentNullException(nameof(comThread));
    }

    /// <summary>
    /// Sets the specified device as the default for the given role asynchronously.
    /// </summary>
    public async Task SetDefaultDeviceAsync(string deviceId, ERole role, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PolicyConfigService));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _comThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetDefaultDeviceInternal(deviceId, role);
        });
    }

    /// <summary>
    /// Sets the specified device as the default for both Console and Communications roles asynchronously.
    /// Uses a single COM object for both calls to reduce overhead.
    /// </summary>
    public async Task SetDefaultDeviceForAllRolesAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PolicyConfigService));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _comThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetDefaultDeviceForAllRolesInternal(deviceId);
        });
    }

    /// <summary>
    /// Synchronous version for backward compatibility. Blocks the calling thread.
    /// </summary>
    public void SetDefaultDevice(string deviceId, ERole role)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PolicyConfigService));
        }

        SetDefaultDeviceInternal(deviceId, role);
    }

    /// <summary>
    /// Synchronous version for backward compatibility. Blocks the calling thread.
    /// </summary>
    public void SetDefaultDeviceForAllRoles(string deviceId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PolicyConfigService));
        }

        SetDefaultDeviceForAllRolesInternal(deviceId);
    }

    private static void SetDefaultDeviceInternal(string deviceId, ERole role)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            int hr = policyConfig.SetDefaultEndpoint(deviceId, role);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }

    private static void SetDefaultDeviceForAllRolesInternal(string deviceId)
    {
        // Use single COM object for both calls to reduce overhead
        var policyConfig = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            int hr1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
            Marshal.ThrowExceptionForHR(hr1);

            int hr2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            Marshal.ThrowExceptionForHR(hr2);
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
