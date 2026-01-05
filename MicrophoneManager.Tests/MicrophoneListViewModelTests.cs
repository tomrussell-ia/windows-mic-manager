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
}
