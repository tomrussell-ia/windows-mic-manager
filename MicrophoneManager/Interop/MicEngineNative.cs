using System.Runtime.InteropServices;

namespace MicrophoneManager.Interop;

/// <summary>
/// P/Invoke declarations for the mic_engine_ffi Rust library.
/// </summary>
internal static partial class MicEngineNative
{
    private const string DllName = "mic_engine_ffi";

    // ========================================================================
    // Lifecycle
    // ========================================================================

    /// <summary>
    /// Create a new mic engine instance.
    /// </summary>
    /// <param name="configJson">JSON configuration string (can be null for defaults)</param>
    /// <returns>Handle to the engine, or IntPtr.Zero on failure</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_create")]
    public static partial IntPtr Create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? configJson);

    /// <summary>
    /// Destroy a mic engine instance.
    /// </summary>
    /// <param name="handle">Handle returned by Create</param>
    [LibraryImport(DllName, EntryPoint = "mic_engine_destroy")]
    public static partial void Destroy(IntPtr handle);

    // ========================================================================
    // Device Operations
    // ========================================================================

    /// <summary>
    /// Get all microphone devices.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <returns>JSON string with device list. Must be freed with FreeString.</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_get_devices")]
    public static partial IntPtr GetDevices(IntPtr handle);

    /// <summary>
    /// Get a specific microphone device by ID.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <param name="deviceId">Device ID (UTF-8)</param>
    /// <returns>JSON string with device info. Must be freed with FreeString.</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_get_device")]
    public static partial IntPtr GetDevice(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId);

    /// <summary>
    /// Set a device as the default for a specific role.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <param name="deviceId">Device ID (UTF-8)</param>
    /// <param name="role">0 = Console, 1 = Multimedia, 2 = Communications</param>
    /// <returns>0 on success, negative error code on failure</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_set_default_device")]
    public static partial int SetDefaultDevice(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId,
        uint role);

    /// <summary>
    /// Set the volume level for a device.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <param name="deviceId">Device ID (UTF-8)</param>
    /// <param name="volume">Volume level (0.0 to 1.0)</param>
    /// <returns>0 on success, negative error code on failure</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_set_volume")]
    public static partial int SetVolume(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId,
        float volume);

    /// <summary>
    /// Toggle the mute state of a device.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <param name="deviceId">Device ID (UTF-8)</param>
    /// <returns>JSON string with result. Must be freed with FreeString.</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_toggle_mute")]
    public static partial IntPtr ToggleMute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId);

    /// <summary>
    /// Set the mute state of a device.
    /// </summary>
    /// <param name="handle">Engine handle</param>
    /// <param name="deviceId">Device ID (UTF-8)</param>
    /// <param name="muted">1 = muted, 0 = unmuted</param>
    /// <returns>0 on success, negative error code on failure</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_set_mute")]
    public static partial int SetMute(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId,
        int muted);

    // ========================================================================
    // Memory Management
    // ========================================================================

    /// <summary>
    /// Free a string allocated by the Rust library.
    /// </summary>
    /// <param name="ptr">Pointer to string returned by other functions</param>
    [LibraryImport(DllName, EntryPoint = "mic_engine_free_string")]
    public static partial void FreeString(IntPtr ptr);

    // ========================================================================
    // Error Handling
    // ========================================================================

    /// <summary>
    /// Get the last error code.
    /// </summary>
    /// <returns>Error code from last failed operation, or 0 if no error</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_last_error_code")]
    public static partial int LastErrorCode();

    /// <summary>
    /// Get the last error message.
    /// </summary>
    /// <returns>Error message string. Must be freed with FreeString.</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_last_error_message")]
    public static partial IntPtr LastErrorMessage();

    // ========================================================================
    // Utility
    // ========================================================================

    /// <summary>
    /// Get the library version.
    /// </summary>
    /// <returns>Version string. Must be freed with FreeString.</returns>
    [LibraryImport(DllName, EntryPoint = "mic_engine_version")]
    public static partial IntPtr Version();
}
