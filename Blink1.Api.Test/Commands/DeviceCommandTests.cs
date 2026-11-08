using Blink1.Api.Test.Support;
using System.Text;

namespace Blink1.Api.Test.Commands;

public class DeviceCommandTests
{
	private static readonly CancellationToken None = CancellationToken.None;

	[Fact]
	public async Task GetFirmwareVersionAsync_ReturnsTheVersionNumber()
	{
		// The answer the attached mk2 gave: 00 76 00 32 34 00 00 00 00.
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'v', 0, (byte)'2', (byte)'4', 0, 0, 0, 0);

		var version = await client.GetFirmwareVersionAsync(None);

		version.Should().Be(204);
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'v', 0, 0, 0, 0, 0, 0, 0);
	}

	[Theory]
	[InlineData((byte)'x', (byte)'4')]
	[InlineData((byte)'2', 0)]
	public async Task GetFirmwareVersionAsync_RejectsAVersionThatIsNotTwoDigits(byte first, byte second)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'v', 0, first, second);

		await FluentActions.Awaiting(() => client.GetFirmwareVersionAsync(None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("*not two digits*");
	}

	[Fact]
	public async Task RunTestCommandAsync_ReturnsTheRawAnswer()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.Answer((byte)'!', 1, 2, 3, 4, 5, 6, 7);

		var answer = await client.RunTestCommandAsync(None);

		answer.Should().Equal((byte)'!', 1, 2, 3, 4, 5, 6, 7);
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'!', 0, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task ReadEepromAsync_ReturnsTheByte()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk1);
		transport.Answer((byte)'e', 3, 0x5A, 0, 0, 0, 0, 0);

		var value = await client.ReadEepromAsync(3, None);

		value.Should().Be(0x5A);
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'e', 3, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task WriteEepromAsync_SendsCommandE()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk1);

		await client.WriteEepromAsync(1, 2, None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'E', 1, 2, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task WriteNoteAsync_SendsCommandFOnReportTwo()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);

		await client.WriteNoteAsync(4, "hello"u8.ToArray(), None);

		var sent = transport.Sent.Should().ContainSingle().Subject;
		sent.Should().HaveCount(61);
		sent[..9].Should().Equal(2, (byte)'F', 4, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0);
		sent[9..].Should().OnlyContain(b => b == 0);
	}

	[Fact]
	public async Task WriteNoteAsync_AcceptsAFullNoteAndRejectsALongerOne()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);

		await client.WriteNoteAsync(0, new byte[Blink1Client.NoteSize], None);
		await FluentActions.Awaiting(() => client.WriteNoteAsync(0, new byte[Blink1Client.NoteSize + 1], None))
			.Should().ThrowAsync<ArgumentException>().WithParameterName("data");

		transport.Sent.Should().ContainSingle();
	}

	[Fact]
	public async Task ReadNoteAsync_ReturnsTheNote()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);
		var answer = new byte[60];
		answer[0] = (byte)'f';
		answer[1] = 7;
		for (var i = 0; i < Blink1Client.NoteSize; i++)
		{
			answer[2 + i] = (byte)(i + 1);
		}

		transport.Answers.Enqueue(answer);

		var note = await client.ReadNoteAsync(7, None);

		note.Should().HaveCount(Blink1Client.NoteSize).And.StartWith(new byte[] { 1, 2, 3 }).And.EndWith(new byte[] { 50 });
		transport.Sent.Should().ContainSingle().Which[..3].Should().Equal(2, (byte)'f', 7);
		transport.ReadLengths.Should().Equal(61);
	}

	[Theory]
	[InlineData("GOBOOT", true)]
	[InlineData("NOBOOT", false)]
	public async Task EnterBootloaderAsync_ReportsWhetherTheDeviceAgreed(string answer, bool expected)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);
		transport.Answer(Encoding.ASCII.GetBytes(answer));

		var agreed = await client.EnterBootloaderAsync(None);

		agreed.Should().Be(expected);
		transport.Sent.Should().ContainSingle().Which[..8].Should().Equal(2, (byte)'G', (byte)'o', (byte)'B', (byte)'o', (byte)'o', (byte)'t', 0);
	}

	[Theory]
	[InlineData("LOCKED", true)]
	[InlineData("NOLOCK", false)]
	public async Task LockBootloaderAsync_ReportsWhetherTheDeviceLocked(string answer, bool expected)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);
		transport.Answer(Encoding.ASCII.GetBytes(answer));

		var locked = await client.LockBootloaderAsync(None);

		locked.Should().Be(expected);
		Encoding.ASCII.GetString(transport.Sent.Should().ContainSingle().Subject, 1, 12).Should().Be("LockBootload");
	}

	[Fact]
	public async Task SetStartupParametersAsync_SendsCommandB()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);

		await client.SetStartupParametersAsync(new Blink1StartupParameters(Blink1BootMode.Play, 2, 8, 3), None);

		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'B', 1, 2, 8, 3, 0, 0, 0);
	}

	[Fact]
	public async Task SetStartupParametersAsync_ValidatesItsArguments()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);

		await FluentActions.Awaiting(() => client.SetStartupParametersAsync(null!, None))
			.Should().ThrowAsync<ArgumentNullException>().WithParameterName("parameters");
		await FluentActions.Awaiting(() => client.SetStartupParametersAsync(new Blink1StartupParameters(Blink1BootMode.Play, 32, 0, 0), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("parameters");
		await FluentActions.Awaiting(() => client.SetStartupParametersAsync(new Blink1StartupParameters(Blink1BootMode.Play, 0, 33, 0), None))
			.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("parameters");
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task GetStartupParametersAsync_ReturnsTheParameters()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);
		transport.Answer((byte)'b', 2, 1, 9, 4, 0, 0, 0);

		var parameters = await client.GetStartupParametersAsync(None);

		parameters.Should().Be(new Blink1StartupParameters(Blink1BootMode.Off, 1, 9, 4));
		transport.Sent.Should().ContainSingle().Which.Should().Equal(1, (byte)'b', 0, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public async Task GetChipIdAsync_ReturnsTheIdBlock()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk3);
		var answer = new byte[60];
		answer[0] = (byte)'U';
		for (var i = 1; i < answer.Length; i++)
		{
			answer[i] = (byte)i;
		}

		transport.Answers.Enqueue(answer);

		var id = await client.GetChipIdAsync(None);

		id.Should().HaveCount(58).And.StartWith(new byte[] { 1, 2 }).And.EndWith(new byte[] { 58 });
		transport.Sent.Should().ContainSingle().Which[..2].Should().Equal(2, (byte)'U');
	}
}
