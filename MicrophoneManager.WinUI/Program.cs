using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MicrophoneManager.WinUI;

public static class Program
{
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "startup_error.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        };

        try
        {
            Log("=== Application starting ===");
            Log($"Current directory: {Environment.CurrentDirectory}");
            Log($"Base directory: {AppContext.BaseDirectory}");
            Log($"OS: {Environment.OSVersion}");
            Log($"64-bit process: {Environment.Is64BitProcess}");

            Log("Initializing WinRT...");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Log("WinRT initialized");

            Log("Starting application...");
            Application.Start((p) =>
            {
                Log("Application.Start callback invoked");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                Log("Creating App instance...");
                var app = new App();
                app.UnhandledException += (sender, args) =>
                {
                    Log($"APP UNHANDLED EXCEPTION: {args.Exception}");
                    args.Handled = true;
                };
                Log("App instance created");
            });
            Log("Application.Start completed - message loop ended");
        }
        catch (Exception ex)
        {
            Log($"FATAL ERROR: {ex}");

            // Also show a message box for visibility
            _ = MessageBox(IntPtr.Zero, $"Application failed to start:\n\n{ex.Message}\n\nSee startup_error.log for details.", "Microphone Manager Error", 0x10);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
