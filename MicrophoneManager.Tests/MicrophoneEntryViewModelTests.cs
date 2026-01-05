using MicrophoneManager.WinUI.Models;
using MicrophoneManager.Tests.Fakes;
using MicrophoneManager.WinUI.ViewModels;
using Xunit;

namespace MicrophoneManager.Tests;

public class MicrophoneEntryViewModelTests
{
    [Fact]
    public void UpdateFromTracksMetersAndPeaks()
    {
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Desk Mic",
            IsDefault = true,
            IsDefaultCommunication = false,
            IsMuted = false,
            VolumeLevel = 0.4f,
            FormatTag = "48 kHz 24-bit Mono",
            InputLevelPercent = 25
        };

        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        Assert.Equal("Desk Mic", viewModel.Name);
        Assert.InRange(viewModel.InputLevelPercent, 25d - 1e-6, 25d + 1e-6);
        Assert.InRange(viewModel.PeakLevelPercent, 25d - 1e-6, 25d + 1e-6);

        viewModel.UpdateMeter(80);
        Assert.InRange(viewModel.InputLevelPercent, 80d - 1e-6, 80d + 1e-6);
        Assert.InRange(viewModel.PeakLevelPercent, 80d - 1e-6, 80d + 1e-6);

        viewModel.UpdateMeter(20);

        // Simulate time passing so the exponential release can lower the displayed meter.
        System.Threading.Thread.Sleep(350);
        viewModel.UpdateMeter(20);

        // Peak hold is ~5s; decay should start after that.
        viewModel.TickPeak(DateTime.UtcNow.AddSeconds(6));

        // Peak should decay toward the current meter after hold expires.
        Assert.InRange(viewModel.InputLevelPercent, 0, 80);
        Assert.InRange(viewModel.PeakLevelPercent, viewModel.InputLevelPercent - 1e-3, 80d + 1e-3);
    }

    [Fact]
    public void VolumeChangeClampsAndWritesToService()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.DefaultConsoleId = "mic-1";

        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        viewModel.VolumePercent = 150;

        var updated = fakeService.GetMicrophones().Single(m => m.Id == "mic-1");
        Assert.Equal(1.0f, updated.VolumeLevel);
    }

    [Fact]
    public void CommandsUpdateDefaultRolesAndMute()
    {
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));

        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        viewModel.SetDefaultCommand.Execute(null);
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);

        viewModel.SetDefaultCommunicationCommand.Execute(null);
        Assert.Equal("mic-1", fakeService.DefaultCommunicationsId);

        fakeService.DefaultConsoleId = null;
        fakeService.DefaultCommunicationsId = null;
        viewModel.SetBothCommand.Execute(null);
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);
        Assert.Equal("mic-1", fakeService.DefaultCommunicationsId);

        var initialMute = fakeService.IsMuted("mic-1");
        viewModel.ToggleMuteCommand.Execute(null);
        Assert.NotEqual(initialMute, fakeService.IsMuted("mic-1"));
        Assert.Equal(fakeService.IsMuted("mic-1"), viewModel.IsMuted);
    }

    #region FR-007: Audio Format Display

    [Fact]
    public void FormatTag_DisplaysCorrectFormat()
    {
        // Arrange - FR-007: System MUST display the audio format tag for EACH microphone device
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Studio Mic",
            FormatTag = "48 kHz 24-bit Stereo"
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert
        Assert.Equal("48 kHz 24-bit Stereo", viewModel.FormatTag);
    }

    [Fact]
    public void FormatTag_DisplaysVariousSampleRates()
    {
        var fakeService = new FakeAudioDeviceService();
        
        // Test common sample rates
        var formats = new[] { "44.1 kHz 16-bit Mono", "48 kHz 16-bit Stereo", "96 kHz 24-bit Stereo" };
        
        foreach (var format in formats)
        {
            var device = new MicrophoneDevice
            {
                Id = "mic-1",
                Name = "Test Mic",
                FormatTag = format
            };

            var viewModel = new MicrophoneEntryViewModel(device, fakeService);
            Assert.Equal(format, viewModel.FormatTag);
        }
    }

    [Fact]
    public void FormatTag_HandlesNullGracefully()
    {
        // Arrange - Edge case: format information unavailable
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Unknown Mic",
            FormatTag = null
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert - should not throw, FormatTag can be null
        Assert.Null(viewModel.FormatTag);
    }

    [Fact]
    public void FormatTag_HandlesEmptyStringGracefully()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Unknown Mic",
            FormatTag = ""
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert
        Assert.Equal("", viewModel.FormatTag);
    }

    #endregion

    #region Edge Cases: Volume Boundaries

    [Fact]
    public void VolumePercent_ClampsNegativeToZero()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act - set negative value
        viewModel.VolumePercent = -50;

        // Assert - should clamp to 0
        var updated = fakeService.GetMicrophones().Single();
        Assert.Equal(0.0f, updated.VolumeLevel, 3);
    }

    [Fact]
    public void VolumePercent_ClampsOver100ToMax()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act - set over 100
        viewModel.VolumePercent = 200;

        // Assert - should clamp to 1.0
        var updated = fakeService.GetMicrophones().Single();
        Assert.Equal(1.0f, updated.VolumeLevel, 3);
    }

    [Fact]
    public void VolumePercent_AcceptsZero()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5
        });
        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act
        viewModel.VolumePercent = 0;

        // Assert
        var updated = fakeService.GetMicrophones().Single();
        Assert.Equal(0.0f, updated.VolumeLevel, 3);
    }

    [Fact]
    public void VolumePercent_AcceptsHundred()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic")
        {
            VolumeScalar = 0.5
        });
        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act
        viewModel.VolumePercent = 100;

        // Assert
        var updated = fakeService.GetMicrophones().Single();
        Assert.Equal(1.0f, updated.VolumeLevel, 3);
    }

    #endregion

    #region FR-009 to FR-014: Default Device Roles

    [Fact]
    public void IsDefault_ReflectsConsoleRole()
    {
        // Arrange - FR-009: System MUST distinguish between Default Device and Default Communication Device
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Desk Mic",
            IsDefault = true,
            IsDefaultCommunication = false
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert
        Assert.True(viewModel.IsDefault);
        Assert.False(viewModel.IsDefaultCommunication);
    }

    [Fact]
    public void IsDefaultCommunication_ReflectsCommunicationRole()
    {
        // Arrange - FR-009
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "Headset Mic",
            IsDefault = false,
            IsDefaultCommunication = true
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert
        Assert.False(viewModel.IsDefault);
        Assert.True(viewModel.IsDefaultCommunication);
    }

    [Fact]
    public void DeviceCanBeBothDefaultAndCommunication()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        var device = new MicrophoneDevice
        {
            Id = "mic-1",
            Name = "All-Purpose Mic",
            IsDefault = true,
            IsDefaultCommunication = true
        };

        // Act
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Assert
        Assert.True(viewModel.IsDefault);
        Assert.True(viewModel.IsDefaultCommunication);
    }

    [Fact]
    public void SetDefaultCommand_SetsConsoleRole()
    {
        // Arrange - FR-010: System MUST allow users to set a microphone as Default Device independently
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset"));
        fakeService.DefaultCommunicationsId = "mic-2"; // Communication set to different device
        
        var device = fakeService.GetMicrophones().First(m => m.Id == "mic-1");
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act
        viewModel.SetDefaultCommand.Execute(null);

        // Assert - Console role changed, Communications unchanged
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);
        Assert.Equal("mic-2", fakeService.DefaultCommunicationsId);
    }

    [Fact]
    public void SetDefaultCommunicationCommand_SetsCommunicationRole()
    {
        // Arrange - FR-011: System MUST allow users to set a microphone as Default Communication Device independently
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-2", "Headset"));
        fakeService.DefaultConsoleId = "mic-1"; // Default set to different device
        
        var device = fakeService.GetMicrophones().First(m => m.Id == "mic-2");
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act
        viewModel.SetDefaultCommunicationCommand.Execute(null);

        // Assert - Communications role changed, Console unchanged
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);
        Assert.Equal("mic-2", fakeService.DefaultCommunicationsId);
    }

    [Fact]
    public void SetBothCommand_SetsBothRoles()
    {
        // Arrange - FR-012: System MUST provide an option to set a microphone as both
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "All-Purpose Mic"));
        
        var device = fakeService.GetMicrophones().Single();
        var viewModel = new MicrophoneEntryViewModel(device, fakeService);

        // Act
        viewModel.SetBothCommand.Execute(null);

        // Assert - Both roles set to same device
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);
        Assert.Equal("mic-1", fakeService.DefaultCommunicationsId);
    }

    #endregion

    #region Error Callback for IPolicyConfig Failures

    [Fact]
    public void SetDefaultCommand_CallsErrorCallback_WhenFails()
    {
        // Arrange - Edge case: IPolicyConfig failure surfaced to UI
        var fakeService = new FakeAudioDeviceService();
        // Device not in service, so SetMicrophoneForRole will return false
        
        var device = new MicrophoneDevice
        {
            Id = "non-existent-mic",
            Name = "Ghost Mic"
        };

        string? capturedError = null;
        var viewModel = new MicrophoneEntryViewModel(device, fakeService, error => capturedError = error);

        // Act
        viewModel.SetDefaultCommand.Execute(null);

        // Assert
        Assert.Equal("Failed to set default device", capturedError);
    }

    [Fact]
    public void SetDefaultCommunicationCommand_CallsErrorCallback_WhenFails()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        
        var device = new MicrophoneDevice
        {
            Id = "non-existent-mic",
            Name = "Ghost Mic"
        };

        string? capturedError = null;
        var viewModel = new MicrophoneEntryViewModel(device, fakeService, error => capturedError = error);

        // Act
        viewModel.SetDefaultCommunicationCommand.Execute(null);

        // Assert
        Assert.Equal("Failed to set communication device", capturedError);
    }

    [Fact]
    public void SetBothCommand_CallsErrorCallback_WhenFails()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        
        var device = new MicrophoneDevice
        {
            Id = "non-existent-mic",
            Name = "Ghost Mic"
        };

        string? capturedError = null;
        var viewModel = new MicrophoneEntryViewModel(device, fakeService, error => capturedError = error);

        // Act
        viewModel.SetBothCommand.Execute(null);

        // Assert
        Assert.Equal("Failed to set default device", capturedError);
    }

    [Fact]
    public void SetDefaultCommand_NoErrorCallback_WhenSucceeds()
    {
        // Arrange
        var fakeService = new FakeAudioDeviceService();
        fakeService.AddOrUpdateMicrophone(new FakeAudioDeviceService.FakeMicrophone("mic-1", "Desk Mic"));
        
        var device = fakeService.GetMicrophones().Single();

        string? capturedError = null;
        var viewModel = new MicrophoneEntryViewModel(device, fakeService, error => capturedError = error);

        // Act
        viewModel.SetDefaultCommand.Execute(null);

        // Assert - no error callback when successful
        Assert.Null(capturedError);
        Assert.Equal("mic-1", fakeService.DefaultConsoleId);
    }

    #endregion
}
