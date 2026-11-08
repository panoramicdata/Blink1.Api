using Blink1.Api.Test.Support;

namespace Blink1.Api.Test.Core;

public class ClientTests
{
	private static readonly CancellationToken None = CancellationToken.None;

	[Fact]
	public void Constructor_OpensTheFirstDeviceBySerialNumber()
	{
		var backend = new FakeBackend(TestDevices.Mk3, TestDevices.Mk2);

		using var client = new Blink1Client(new Blink1ClientOptions(), backend);

		client.Device.SerialNumber.Should().Be("20002");
		backend.OpenedPath.Should().Be("path-mk2");
	}

	[Fact]
	public void Constructor_OpensTheDeviceWithTheSerialNumber()
	{
		var backend = new FakeBackend(TestDevices.Mk2, TestDevices.Mk3);

		using var client = new Blink1Client(new Blink1ClientOptions { SerialNumber = "30001234" }, backend);

		client.Device.Generation.Should().Be(Blink1Generation.Mk3);
		backend.OpenedPath.Should().Be("path-mk3");
	}

	[Fact]
	public void Constructor_OpensTheDeviceAtThePath()
	{
		var backend = new FakeBackend(TestDevices.Mk2, TestDevices.Mk1);

		using var client = new Blink1Client(new Blink1ClientOptions { DevicePath = "PATH-MK1" }, backend);

		client.Device.Generation.Should().Be(Blink1Generation.Mk1);
	}

	[Fact]
	public void Constructor_NamesWhatItCouldNotFind()
	{
		var backend = new FakeBackend(TestDevices.Mk2);

		FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions { SerialNumber = "nope" }, backend))
			.Should().Throw<Blink1Exception>().WithMessage("No blink(1) with serial number 'nope' is attached.");
		FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions { DevicePath = "elsewhere" }, backend))
			.Should().Throw<Blink1Exception>().WithMessage("No blink(1) is attached at 'elsewhere'.");
		FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions(), new FakeBackend()))
			.Should().Throw<Blink1Exception>().WithMessage("No blink(1) is attached.");
		backend.OpenedPath.Should().BeNull();
	}

	[Fact]
	public void Constructor_RejectsBothSerialNumberAndPath()
		=> FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions { SerialNumber = "20002", DevicePath = "path-mk2" }, new FakeBackend(TestDevices.Mk2)))
			.Should().Throw<ArgumentException>().WithMessage("*not both*");

	[Fact]
	public void Constructor_RejectsNulls()
	{
		FluentActions.Invoking(() => new Blink1Client(null!, new FakeBackend()))
			.Should().Throw<ArgumentNullException>().WithParameterName("options");
		FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions(), null!))
			.Should().Throw<ArgumentNullException>().WithParameterName("backend");
	}

	[Fact]
	public void Open_ThrowsWhenNoDeviceMatches()
		=> FluentActions.Invoking(() => Blink1Client.Open(new Blink1ClientOptions { SerialNumber = "not-a-blink1-serial" }))
			.Should().Throw<Blink1Exception>().WithMessage("*'not-a-blink1-serial'*");

	[Fact]
	public void GetDevices_RemovesDuplicatesAndSortsBySerialThenPath()
	{
		var backend = new FakeBackend(
			TestDevices.Mk3,
			TestDevices.Mk2,
			TestDevices.Mk2 with { DevicePath = "PATH-MK2" },
			TestDevices.Mk2 with { DevicePath = "another-mk2" });

		var devices = Blink1Client.GetDevices(backend);

		devices.Select(d => d.DevicePath).Should().Equal("another-mk2", "path-mk2", "path-mk3");
	}

	[Fact]
	public void GetDevices_ReturnsOnlyBlink1Devices()
		=> Blink1Client.GetDevices().Should().OnlyContain(d => d.DevicePath.Length > 0);

	[Fact]
	public async Task Dispose_ClosesTheTransportOnceAndStopsTheClient()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		client.Dispose();
		client.Dispose();

		transport.IsDisposed.Should().BeTrue();
		await FluentActions.Awaiting(() => client.TurnOffAsync(None)).Should().ThrowAsync<ObjectDisposedException>();
	}

	[Fact]
	public async Task ReadOnly_RefusesCommandsThatChangeTheDeviceAndAllowsReads()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, static o => o.IsReadOnly = true);
		transport.Answer((byte)'v', 0, (byte)'2', (byte)'4');

		await FluentActions.Awaiting(() => client.SetColorAsync(Blink1Colors.Red, None))
			.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'n'*read-only*");
		(await client.GetFirmwareVersionAsync(None)).Should().Be(204);

		transport.Sent.Should().ContainSingle().Which[1].Should().Be((byte)'v');
	}

	[Fact]
	public async Task Generation_RefusesCommandsTheDeviceLacks()
	{
		var (mk2, mk2Transport) = TestDevices.Open(TestDevices.Mk2);
		var (mk1, _) = TestDevices.Open(TestDevices.Mk1);

		await FluentActions.Awaiting(() => mk2.ReadEepromAsync(0, None))
			.Should().ThrowAsync<NotSupportedException>().WithMessage("Command 'e' needs a blink(1) Mk1; '20002' is Mk2.");
		await FluentActions.Awaiting(() => mk2.GetChipIdAsync(None))
			.Should().ThrowAsync<NotSupportedException>().WithMessage("Command 'U' needs a blink(1) Mk3 or Mk4*");
		await FluentActions.Awaiting(() => mk1.ReadColorAsync(1, None))
			.Should().ThrowAsync<NotSupportedException>();
		mk2Transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public async Task UnknownGeneration_AllowsEveryCommandAndAnyPosition()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Unknown);
		transport.Answer((byte)'e', 0, 9, 0, 0, 0, 0, 0);
		transport.Answer((byte)'R', 1, 2, 3, 0, 0, 0, 0);

		(await client.ReadEepromAsync(0, None)).Should().Be(9);
		await client.WriteNoteAsync(0, new byte[1], None);
		var line = await client.ReadPatternLineAsync(255, None);
		await client.PlayLoopAsync(255, 255, 0, None);
		await client.EnableServerTickleAsync(TimeSpan.FromSeconds(1), false, 255, 255, None);

		line.Color.Should().Be(new Blink1Color(1, 2, 3));
		client.Device.PatternLineCount.Should().Be(0);
		transport.Sent.Select(r => (char)r[1]).Should().Equal('e', 'F', 'R', 'p', 'D');
	}

	public static TheoryData<Exception> TransportFailures => [new IOException("unplugged"), new TimeoutException("unplugged"), new UnauthorizedAccessException("unplugged")];

	[Theory]
	[MemberData(nameof(TransportFailures))]
	public async Task TransportFailures_OnSend_BecomeBlink1Exceptions(Exception failure)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailure = failure;

		(await FluentActions.Awaiting(() => client.SetColorAsync(Blink1Colors.Red, None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("The blink(1) '20002' failed command 'n': unplugged"))
			.Which.InnerException.Should().BeSameAs(failure);
	}

	[Fact]
	public async Task TransportFailures_OnRead_BecomeBlink1Exceptions()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.GetFailure = new IOException("unplugged");

		await FluentActions.Awaiting(() => client.ReadPlayStateAsync(None))
			.Should().ThrowAsync<Blink1Exception>().WithMessage("*failed command 'S'*");

		await FluentActions.Awaiting(() => client.ReadPlayStateAsync(None)).Should().ThrowAsync<Blink1Exception>("the gate is released after a failure");
	}

	[Fact]
	public async Task TransientFailures_AreRetriedUntilTheCommandGetsThrough()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailures.Enqueue(new IOException("busy"));
		transport.SetFailures.Enqueue(new TimeoutException("busy"));

		await client.SetColorAsync(Blink1Colors.Red, None);

		transport.SetAttempts.Should().Be(3);
		transport.Sent.Should().ContainSingle();
	}

	public static TheoryData<Exception, int, int> PersistentFailures => new()
	{
		{ new IOException("gone"), 2, 3 },
		{ new TimeoutException("gone"), 3, 4 },
		{ new IOException("gone"), 0, 1 },
		{ new UnauthorizedAccessException("denied"), 3, 1 },
	};

	[Theory]
	[MemberData(nameof(PersistentFailures))]
	public async Task PersistentFailures_AreSentOnceThenRetriedOnlyIfTransient(Exception failure, int maxRetries, int expectedAttempts)
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2, o => o.MaxRetries = maxRetries);
		transport.SetFailure = failure;

		await FluentActions.Awaiting(() => client.SetColorAsync(Blink1Colors.Red, None)).Should().ThrowAsync<Blink1Exception>();

		transport.SetAttempts.Should().Be(expectedAttempts);
	}

	[Fact]
	public async Task AFailedRead_ResendsTheQuery()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.GetFailures.Enqueue(new IOException("busy"));
		transport.Answer((byte)'v', 0, (byte)'2', (byte)'4');

		(await client.GetFirmwareVersionAsync(None)).Should().Be(204);
		transport.Sent.Should().HaveCount(2).And.OnlyContain(r => r[1] == (byte)'v');
	}

	[Fact]
	public void Options_RejectNegativeRetries()
		=> FluentActions.Invoking(() => new Blink1Client(new Blink1ClientOptions { MaxRetries = -1 }, new FakeBackend(TestDevices.Mk2)))
			.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("MaxRetries");

	[Fact]
	public async Task OtherTransportExceptions_AreNotWrapped()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);
		transport.SetFailure = new InvalidOperationException("bug");

		await FluentActions.Awaiting(() => client.SetColorAsync(Blink1Colors.Red, None))
			.Should().ThrowAsync<InvalidOperationException>().WithMessage("bug");
	}

	[Fact]
	public async Task Cancellation_StopsACommandBeforeItIsSent()
	{
		var (client, transport) = TestDevices.Open(TestDevices.Mk2);

		await FluentActions.Awaiting(() => client.SetColorAsync(Blink1Colors.Red, new CancellationToken(true)))
			.Should().ThrowAsync<OperationCanceledException>();
		transport.Sent.Should().BeEmpty();
	}

	[Fact]
	public void Blink1Exception_HasTheStandardConstructors()
	{
		new Blink1Exception().Message.Should().NotBeEmpty();
		new Blink1Exception("m").Message.Should().Be("m");
		new Blink1Exception("m", new IOException("i")).InnerException.Should().BeOfType<IOException>();
	}
}
