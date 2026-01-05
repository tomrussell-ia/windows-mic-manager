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
}
