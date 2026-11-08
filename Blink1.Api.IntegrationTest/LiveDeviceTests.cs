namespace Blink1.Api.IntegrationTest;

[Collection(typeof(LiveDevice))]
public class LiveDeviceTests(Blink1Fixture fixture)
{
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	private Blink1Client Client => fixture.Client;

	[Fact]
	public void GetDevices_FindsTheOpenDevice()
	{
		var devices = Blink1Client.GetDevices();

		devices.Should().ContainSingle(d => d.DevicePath == Client.Device.DevicePath)
			.Which.Should().Be(Client.Device);
		Client.Device.Manufacturer.Should().Be("ThingM");
		Client.Device.Generation.Should().NotBe(Blink1Generation.Unknown, "the product string or serial number identifies the generation");
	}

	[Fact]
	public async Task Open_BySerialNumber_OpensTheSameDevice()
	{
		using var second = fixture.Open(o => o.SerialNumber = Client.Device.SerialNumber);

		second.Device.Should().Be(Client.Device);
		(await second.GetFirmwareVersionAsync(Token)).Should().Be(await Client.GetFirmwareVersionAsync(Token));
	}

	[Fact]
	public async Task GetFirmwareVersionAsync_ReturnsAPlausibleVersion()
		=> (await Client.GetFirmwareVersionAsync(Token)).Should().BeInRange(100, 999);

	[Fact]
	public async Task RunTestCommandAsync_Answers()
		=> (await Client.RunTestCommandAsync(Token)).Should().HaveCount(8);

	[Fact]
	public async Task SetColorAsync_EachLed_ReadsBack()
	{
		var colors = new[] { new Blink1Color(200, 10, 0), new Blink1Color(0, 20, 180) };

		for (var i = 0; i < fixture.Leds.Length; i++)
		{
			await Client.SetColorAsync(colors[i], fixture.Leds[i], Token);
		}

		await Task.Delay(TimeSpan.FromMilliseconds(50), Token);
		for (var i = 0; i < fixture.Leds.Length; i++)
		{
			(await Client.ReadColorAsync(fixture.Leds[i], Token)).Should().Be(colors[i]);
		}
	}

	[Fact]
	public async Task FadeToColorAsync_ArrivesAtTheColour()
	{
		var target = new Blink1Color(30, 60, 90);

		await Client.FadeToColorAsync(target, TimeSpan.FromMilliseconds(200), Token);
		await Task.Delay(TimeSpan.FromMilliseconds(500), Token);

		foreach (var led in fixture.Leds)
		{
			(await Client.ReadColorAsync(led, Token)).Should().Be(target);
		}
	}

	[Fact]
	public async Task TurnOffAsync_LeavesEveryLedBlack()
	{
		await Client.SetColorAsync(Blink1Colors.White, Token);

		await Client.TurnOffAsync(Token);

		foreach (var led in fixture.Leds)
		{
			(await Client.ReadColorAsync(led, Token)).Should().Be(Blink1Colors.Black);
		}
	}

	[Fact]
	public async Task WritePatternLineAsync_ReadsBack()
	{
		var last = (byte)(Client.Device.PatternLineCount - 1);
		var line = new Blink1PatternLine(new Blink1Color(11, 22, 33), TimeSpan.FromMilliseconds(1230), fixture.Leds[^1]);

		try
		{
			await Client.WritePatternLineAsync(last, line, Token);

			(await Client.ReadPatternLineAsync(last, Token)).Should().Be(line);
		}
		finally
		{
			await fixture.RestorePatternAsync(Token);
		}
	}

	[Fact]
	public async Task SavePatternsAsync_KeepsTheStoredPattern()
	{
		await fixture.RestorePatternAsync(Token);

		await Client.SavePatternsAsync(Token);

		for (byte position = 0; position < Client.Device.PatternLineCount; position++)
		{
			(await Client.ReadPatternLineAsync(position, Token)).Should().Be(fixture.OriginalPattern[position]);
		}
	}

	[Fact]
	public async Task PlayLoopAsync_PlaysUntilStopped()
	{
		try
		{
			await Client.PlayLoopAsync(1, 3, 0, Token);

			var playing = await Client.ReadPlayStateAsync(Token);
			playing.IsPlaying.Should().BeTrue();
			playing.LoopStart.Should().Be(1);
			playing.LoopEnd.Should().Be(4, "PlayLoopAsync's end is inclusive and reads back as one more");

			await Client.StopAsync(Token);

			(await Client.ReadPlayStateAsync(Token)).IsPlaying.Should().BeFalse();
		}
		finally
		{
			await Client.StopAsync(Token);
		}
	}

	[Fact]
	public async Task PlayAsync_StartsFromTheGivenLine()
	{
		try
		{
			await Client.PlayAsync(2, Token);

			var state = await Client.ReadPlayStateAsync(Token);
			state.IsPlaying.Should().BeTrue();
			state.LoopStart.Should().Be(2);
		}
		finally
		{
			await Client.StopAsync(Token);
		}
	}

	[Fact]
	public async Task ServerTickle_FiresWhenNotTickledAndDisarms()
	{
		try
		{
			await Client.EnableServerTickleAsync(TimeSpan.FromMilliseconds(300), Token);
			await Task.Delay(TimeSpan.FromMilliseconds(1000), Token);

			(await Client.ReadPlayStateAsync(Token)).IsPlaying.Should().BeTrue("server tickle plays the pattern once the host stops calling");
		}
		finally
		{
			await Client.DisableServerTickleAsync(Token);
			await Client.StopAsync(Token);
		}
	}

	[Fact]
	public async Task ServerTickle_PlaysTheGivenRangeWithAnExclusiveEnd()
	{
		try
		{
			await Client.EnableServerTickleAsync(TimeSpan.FromMilliseconds(200), true, 1, 3, Token);
			await Task.Delay(TimeSpan.FromMilliseconds(800), Token);

			var state = await Client.ReadPlayStateAsync(Token);
			state.IsPlaying.Should().BeTrue();
			state.LoopStart.Should().Be(1);
			state.LoopEnd.Should().Be(3);
		}
		finally
		{
			await Client.DisableServerTickleAsync(Token);
			await Client.StopAsync(Token);
		}
	}

	[Fact]
	public async Task SetColorAsync_OneLed_LeavesTheOtherAloneAndDoesNotStartPlayback()
	{
		await Client.SetColorAsync(Blink1Colors.Black, Blink1Led.All, Token);

		await Client.SetColorAsync(new Blink1Color(40, 50, 60), fixture.Leds[0], Token);
		await Task.Delay(TimeSpan.FromMilliseconds(50), Token);

		(await Client.ReadColorAsync(fixture.Leds[0], Token)).Should().Be(new Blink1Color(40, 50, 60));
		(await Client.ReadColorAsync(fixture.Leds[^1], Token)).Should().Be(fixture.Leds.Length > 1 ? Blink1Colors.Black : new Blink1Color(40, 50, 60));
		(await Client.ReadPlayStateAsync(Token)).IsPlaying.Should().BeFalse();
	}

	[Fact]
	public async Task ServerTickle_StaysQuietWhileTickled()
	{
		try
		{
			for (var i = 0; i < 4; i++)
			{
				await Client.EnableServerTickleAsync(TimeSpan.FromMilliseconds(800), Token);
				await Task.Delay(TimeSpan.FromMilliseconds(250), Token);
			}

			(await Client.ReadPlayStateAsync(Token)).IsPlaying.Should().BeFalse();
		}
		finally
		{
			await Client.DisableServerTickleAsync(Token);
			await Client.StopAsync(Token);
		}
	}

	[Fact]
	public async Task ReadOnlyClient_ReadsButRefusesWrites()
	{
		using var readOnly = fixture.Open(static o => o.IsReadOnly = true);

		(await readOnly.GetFirmwareVersionAsync(Token)).Should().BeGreaterThan(0);
		await FluentActions.Awaiting(() => readOnly.SetColorAsync(Blink1Colors.Red, Token)).Should().ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task CommandsForOtherGenerations_AreRefusedBeforeSending()
	{
		var generation = Client.Device.Generation;
		if (generation != Blink1Generation.Mk1)
		{
			await FluentActions.Awaiting(() => Client.ReadEepromAsync(0, Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.WriteEepromAsync(0, 0, Token)).Should().ThrowAsync<NotSupportedException>();
		}

		if (generation is Blink1Generation.Mk1 or Blink1Generation.Mk2)
		{
			await FluentActions.Awaiting(() => Client.GetChipIdAsync(Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.ReadNoteAsync(0, Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.WriteNoteAsync(0, new byte[1], Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.GetStartupParametersAsync(Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.SetStartupParametersAsync(new Blink1StartupParameters(Blink1BootMode.Normal, 0, 0, 0), Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.EnterBootloaderAsync(Token)).Should().ThrowAsync<NotSupportedException>();
			await FluentActions.Awaiting(() => Client.LockBootloaderAsync(Token)).Should().ThrowAsync<NotSupportedException>();
		}
	}
}
