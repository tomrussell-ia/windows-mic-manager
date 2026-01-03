using System.Text.Json.Serialization;

namespace MicrophoneManager.Interop;

/// <summary>
/// Device role for setting defaults.
/// Maps to Windows ERole enum.
/// </summary>
public enum MicDeviceRole : uint
{
    /// <summary>Used by games, system sounds, most general applications</summary>
    Console = 0,

    /// <summary>Used by music players, video players</summary>
    Multimedia = 1,

    /// <summary>Used by Teams, Zoom, Discord, and other VoIP applications</summary>
    Communications = 2
}

/// <summary>
/// Audio format information from Rust.
/// </summary>
public sealed class MicAudioFormatDto
{
    [JsonPropertyName("sample_rate")]
    public uint SampleRate { get; set; }

    [JsonPropertyName("bit_depth")]
    public ushort BitDepth { get; set; }

    [JsonPropertyName("channels")]
    public ushort Channels { get; set; }

    public override string ToString()
    {
        var rateKhz = SampleRate / 1000.0;
        return rateKhz == Math.Floor(rateKhz)
            ? $"{(int)rateKhz}kHz/{BitDepth}-bit"
            : $"{rateKhz:F1}kHz/{BitDepth}-bit";
    }
}

/// <summary>
/// Microphone device information from Rust.
/// </summary>
public sealed class MicDeviceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("is_default_communication")]
    public bool IsDefaultCommunication { get; set; }

    [JsonPropertyName("is_muted")]
    public bool IsMuted { get; set; }

    [JsonPropertyName("volume_level")]
    public float VolumeLevel { get; set; }

    [JsonPropertyName("audio_format")]
    public MicAudioFormatDto? AudioFormat { get; set; }
}

/// <summary>
/// Response containing a list of devices.
/// </summary>
public sealed class DeviceListResponseDto
{
    [JsonPropertyName("devices")]
    public List<MicDeviceDto> Devices { get; set; } = new();
}

/// <summary>
/// Response containing a single device.
/// </summary>
public sealed class DeviceResponseDto
{
    [JsonPropertyName("device")]
    public MicDeviceDto? Device { get; set; }
}

/// <summary>
/// Response from toggle mute operation.
/// </summary>
public sealed class OperationResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("is_muted")]
    public bool? IsMuted { get; set; }
}
