using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MicrophoneManager.WinUI.Views;

/// <summary>
/// Static functions used by x:Bind function bindings in XAML (replaces value converters).
/// </summary>
public static class BindingFunctions
{
    public static double ToOpacity(bool visible) => visible ? 1.0 : 0.0;

    public static string MuteLabel(bool isMuted) => isMuted ? "Unmute" : "Mute";

    // Segoe Fluent Icons: MicOff / Microphone
    public static string MuteGlyph(bool isMuted) => isMuted ? "" : "";

    public static string DbText(double db)
    {
        if (double.IsNaN(db)) db = -96.0;
        if (double.IsNegativeInfinity(db)) return "-∞ dB";
        db = Math.Max(-96.0, Math.Min(0.0, db));
        return $"{db:0} dB";
    }

    /// <summary>
    /// OBS-style (sample peak) thresholds: warning ~ -20 dBFS (yellow), error ~ -9 dBFS (red).
    /// </summary>
    public static Brush MeterBrush(double db)
    {
        if (double.IsNaN(db) || double.IsNegativeInfinity(db)) db = -96.0;

        var key = db >= -9.0
            ? "MeterRedBrush"
            : (db >= -20.0 ? "MeterYellowBrush" : "MeterGreenBrush");

        if (Application.Current?.Resources?.TryGetValue(key, out var resource) == true && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }
}
