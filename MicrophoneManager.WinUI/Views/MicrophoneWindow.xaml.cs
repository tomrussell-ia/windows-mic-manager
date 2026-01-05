using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace MicrophoneManager.WinUI.Views;

/// <summary>
/// Unified window for microphone list - supports both flyout (tray popup) and docked (persistent) modes.
/// </summary>
public sealed partial class MicrophoneWindow : Window
{
    private const int MinClientWidth = 560;
    private const int DefaultClientHeight = 540;
    private const int ScreenMarginPx = 12;

    private readonly bool _isDocked;

    public MicrophoneWindow(bool isDocked = false)
    {
        _isDocked = isDocked;

        InitializeComponent();

        Flyout.IsDockedMode = isDocked;

        if (!isDocked)
        {
            Flyout.RequestClose = Close;
        }

        // Resize whenever flyout content changes (cards load / devices change).
        // In flyout mode we also reposition; in docked mode we only resize.
        Flyout.ViewportHeightChanged += (_, _) =>
        {
            try { DispatcherQueue.TryEnqueue(() => ResizeAndPosition()); } catch { }
        };

        ConfigureWindow();

        Activated += MicrophoneWindow_Activated;
        Closed += MicrophoneWindow_Closed;

        RootBorder.Loaded += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() => ResizeAndPosition());
        };

        // Close on Escape key (flyout mode only)
        if (!isDocked && Content is UIElement root)
        {
            root.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    Close();
                }
            };
        }

        // Start metering
        try
        {
            Flyout.ViewModel.SetMeteringEnabled(true);
        }
        catch { }
    }

    private void ConfigureWindow()
    {
        var appWindow = AppWindow;

        if (_isDocked)
        {
            // Docked mode: normal resizable/movable window with title bar
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;

            var presenter = OverlappedPresenter.Create();
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
            presenter.IsAlwaysOnTop = false;
            appWindow.SetPresenter(presenter);
        }
        else
        {
            // Flyout mode: borderless, always-on-top popup
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

            // NOTE: CreateForContextMenu can ignore later ResizeClient calls on some systems.
            // Use a normal overlapped presenter configured like a popup instead.
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            appWindow.SetPresenter(presenter);
        }

        // Initial size
        appWindow.ResizeClient(new Windows.Graphics.SizeInt32(MinClientWidth, DefaultClientHeight));
    }

    private void MicrophoneWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (!_isDocked)
            {
                // Flyout mode: close when focus is lost
                try { Flyout.ViewModel.SetMeteringEnabled(false); } catch { }
                try { Close(); } catch { }
            }
            return;
        }

        try { Flyout.ViewModel.SetMeteringEnabled(true); } catch { }

        // Always resize based on content; only reposition for flyout mode
        ResizeAndPosition();
    }

    private void MicrophoneWindow_Closed(object sender, WindowEventArgs args)
    {
        try { Flyout.ViewModel.Dispose(); } catch { }

        if (_isDocked)
        {
            App.DockedWindow = null;
        }
    }

    private void ResizeAndPosition()
    {
        try
        {
            var appWindow = AppWindow;
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            // WinUI layout uses DIPs; AppWindow sizing APIs use Win32 client coordinates (pixels).
            // Convert DIP measurements to pixels using the window's rasterization scale.
            var scale = RootBorder?.XamlRoot?.RasterizationScale ?? 1.0;

            var beforeClient = appWindow.ClientSize;
            var beforeSize = appWindow.Size;

            // Get desired height based on measured card height so we can at least fit 2 cards.
            var contentSize = Flyout.GetDesiredContentSize();
            var desiredContentHeight = Flyout.GetDesiredContentHeight();
            if (desiredContentHeight > 0)
            {
                contentSize = new Windows.Foundation.Size(contentSize.Width, desiredContentHeight);
            }

            // Add RootBorder padding (16 on each side) and border thickness
            const double borderPadding = 32 + 2; // 16*2 padding + 1*2 border
            var desiredWidthDip = contentSize.Width + borderPadding;
            var desiredHeightDip = contentSize.Height + borderPadding;

            // ResizeClient expects a Win32 client size; the non-client area is calculated.
            var maxClientWidth = Math.Max(0, workArea.Width - (ScreenMarginPx * 2));
            var maxClientHeight = Math.Max(0, workArea.Height - (ScreenMarginPx * 2));

            var targetClientWidth = (int)Math.Ceiling(desiredWidthDip * scale);
            var targetClientHeight = (int)Math.Ceiling(desiredHeightDip * scale);

            var minClientWidth = (int)Math.Ceiling(MinClientWidth * scale);

            targetClientWidth = Math.Clamp(targetClientWidth, minClientWidth, maxClientWidth);
            targetClientHeight = Math.Clamp(targetClientHeight, 200, maxClientHeight);

            // Prefer ResizeClient so the window computes non-client area correctly.
            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(targetClientWidth, targetClientHeight));

            var afterClient = appWindow.ClientSize;
            var afterSize = appWindow.Size;

            // Fallback: if ResizeClient isn't honored, translate requested client -> window size using
            // the current frame insets.
            if (Math.Abs(afterClient.Width - targetClientWidth) > 1 || Math.Abs(afterClient.Height - targetClientHeight) > 1)
            {
                var frameWidth = Math.Max(0, beforeSize.Width - beforeClient.Width);
                var frameHeight = Math.Max(0, beforeSize.Height - beforeClient.Height);
                var targetWindowWidth = targetClientWidth + frameWidth;
                var targetWindowHeight = targetClientHeight + frameHeight;

                try { appWindow.Resize(new Windows.Graphics.SizeInt32(targetWindowWidth, targetWindowHeight)); } catch { }
                afterClient = appWindow.ClientSize;
                afterSize = appWindow.Size;

                // One more try with ResizeClient after changing overall window size.
                if (Math.Abs(afterClient.Width - targetClientWidth) > 1 || Math.Abs(afterClient.Height - targetClientHeight) > 1)
                {
                    try { appWindow.ResizeClient(new Windows.Graphics.SizeInt32(targetClientWidth, targetClientHeight)); } catch { }
                    afterClient = appWindow.ClientSize;
                    afterSize = appWindow.Size;
                }
            }

            App.Trace(
                $"ResizeAndPosition mode={(_isDocked ? "docked" : "flyout")} " +
                $"mics={Flyout.ViewModel.Microphones.Count} " +
                $"cardH={(Flyout.MeasuredCardOuterHeight.HasValue ? Flyout.MeasuredCardOuterHeight.Value.ToString("0.0") : "null")} " +
                $"content=({contentSize.Width:0.0}x{contentSize.Height:0.0}) " +
                $"scale={scale:0.###} " +
                $"targetDip=({desiredWidthDip:0.0}x{desiredHeightDip:0.0}) " +
                $"targetClient=({targetClientWidth}x{targetClientHeight}) " +
                $"client {beforeClient.Width}x{beforeClient.Height}->{afterClient.Width}x{afterClient.Height} " +
                $"size {beforeSize.Width}x{beforeSize.Height}->{afterSize.Width}x{afterSize.Height} " +
                $"workArea=({workArea.Width}x{workArea.Height})");

            if (!_isDocked)
            {
                // Flyout mode: anchor to bottom-right of work area
                var x = workArea.X + workArea.Width - afterSize.Width - ScreenMarginPx;
                var y = workArea.Y + workArea.Height - afterSize.Height - ScreenMarginPx;
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }
        }
        catch
        {
            App.Trace("ResizeAndPosition threw; sizing skipped");
        }
    }
}
