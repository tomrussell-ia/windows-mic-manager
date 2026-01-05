using MicrophoneManager.Tests.Fakes;
using MicrophoneManager.WinUI.ViewModels;
using Xunit;

namespace MicrophoneManager.Tests;

public class MicrophoneListViewModelTests
{
    [Fact]
    public void RefreshDevicesSynchronizesViewModel()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5,
            FormatTag = "48 kHz 16-bit Mono"
        });
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset")
        {
            InputLevelPercent = 30
        });
        fakeService.DefaultConsoleId = "mic-1";
        fakeService.DefaultCommunicationsId = "mic-2";

        var viewModel = new MicrophoneListViewModel(fakeService);

        Assert.Equal(2, viewModel.Microphones.Count);
        Assert.Equal("mic-1", viewModel.SelectedMicrophone?.Id);
        Assert.True(viewModel.Microphones.First(m => m.Id == "mic-2").IsDefaultCommunication);
        Assert.Equal(50, viewModel.CurrentMicLevelPercent);

        fakeService.RemoveMicrophone("mic-1");
        fakeService.DefaultConsoleId = "mic-2";
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset Updated")
        {
            VolumeScalar = 0.3,
            FormatTag = "44.1 kHz 16-bit Mono",
            InputLevelPercent = 55
        });
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-3", "USB Mic")
        {
            InputLevelPercent = 15
        });

        viewModel.RefreshDevices();

        Assert.Equal(2, viewModel.Microphones.Count);
        Assert.DoesNotContain(viewModel.Microphones, m => m.Id == "mic-1");
        Assert.Equal("Headset Updated", viewModel.Microphones.First(m => m.Id == "mic-2").Name);
        Assert.Equal("mic-2", viewModel.SelectedMicrophone?.Id);
    }

    [Fact]
    public void DefaultVolumeEventsUpdateCurrentState()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.25
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        fakeService.RaiseDefaultVolumeChanged("mic-1", 0.42f, true);

        Assert.Equal(42, viewModel.CurrentMicLevelPercent);
        Assert.True(viewModel.IsMuted);
        Assert.Equal(42, viewModel.Microphones.First(m => m.Id == "mic-1").VolumePercent);
        Assert.True(viewModel.Microphones.First(m => m.Id == "mic-1").IsMuted);
    }

    [Fact]
    public void InputLevelEventsUpdateMeters()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        fakeService.RaiseInputLevelChanged("mic-1", 70, -5);

        Assert.Equal(70, viewModel.CurrentMicInputLevelPercent);
        Assert.Equal(-5, viewModel.CurrentMicInputLevelDbFs);
        Assert.True(viewModel.PeakMicInputLevelPercent >= 70);
    }

    [Fact]
    public void PerDeviceVolumeEventsUpdateNonDefaultEntries()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.25,
            IsMuted = false
        });
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset")
        {
            VolumeScalar = 0.80,
            IsMuted = false
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Simulate an external change to a non-default mic.
        fakeService.RaiseMicrophoneVolumeChanged("mic-2", 0.33f, true);

        var updated = viewModel.Microphones.First(m => m.Id == "mic-2");
        Assert.Equal(33, updated.VolumePercent);
        Assert.True(updated.IsMuted);

        // Default mic UI stays driven by default-specific events.
        Assert.Equal("mic-1", viewModel.SelectedMicrophone?.Id);
    }

    [Fact]
    public void ChangingSliderSendsVolumeToService()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService)
        {
            CurrentMicLevelPercent = 0
        };

        viewModel.CurrentMicLevelPercent = 65;

        var updated = fakeService.GetMicrophones().First(m => m.Id == "mic-1");
        Assert.Equal(0.65f, updated.VolumeLevel, 3);
    }

    [Fact]
    public void ToggleMuteCommandUsesService()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        viewModel.ToggleMuteCommand.Execute(null);

        Assert.Equal(fakeService.IsDefaultMicrophoneMuted(), viewModel.IsMuted);
    }

    #region FR-018, FR-019: Hot-Plug Detection

    [Fact]
    public void DevicesChangedEvent_RefreshesDeviceList_WhenNewDeviceAdded()
    {
        // Arrange - FR-018: System MUST automatically detect when microphones are connected
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.Single(viewModel.Microphones);

        // Act - simulate hot-plug of new USB microphone
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "USB Microphone"));
        fakeService.RaiseDevicesChanged();

        // Assert
        Assert.Equal(2, viewModel.Microphones.Count);
        Assert.Contains(viewModel.Microphones, m => m.Id == "mic-2");
    }

    [Fact]
    public void DevicesChangedEvent_RefreshesDeviceList_WhenDeviceRemoved()
    {
        // Arrange - FR-018: System MUST automatically detect when microphones are disconnected
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "USB Microphone"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.Equal(2, viewModel.Microphones.Count);

        // Act - simulate USB microphone disconnection
        fakeService.RemoveMicrophone("mic-2");
        fakeService.RaiseDevicesChanged();

        // Assert
        Assert.Single(viewModel.Microphones);
        Assert.DoesNotContain(viewModel.Microphones, m => m.Id == "mic-2");
    }

    [Fact]
    public void DefaultDeviceChangedEvent_UpdatesSelectedMicrophone()
    {
        // Arrange - FR-019: System MUST automatically detect when the default device changes
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.Equal("mic-1", viewModel.SelectedMicrophone?.Id);

        // Act - simulate external default device change (e.g., from Windows Settings)
        fakeService.DefaultConsoleId = "mic-2";
        fakeService.RaiseDefaultDeviceChanged();

        // Assert
        Assert.Equal("mic-2", viewModel.SelectedMicrophone?.Id);
    }

    [Fact]
    public void DefaultDeviceDisconnected_SelectsNewDefault()
    {
        // Arrange - Edge case: default mic disconnected
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Act - default device is disconnected, system selects new default
        fakeService.RemoveMicrophone("mic-1");
        fakeService.DefaultConsoleId = "mic-2";
        fakeService.RaiseDevicesChanged();
        fakeService.RaiseDefaultDeviceChanged();

        // Assert
        Assert.Single(viewModel.Microphones);
        Assert.Equal("mic-2", viewModel.SelectedMicrophone?.Id);
    }

    #endregion

    #region Edge Cases: Empty Device List

    [Fact]
    public void EmptyDeviceList_HasMicrophonesIsFalse()
    {
        // Arrange - Edge case: No microphones connected
        var fakeService = new FakeAudioDeviceService();

        // Act
        var viewModel = new MicrophoneListViewModel(fakeService);

        // Assert
        Assert.Empty(viewModel.Microphones);
        Assert.False(viewModel.HasMicrophones);
        Assert.Null(viewModel.SelectedMicrophone);
    }

    [Fact]
    public void AllDevicesRemoved_HasMicrophonesBecomeFalse()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.True(viewModel.HasMicrophones);

        // Act - all devices removed
        fakeService.RemoveMicrophone("mic-1");
        fakeService.DefaultConsoleId = null;
        fakeService.RaiseDevicesChanged();

        // Assert
        Assert.Empty(viewModel.Microphones);
        Assert.False(viewModel.HasMicrophones);
    }

    #endregion

    #region Edge Cases: Volume Boundaries

    [Fact]
    public void VolumeSlider_ClampsToZeroPercent()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Act - set negative volume (should clamp to 0)
        viewModel.CurrentMicLevelPercent = -10;

        // Assert
        var mic = fakeService.GetMicrophones().First();
        Assert.Equal(0.0f, mic.VolumeLevel, 3);
    }

    [Fact]
    public void VolumeSlider_ClampsToHundredPercent()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Act - set volume over 100 (should clamp to 100)
        viewModel.CurrentMicLevelPercent = 150;

        // Assert
        var mic = fakeService.GetMicrophones().First();
        Assert.Equal(1.0f, mic.VolumeLevel, 3);
    }

    #endregion

    #region FR-020, FR-021: External Change Sync

    [Fact]
    public void ExternalVolumeChange_UpdatesSliderWithoutFeedbackLoop()
    {
        // Arrange - FR-020: System MUST handle volume changes made by external applications
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.Equal(50, viewModel.CurrentMicLevelPercent);

        // Act - simulate Windows Settings changing volume
        fakeService.RaiseDefaultVolumeChanged("mic-1", 0.75f, false);

        // Assert
        Assert.Equal(75, viewModel.CurrentMicLevelPercent);
    }

    [Fact]
    public void ExternalMuteChange_UpdatesMuteState()
    {
        // Arrange - FR-021: System MUST detect and reflect mute state changes made by external applications
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            IsMuted = false
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.False(viewModel.IsMuted);

        // Act - simulate external mute
        fakeService.RaiseDefaultVolumeChanged("mic-1", 0.5f, true);

        // Assert
        Assert.True(viewModel.IsMuted);
    }

    #endregion

    #region Dispose Pattern

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Act & Assert - should not throw
        var exception = Record.Exception(() => viewModel.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void AfterDispose_EventsDoNotCrash()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        viewModel.Dispose();

        // Act - events raised after dispose should not crash
        var exception = Record.Exception(() =>
        {
            fakeService.RaiseDevicesChanged();
            fakeService.RaiseDefaultDeviceChanged();
            fakeService.RaiseDefaultVolumeChanged("mic-1", 0.5f, false);
        });

        // Assert
        Assert.Null(exception);
    }

    #endregion

    #region FR-023: Audio Format Change Sync

    [Fact]
    public void FormatChangedEvent_UpdatesDeviceFormatTag()
    {
        // Arrange - FR-023: System MUST detect and reflect audio format changes
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            FormatTag = "48 kHz 24-bit Stereo"
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.Equal("48 kHz 24-bit Stereo", viewModel.Microphones.First().FormatTag);

        // Act - simulate external format change
        fakeService.RaiseFormatChanged("mic-1", "96 kHz 32-bit Stereo");

        // Assert
        Assert.Equal("96 kHz 32-bit Stereo", viewModel.Microphones.First().FormatTag);
    }

    [Fact]
    public void FormatChangedEvent_OnlyUpdatesMatchingDevice()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            FormatTag = "48 kHz 24-bit Stereo"
        });
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset")
        {
            FormatTag = "44.1 kHz 16-bit Mono"
        });
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);

        // Act - change format for mic-2 only
        fakeService.RaiseFormatChanged("mic-2", "48 kHz 16-bit Stereo");

        // Assert - only mic-2 should change
        Assert.Equal("48 kHz 24-bit Stereo", viewModel.Microphones.First(m => m.Id == "mic-1").FormatTag);
        Assert.Equal("48 kHz 16-bit Stereo", viewModel.Microphones.First(m => m.Id == "mic-2").FormatTag);
    }

    #endregion

    #region Error Message Infrastructure

    [Fact]
    public void ShowError_SetsErrorMessageAndHasError()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);

        // Act
        viewModel.ShowError("Test error message");

        // Assert
        Assert.True(viewModel.HasError);
        Assert.Equal("Test error message", viewModel.ErrorMessage);
    }

    [Fact]
    public void DismissError_ClearsErrorMessage()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var viewModel = new MicrophoneListViewModel(fakeService);
        viewModel.ShowError("Test error");
        Assert.True(viewModel.HasError);

        // Act
        viewModel.DismissError();

        // Assert
        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
    }

    #endregion
}
