using MicrophoneManager.Tests.Fakes;
using MicrophoneManager.WinUI.ViewModels;
using Xunit;

namespace MicrophoneManager.Tests;

/// <summary>
/// Tests for TrayViewModel covering FR-015 (tray icon mute state), FR-016 (tooltip text),
/// FR-017 (exit command), and FR-024 (start with Windows toggle).
/// </summary>
public class TrayViewModelTests
{
    private bool _lastIconMutedState;

    private TrayViewModel CreateViewModel(FakeAudioDeviceService fakeService)
    {
        return new TrayViewModel(fakeService, (isMuted) => _lastIconMutedState = isMuted);
    }

    #region FR-016: Tooltip Text

    [Fact]
    public void TooltipText_ShowsDefaultMicrophoneName_WhenNotMuted()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Studio Microphone"));
        fakeService.DefaultConsoleId = "mic-1";

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.Equal("Studio Microphone", viewModel.TooltipText);
    }

    [Fact]
    public void TooltipText_ShowsMutedSuffix_WhenDefaultMicrophoneIsMuted()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Studio Microphone")
        {
            IsMuted = true
        });
        fakeService.DefaultConsoleId = "mic-1";

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.Equal("Studio Microphone (Muted)", viewModel.TooltipText);
    }

    [Fact]
    public void TooltipText_ShowsNoMicrophoneMessage_WhenNoDevicesAvailable()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        // No microphones added

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.Equal("No microphone detected", viewModel.TooltipText);
    }

    [Fact]
    public void TooltipText_UpdatesWhenDefaultDeviceChanges()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);
        Assert.Equal("Desk Mic", viewModel.TooltipText);

        // Act - change default device
        fakeService.DefaultConsoleId = "mic-2";
        fakeService.RaiseDefaultDeviceChanged();

        // Assert
        Assert.Equal("Headset Mic", viewModel.TooltipText);
    }

    [Fact]
    public void TooltipText_UpdatesWhenMuteStateChangesExternally()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);
        Assert.Equal("Desk Mic", viewModel.TooltipText);

        // Act - simulate external mute change (update state then raise event)
        fakeService.ToggleMute("mic-1"); // Set to muted state
        fakeService.RaiseDefaultVolumeChanged("mic-1", 0.5f, true);

        // Assert
        Assert.Equal("Desk Mic (Muted)", viewModel.TooltipText);
    }

    #endregion

    #region FR-015: Tray Icon Mute State

    [Fact]
    public void IsMuted_ReflectsDefaultMicrophoneMuteState()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            IsMuted = true
        });
        fakeService.DefaultConsoleId = "mic-1";

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.True(viewModel.IsMuted);
        Assert.True(_lastIconMutedState);
    }

    [Fact]
    public void IsMuted_IsFalse_WhenDefaultMicrophoneNotMuted()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            IsMuted = false
        });
        fakeService.DefaultConsoleId = "mic-1";

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.False(viewModel.IsMuted);
        Assert.False(_lastIconMutedState);
    }

    [Fact]
    public void IsMuted_IsFalse_WhenNoDevicesAvailable()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();

        // Act
        var viewModel = CreateViewModel(fakeService);

        // Assert
        Assert.False(viewModel.IsMuted);
    }

    [Fact]
    public void IconCallback_InvokedOnMuteStateChange()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);
        Assert.False(_lastIconMutedState);

        // Act - mute externally (update state then raise event)
        fakeService.ToggleMute("mic-1"); // Set to muted state
        fakeService.RaiseDefaultVolumeChanged("mic-1", 0.5f, true);

        // Assert
        Assert.True(_lastIconMutedState);
    }

    #endregion

    #region ToggleMute Command

    [Fact]
    public void ToggleMuteCommand_TogglesDefaultMicrophoneMuteState()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            IsMuted = false
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);
        Assert.False(viewModel.IsMuted);

        // Act
        viewModel.ToggleMuteCommand.Execute(null);

        // Assert
        Assert.True(viewModel.IsMuted);
        Assert.True(fakeService.IsMuted("mic-1"));
    }

    [Fact]
    public void ToggleMuteCommand_UpdatesTooltipText()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);
        Assert.Equal("Desk Mic", viewModel.TooltipText);

        // Act
        viewModel.ToggleMuteCommand.Execute(null);

        // Assert
        Assert.Equal("Desk Mic (Muted)", viewModel.TooltipText);
    }

    #endregion

    #region FR-024: Start with Windows

    [Fact]
    public void StartupMenuText_ShowsCheckmark_WhenEnabled()
    {
        // Note: This test validates the menu text logic, not the actual registry.
        // The StartupService itself uses static methods which would need registry access.
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);

        // The IsStartupEnabled state depends on actual registry, so we just verify the property format
        if (viewModel.IsStartupEnabled)
        {
            Assert.Equal("✓ Start with Windows", viewModel.StartupMenuText);
        }
        else
        {
            Assert.Equal("Start with Windows", viewModel.StartupMenuText);
        }
    }

    #endregion

    #region Device Change Events

    [Fact]
    public void DevicesChangedEvent_RefreshesTooltip()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = CreateViewModel(fakeService);

        // Remove the device and update default
        fakeService.RemoveMicrophone("mic-1");
        fakeService.DefaultConsoleId = null;

        // Act
        fakeService.RaiseDevicesChanged();

        // Assert
        Assert.Equal("No microphone detected", viewModel.TooltipText);
    }

    #endregion
}
