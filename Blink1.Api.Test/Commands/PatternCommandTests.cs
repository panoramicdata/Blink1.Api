using Blink1.Api.Test.Support;

namespace Blink1.Api.Test.Commands;

public class PatternCommandTests
{
	private static readonly CancellationToken None = CancellationToken.None;

	[Fact]
	public async Task WritePatternLineAsync_SetsTheLedThenWritesTheLine()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.WritePatternLineAsync(31, new Blink1PatternLine(new Blink1Color(255, 0, 255), TimeSpan.FromMilliseconds(500), Blink1Led.Second), None);

		transport.Sent.Should().HaveCount(2);
		transport.Sent[0].Should().Equal(1, (byte)'l', 2, 0, 0, 0, 0, 0, 0);
		transport.Sent[1].Should().Equal(1, (byte)'P', 255, 0, 255, 0, 50, 31, 0);
	}

	[Fact]
	public async Task WritePatternLineAsync_OnMk1_SendsOnlyTheLine()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk1);

		await client.WritePatternLineAsync(11, new Blink1PatternLine(Blink1Colors.Blue, TimeSpan.Zero, Blink1Led.All), None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'P', 0, 0, 255, 0, 0, 11, 0);
	}

	[Fact]
	public async Task WritePatternLineAsync_OnMk1_RefusesASingleLed()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk1);

		await FluentActions.Awaiting(() => client.WritePatternLineAsync(0, new Blink1PatternLine(Blink1Colors.Blue, TimeSpan.Zero, Blink1Led.First), None))
			.Should().ThrowAsync<NotSupportedException>().WithMessage("*one LED*");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task WritePatternLineAsync_ValidatesItsArguments()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await FluentActions.Awaiting(() => client.WritePatternLineAsync(0, null!, None))
			.Should().ThrowAsync<ArgumentNullException>().WithParameterName("line");
		await FluentActions.Awaiting(() => client.WritePatternLineAsync(32, new Blink1PatternLine(Blink1Colors.Red, TimeSpan.Zero, 0), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("position");
		await FluentActions.Awaiting(() => client.WritePatternLineAsync(0, new Blink1PatternLine(Blink1Colors.Red, TimeSpan.FromDays(1), 0), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("line");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task WritePatternLineAsync_AppliesGammaCorrection()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, static o => o.ApplyGammaCorrection = true);

		await client.WritePatternLineAsync(0, new Blink1PatternLine(new Blink1Color(128, 0, 0), TimeSpan.Zero, 0), None);

		transport.Sent[1][2].Should().Be(55);
	}

	[Fact]
	public async Task SetPatternLedAsync_SendsCommandL()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.SetPatternLedAsync(Blink1Led.First, None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'l', 1, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task ReadPatternLineAsync_ReturnsTheLine()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'R', 0xFF, 0, 0, 0x01, 0x02, 1, 0);

		var line = await client.ReadPatternLineAsync(5, None);

		line.Should().Be(new Blink1PatternLine(Blink1Colors.Red, TimeSpan.FromMilliseconds(2580), 1));
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'R', 0, 0, 0, 0, 0, 5, 0);
	}

	[Fact]
	public async Task ReadPatternLineAsync_RejectsAPositionPastThePattern()
	{
		var (client, _) = TestDevices.Open(TestDevices.Mk1);

		await FluentActions.Awaiting(() => client.ReadPatternLineAsync(12, None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*lines 0 to 11*");
	}

	[Fact]
	public async Task SavePatternsAsync_SendsCommandWWithItsKey()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.SavePatternsAsync(None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'W', 0xBE, 0xEF, 0xCA, 0xFE, 0, 0, 0);
	}

	[Fact]
	public async Task SavePatternsAsync_AcceptsTheFlashWriteStallOnceTheDeviceAnswers()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailures.Enqueue(new IOException("SetFeature failed."));
		transport.SetFailures.Enqueue(new IOException("still writing flash"));
		transport.Answer((byte)'v', 0, (byte)'2', (byte)'4');

		await client.SavePatternsAsync(None);

		transport.Sent.Should().ContainSingle().Which[1].Should().Be((byte)'v', "the first version query hit the stall and the second was answered");
	}

	[Fact]
	public async Task SavePatternsAsync_FailsWhenTheDeviceDoesNotComeBack()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailure = new IOException("gone");

		await FluentActions.Awaiting(() => client.SavePatternsAsync(None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("*failed command 'v'*");
	}

	[Fact]
	public async Task SavePatternsAsync_DoesNotHideOtherFailures()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailure = new TimeoutException("slow");

		await FluentActions.Awaiting(() => client.SavePatternsAsync(None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("*failed command 'W'*");
	}

	[Fact]
	public async Task PlayAsync_PlaysToTheEndForEver()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.PlayAsync(3, None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'p', 1, 3, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task PlayLoopAsync_SendsCommandP()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.PlayLoopAsync(2, 31, 5, None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'p', 1, 2, 31, 5, 0, 0, 0);
	}

	[Fact]
	public async Task PlayLoopAsync_RejectsAnEndPastTheLastLine()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await FluentActions.Awaiting(() => client.PlayLoopAsync(0, 32, 0, None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("endPosition");
		await FluentActions.Awaiting(() => client.PlayLoopAsync(32, 0, 0, None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("startPosition");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task StopAsync_SendsCommandPWithPlayOff()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.StopAsync(None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'p', 0, 0, 0, 0, 0, 0, 0);
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(0, false)]
	public async Task ReadPlayStateAsync_ReturnsTheState(byte playing, bool expected)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'S', playing, 1, 32, 3, 7, 0, 0);

		var state = await client.ReadPlayStateAsync(None);

		state.Should().Be(new Blink1PlayState(expected, 1, 32, 3, 7));
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'S', 0, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task EnableServerTickleAsync_SendsCommandD()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.EnableServerTickleAsync(TimeSpan.FromSeconds(30), true, 2, 6, None);
		await client.EnableServerTickleAsync(TimeSpan.FromSeconds(1), None);

		transport.Sent[0].Should().Equal(1, (byte)'D', 1, 0x0B, 0xB8, 1, 2, 6, 0);
		transport.Sent[1].Should().Equal(1, (byte)'D', 1, 0, 100, 0, 0, 0, 0);
	}

	[Fact]
	public async Task EnableServerTickleAsync_ValidatesItsArguments()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await FluentActions.Awaiting(() => client.EnableServerTickleAsync(TimeSpan.FromSeconds(-1), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("timeout");
		await FluentActions.Awaiting(() => client.EnableServerTickleAsync(TimeSpan.FromSeconds(1), false, 40, 0, None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("startPosition");
		await FluentActions.Awaiting(() => client.EnableServerTickleAsync(TimeSpan.FromSeconds(1), false, 0, 40, None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("endPosition");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task DisableServerTickleAsync_SendsCommandD()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await client.DisableServerTickleAsync(None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'D', 0, 0, 0, 0, 0, 0, 0);
	}
}
