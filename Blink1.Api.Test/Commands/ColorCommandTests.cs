using Blink1.Api.Test.Support;

namespace Blink1.Api.Test.Commands;

public class ColorCommandTests
{
	private static readonly CancellationToken None = CancellationToken.None;

	[Fact]
	public async Task FadeToColorAsync_SendsCommandC()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.FadeToColorAsync(new Blink1Color(1, 2, 3), TimeSpan.FromMilliseconds(5000), Blink1Led.Second, None);
		await client.FadeToColorAsync(new Blink1Color(4, 5, 6), TimeSpan.FromMilliseconds(15), None);

		transport.Sent.Should().HaveCount(2);
		transport.Sent[0].Should().Equal(1, (byte)'c', 1, 2, 3, 0x01, 0xF4, 2, 0);
		transport.Sent[1].Should().Equal(1, (byte)'c', 4, 5, 6, 0, 1, 0, 0);
	}

	[Fact]
	public async Task FadeToColorAsync_AppliesGammaCorrectionByDefault()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, static o => o.ApplyGammaCorrection = true);

		await client.FadeToColorAsync(new Blink1Color(128, 255, 21), TimeSpan.Zero, None);

		transport.Sent[0][2..5].Should().Equal(55, 255, 0);
	}

	[Fact]
	public async Task FadeToColorAsync_CarriesTheLongestDuration()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.FadeToColorAsync(Blink1Colors.White, Blink1Client.MaxDuration, None);

		transport.Sent[0][5..7].Should().Equal(0xFF, 0xFF);
	}

	[Fact]
	public async Task FadeToColorAsync_RejectsADurationOutOfRange()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await FluentActions.Awaiting(() => client.FadeToColorAsync(Blink1Colors.Red, TimeSpan.FromMilliseconds(-1), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("fadeTime");
		await FluentActions.Awaiting(() => client.FadeToColorAsync(Blink1Colors.Red, Blink1Client.MaxDuration + TimeSpan.FromMilliseconds(10), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("fadeTime");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task TurnOffAsync_TurnsEveryLedOffAtOnce()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, static o => o.ApplyGammaCorrection = true);

		await client.TurnOffAsync(None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'n', 0, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task SetColorAsync_SendsCommandN()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.SetColorAsync(new Blink1Color(9, 8, 7), None);
		await client.SetColorAsync(new Blink1Color(1, 2, 3), Blink1Led.First, None);

		transport.Sent[0].Should().Equal(1, (byte)'n', 9, 8, 7, 0, 0, 0, 0);
		transport.Sent[1].Should().Equal(new byte[] { 1, (byte)'c', 1, 2, 3, 0, 0, 1, 0 }, "mk2 firmware 204 starts playback when 'n' names an LED");
	}

	[Fact]
	public async Task SetColorAsync_ForEveryLed_UsesCommandN()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, static o => o.ApplyGammaCorrection = true);

		await client.SetColorAsync(new Blink1Color(128, 0, 0), Blink1Led.All, None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'n', 55, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task ReadColorAsync_ReturnsTheColour()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'r', 0, 0xFF, 0x10, 0, 0, 1, 0);

		var color = await client.ReadColorAsync(Blink1Led.Second, None);

		color.Should().Be(new Blink1Color(0, 0xFF, 0x10));
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'r', 0, 0, 0, 0, 0, 2, 0);
		transport.ReadLengths.Should().Equal(9);
	}

	[Fact]
	public async Task ReadColorAsync_RejectsAnAnswerToAnotherCommand()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'v', 0, (byte)'2', (byte)'4');

		await FluentActions.Awaiting(() => client.ReadColorAsync(Blink1Led.First, None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("*answered command 'r' with 'v'*");
	}
}
