using System.Windows.Media;

namespace MicrophoneManager.Models;

public class AudioSession
{
    public uint ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string DisplayName { get; init; }
    public ImageSource? Icon { get; init; }
    public bool IsActive { get; set; }
    public bool IsSystemSound { get; init; }
}
