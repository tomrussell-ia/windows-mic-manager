namespace MicrophoneManager.Models;

public class MicrophoneDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? IconPath { get; init; }
    public bool IsDefault { get; set; }
    public bool IsDefaultCommunication { get; set; }
    public bool IsMuted { get; set; }
    public float VolumeLevel { get; set; }

    public bool IsSelected => IsDefault || IsDefaultCommunication;
}
