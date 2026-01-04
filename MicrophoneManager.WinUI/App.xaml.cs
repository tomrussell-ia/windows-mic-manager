using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
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
        InitializeComponent();

        // Build dependency injection container
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    /// <summary>
    /// Configure dependency injection services
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // TODO Stage B: Register services
        // services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        // services.AddSingleton<IIconGeneratorService, IconGeneratorService>();

        // TODO Stage B: Register ViewModels
        // services.AddSingleton<TrayViewModel>();

        // Register views
        services.AddSingleton<MainWindow>();
        services.AddTransient<Views.FlyoutWindow>();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Get dispatcher for UI thread access
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // TODO Stage B: Initialize services
        // AudioService = Host.Services.GetRequiredService<IAudioDeviceService>();
        // TrayViewModel = Host.Services.GetRequiredService<TrayViewModel>();

        // Create and activate main window (will be hidden, hosts tray icon)
        m_window = Host.Services.GetRequiredService<MainWindow>();
        m_window.Activate();
    }

    private Window? m_window;
}
