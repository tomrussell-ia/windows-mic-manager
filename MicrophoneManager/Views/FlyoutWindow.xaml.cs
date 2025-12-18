using System.Windows;
using System.Windows.Input;

namespace MicrophoneManager.Views;

public partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();

        // Close on Escape key
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
                this.Close();
        };
    }
}
