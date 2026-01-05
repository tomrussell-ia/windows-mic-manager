using Microsoft.Win32;

namespace MicrophoneManager.WinUI.Services;

/// <summary>
/// Manages application auto-start on Windows startup via Registry.
/// </summary>
public static class StartupService
{
    private const string AppName = "MicrophoneManager";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Gets whether the application is set to start with Windows.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables auto-start on Windows startup.
    /// </summary>
    public static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return;

            if (enabled)
            {
                // Get the path to the current executable
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting startup: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles the auto-start setting.
    /// </summary>
    public static bool ToggleStartup()
    {
        var currentState = IsStartupEnabled();
        SetStartupEnabled(!currentState);
        return !currentState;
    }
}
