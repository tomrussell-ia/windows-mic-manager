using System.Runtime.InteropServices;

namespace MicrophoneManager.Core.Services;

/// <summary>
/// Provides access to the undocumented IPolicyConfig COM interface
/// for setting the default audio device.
/// </summary>
public static class PolicyConfigService
{
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

    /// <summary>
    /// Sets the specified device as the default for the given role.
    /// </summary>
    public static void SetDefaultDevice(string deviceId, ERole role)
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

    /// <summary>
    /// Sets the specified device as the default for both Console and Communications roles.
    /// </summary>
    public static void SetDefaultDeviceForAllRoles(string deviceId)
    {
        SetDefaultDevice(deviceId, ERole.eConsole);
        SetDefaultDevice(deviceId, ERole.eCommunications);
    }
}
