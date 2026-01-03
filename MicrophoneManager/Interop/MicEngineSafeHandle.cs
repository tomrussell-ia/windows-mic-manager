using System.Runtime.InteropServices;

namespace MicrophoneManager.Interop;

/// <summary>
/// SafeHandle wrapper for the native mic engine handle.
/// Ensures proper cleanup even if exceptions occur.
/// </summary>
internal sealed class MicEngineSafeHandle : SafeHandle
{
    public MicEngineSafeHandle() : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public MicEngineSafeHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (handle != IntPtr.Zero)
        {
            MicEngineNative.Destroy(handle);
        }
        return true;
    }
}

/// <summary>
/// Helper for managing Rust-allocated strings.
/// </summary>
internal readonly struct RustString : IDisposable
{
    private readonly IntPtr _ptr;

    public RustString(IntPtr ptr)
    {
        _ptr = ptr;
    }

    public bool IsNull => _ptr == IntPtr.Zero;

    public string? AsString()
    {
        if (_ptr == IntPtr.Zero)
            return null;

        return Marshal.PtrToStringUTF8(_ptr);
    }

    public void Dispose()
    {
        if (_ptr != IntPtr.Zero)
        {
            MicEngineNative.FreeString(_ptr);
        }
    }
}
