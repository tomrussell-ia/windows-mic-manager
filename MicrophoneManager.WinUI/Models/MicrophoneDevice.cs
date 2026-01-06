namespace MicrophoneManager.WinUI.Models;

/// <summary>
/// Snapshot of a microphone device returned from the audio service.
/// ViewModels should wrap this to add UI behavior and commands.
/// </summary>
public class MicrophoneDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? IconPath { get; init; }
    public bool IsDefault { get; init; }
    public bool IsDefaultCommunication { get; init; }
    public bool IsMuted { get; init; }
    public float VolumeLevel { get; init; }
    public string FormatTag { get; init; } = "";
    public double InputLevelPercent { get; init; }

    public bool IsSelected => IsDefault || IsDefaultCommunication;
}
