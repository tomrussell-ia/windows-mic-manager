using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static void LogError(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MicrophoneManager");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "startup_error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    internal static void Trace(string message)
    {
#if DEBUG
        LogError(message);
#endif
    }
    /// <summary>
    /// Dependency injection host
    /// </summary>
    public static IHost Host { get; private set; } = null!;

    /// <summary>
    /// Main UI thread dispatcher queue
    /// </summary>
    public static DispatcherQueue MainDispatcherQueue { get; private set; } = null!;

    // TODO: Remove these static references in favor of DI once full migration is complete
    // Kept temporarily for compatibility with existing code patterns
    public static object? TrayViewModel { get; set; }
    public static object? AudioService { get; set; }
    public static Window? DockedWindow { get; set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        try
        {
#if DEBUG
            LogError("App constructor starting");
#endif

            InitializeComponent();
#if DEBUG
            LogError("InitializeComponent completed");
#endif

            // Build dependency injection container
            Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
#if DEBUG
            LogError("DI container built");
#endif
        }
        catch (Exception ex)
        {
            LogError($"App constructor exception: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Configure dependency injection services
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Register services
        // ComThreadService provides STA thread for COM operations
        services.AddSingleton<MicrophoneManager.WinUI.Services.ComThreadService>();

        // PolicyConfigService requires ComThreadService
        services.AddSingleton<MicrophoneManager.WinUI.Services.PolicyConfigService>();

        // AudioDeviceService requires PolicyConfigService
        services.AddSingleton<MicrophoneManager.WinUI.Services.IAudioDeviceService, MicrophoneManager.WinUI.Services.AudioDeviceService>();

        // Register ViewModels
        services.AddSingleton<MicrophoneManager.WinUI.ViewModels.TrayViewModel>(sp =>
        {
            var audioService = sp.GetRequiredService<MicrophoneManager.WinUI.Services.IAudioDeviceService>();
            // Icon update callback will be set in MainWindow
            return new MicrophoneManager.WinUI.ViewModels.TrayViewModel(audioService, _ => { });
        });

        services.AddTransient<MicrophoneManager.WinUI.ViewModels.MicrophoneListViewModel>();

        // Register views
        services.AddSingleton<MainWindow>();
        services.AddTransient<Views.MicrophoneWindow>();
        services.AddTransient<Views.MicrophoneFlyout>();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
#if DEBUG
            LogError("OnLaunched starting");
#endif
            // Get dispatcher for UI thread access
            MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
#if DEBUG
            LogError("DispatcherQueue obtained");
#endif

            // Initialize services
            AudioService = Host.Services.GetRequiredService<MicrophoneManager.WinUI.Services.IAudioDeviceService>();
            TrayViewModel = Host.Services.GetRequiredService<MicrophoneManager.WinUI.ViewModels.TrayViewModel>();

            // Create and activate main window (will be hidden, hosts tray icon)
#if DEBUG
            LogError("Creating MainWindow");
#endif
            m_window = Host.Services.GetRequiredService<MainWindow>();
#if DEBUG
            LogError("MainWindow created, activating");
#endif
            m_window.Activate();
#if DEBUG
            LogError("MainWindow activated");
#endif
        }
        catch (Exception ex)
        {
            LogError($"OnLaunched exception: {ex}");
            throw;
        }
    }

    private Window? m_window;
}
