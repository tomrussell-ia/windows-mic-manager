namespace MicrophoneManager.WinUI.Models;

public enum FidelityTier
{
    Studio,   // ≥88.2 kHz, or ≥44.1 kHz + 24-bit
    High,     // ≥44.1 kHz, 16-bit
    Standard, // ≥22 kHz, 16-bit
    Reduced   // <22 kHz or <16-bit — voice-optimized format
}
