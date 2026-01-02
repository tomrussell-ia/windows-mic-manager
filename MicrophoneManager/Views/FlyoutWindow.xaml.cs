using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MicrophoneManager.Views;

public partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Prevent the docked window from growing off-screen when many devices exist.
        // The content will scroll instead (handled in MicrophoneFlyout).
        try
        {
            var workArea = SystemParameters.WorkArea;
            MaxHeight = Math.Max(200, workArea.Height - 24);
        }
        catch { }

        // Close on Escape key
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
                this.Close();
        };
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allow moving the docked window around.
        // - Default: drag from non-interactive areas so we don't break Slider/List interactions.
        // - Override: Alt+drag from anywhere.
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        var forceDrag = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (!forceDrag && IsFromInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // DragMove can throw if called at an invalid time; ignore.
        }
    }

    private static bool IsFromInteractiveControl(DependencyObject? source)
    {
        // Walk up the visual tree to see if the click originated inside an interactive control.
        // This prevents clicks meant for e.g. Slider, Buttons, ListBox scrolling, etc. from dragging the window.
        while (source != null)
        {
            switch (source)
            {
                case System.Windows.Controls.Primitives.Thumb:
                case System.Windows.Controls.Primitives.ScrollBar:
                case System.Windows.Controls.Primitives.RangeBase:
                case System.Windows.Controls.Primitives.ButtonBase:
                case System.Windows.Controls.Primitives.TextBoxBase:
                case System.Windows.Controls.PasswordBox:
                case System.Windows.Controls.ComboBox:
                case System.Windows.Controls.Primitives.Selector:
                    return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
