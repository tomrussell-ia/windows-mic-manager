using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MicrophoneManager.Services;

/// <summary>
/// Generates tray icons using Windows Segoe MDL2 Assets font for native look.
/// </summary>
public static class IconGenerator
{
    private const int IconSize = 64;  // Larger for better visibility
    private const int FontSize = 48;  // Larger glyph to fill the icon
    private const string FontName = "Segoe MDL2 Assets";

    // Segoe MDL2 Assets glyph codes
    private const string MicrophoneGlyph = "\uE720";      // Microphone
    private const string MicrophoneMutedGlyph = "\uE74F"; // Microphone with slash

    public static Icon CreateMicrophoneIcon(bool isMuted)
    {
        using var bitmap = new Bitmap(IconSize, IconSize);
        using var graphics = Graphics.FromImage(bitmap);

        // High quality rendering
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        // Choose glyph and color based on mute state
        string glyph = isMuted ? MicrophoneMutedGlyph : MicrophoneGlyph;
        var color = isMuted ? Color.FromArgb(180, 180, 180) : Color.White;

        using var font = new Font(FontName, FontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);

        // Use StringFormat for precise centering
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var rect = new RectangleF(0, 0, IconSize, IconSize);
        graphics.DrawString(glyph, font, brush, rect, format);

        // Convert bitmap to icon
        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
