using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;

namespace MicrophoneManager.WinUI.Views;

/// <summary>
/// Flyout window for microphone list
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        Flyout.RequestClose = Close;

        // Configure borderless, always-on-top window
        var appWindow = AppWindow;
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        // Create context menu presenter (borderless, always on top)
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        appWindow.SetPresenter(presenter);

        // Set size and position
        appWindow.ResizeClient(new Windows.Graphics.SizeInt32(390, 300));
        CenterOnScreen();

        // Close on Escape key - hook up to root content
        if (Content is UIElement root)
        {
            root.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    Close();
                }
            };
        }
    }

    private void CenterOnScreen()
    {
        try
        {
            var displayArea = DisplayArea.Primary;
            var workArea = displayArea.WorkArea;
            var appWindow = AppWindow;

            var x = (workArea.Width - 390) / 2 + workArea.X;
            var y = (workArea.Height - 300) / 2 + workArea.Y;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch
        {
            // Fallback: let Windows position it
        }
    }
}
