using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using Windows.UI;

namespace MicrophoneManager.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static ElementTheme? _appliedTheme;

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

    internal static void ApplyThemePalette(ElementTheme actualTheme)
    {
        var normalizedTheme = actualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        if (_appliedTheme == normalizedTheme)
        {
            return;
        }

        _appliedTheme = normalizedTheme;

        if (Current?.Resources is not ResourceDictionary resources)
        {
            return;
        }

        if (normalizedTheme == ElementTheme.Dark)
        {
            SetBrushColor(resources, "AccentBrush", Color.FromArgb(255, 0, 120, 212));
            SetBrushColor(resources, "BackgroundBrush", Color.FromArgb(255, 45, 45, 45));
            SetBrushColor(resources, "ForegroundBrush", Color.FromArgb(255, 255, 255, 255));
            SetBrushColor(resources, "SecondaryForegroundBrush", Color.FromArgb(255, 170, 170, 170));
            SetBrushColor(resources, "HoverBrush", Color.FromArgb(255, 61, 61, 61));
            SetBrushColor(resources, "FlyoutBackgroundBrush", Color.FromArgb(255, 45, 45, 45));
            SetBrushColor(resources, "CardBackgroundBrush", Color.FromArgb(255, 61, 61, 61));
            SetBrushColor(resources, "ErrorBannerBrush", Color.FromArgb(255, 196, 43, 28));
            SetBrushColor(resources, "WarningBackgroundBrush", Color.FromArgb(255, 43, 31, 0));
            SetBrushColor(resources, "WarningForegroundBrush", Color.FromArgb(255, 230, 200, 74));
            SetBrushColor(resources, "WarningBorderBrush", Color.FromArgb(255, 230, 200, 74));
            SetBrushColor(resources, "ButtonActiveForegroundBrush", Color.FromArgb(255, 255, 255, 255));
            return;
        }

        SetBrushColor(resources, "AccentBrush", Color.FromArgb(255, 15, 108, 189));
        SetBrushColor(resources, "BackgroundBrush", Color.FromArgb(255, 217, 217, 217));
        SetBrushColor(resources, "ForegroundBrush", Color.FromArgb(255, 31, 31, 31));
        SetBrushColor(resources, "SecondaryForegroundBrush", Color.FromArgb(255, 97, 97, 97));
        SetBrushColor(resources, "HoverBrush", Color.FromArgb(255, 234, 234, 234));
        SetBrushColor(resources, "FlyoutBackgroundBrush", Color.FromArgb(255, 245, 245, 245));
        SetBrushColor(resources, "CardBackgroundBrush", Color.FromArgb(255, 255, 255, 255));
        SetBrushColor(resources, "ErrorBannerBrush", Color.FromArgb(255, 196, 43, 28));
        SetBrushColor(resources, "WarningBackgroundBrush", Color.FromArgb(255, 255, 244, 206));
        SetBrushColor(resources, "WarningForegroundBrush", Color.FromArgb(255, 138, 109, 29));
        SetBrushColor(resources, "WarningBorderBrush", Color.FromArgb(255, 214, 185, 75));
        SetBrushColor(resources, "ButtonActiveForegroundBrush", Color.FromArgb(255, 255, 255, 255));
    }

    private static void SetBrushColor(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
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
            ApplyThemePalette(ElementTheme.Dark);
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
