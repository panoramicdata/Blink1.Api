using Blink1.Api.Protocol;
using Blink1.Api.Transport;

namespace Blink1.Api;

/// <summary>
/// An open blink(1). Every firmware command is a method here; commands a device's generation does not support throw
/// <see cref="NotSupportedException"/> before anything is sent. The client is safe to share between threads.
/// </summary>
public sealed partial class Blink1Client : IDisposable
{
	/// <summary>The longest fade or server-tickle time the protocol can carry (65535 steps of 10 ms).</summary>
	public static readonly TimeSpan MaxDuration = TimeSpan.FromMilliseconds(655350);

	private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(50);

	private readonly Blink1ClientOptions _options;
	private readonly IBlink1Transport _transport;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _disposed;

	internal Blink1Client(Blink1ClientOptions options, IBlink1HidBackend backend)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(backend);
		options.Validate();
		_options = options;
		Device = Select(GetDevices(backend), options);
		_transport = backend.Open(Device.DevicePath);
	}

	/// <summary>The device this client talks to.</summary>
	public Blink1DeviceInfo Device { get; }

	/// <summary>Opens the blink(1) that <paramref name="options"/> selects; default options open the first one attached.</summary>
	/// <exception cref="Blink1Exception">No matching blink(1) is attached, or it could not be opened.</exception>
	public static Blink1Client Open(Blink1ClientOptions options) => new(options, HidSharpBackend.Instance);

	/// <summary>Every blink(1) attached to this computer, ordered by serial number.</summary>
	public static IReadOnlyList<Blink1DeviceInfo> GetDevices() => GetDevices(HidSharpBackend.Instance);

	internal static IReadOnlyList<Blink1DeviceInfo> GetDevices(IBlink1HidBackend backend)
		=> [.. backend.Enumerate()
			.DistinctBy(static d => d.DevicePath, StringComparer.OrdinalIgnoreCase)
			.Select(Blink1DeviceInfo.From)
			.OrderBy(static d => d.SerialNumber, StringComparer.Ordinal)
			.ThenBy(static d => d.DevicePath, StringComparer.Ordinal)];

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_transport.Dispose();
		_gate.Dispose();
	}

	private static Blink1DeviceInfo Select(IReadOnlyList<Blink1DeviceInfo> devices, Blink1ClientOptions options)
	{
		var match = options switch
		{
			{ DevicePath: { } path } => devices.FirstOrDefault(d => string.Equals(d.DevicePath, path, StringComparison.OrdinalIgnoreCase)),
			{ SerialNumber: { } serial } => devices.FirstOrDefault(d => string.Equals(d.SerialNumber, serial, StringComparison.OrdinalIgnoreCase)),
			_ => devices.Count > 0 ? devices[0] : null,
		};

		return match ?? throw new Blink1Exception(options switch
		{
			{ DevicePath: { } path } => $"No blink(1) is attached at '{path}'.",
			{ SerialNumber: { } serial } => $"No blink(1) with serial number '{serial}' is attached.",
			_ => "No blink(1) is attached.",
		});
	}

	private Task<byte[]> WriteAsync(Blink1Command command, byte[] arguments, CancellationToken cancellationToken)
		=> ExchangeAsync(command, arguments, readBack: false, TimeSpan.Zero, _options.MaxRetries, cancellationToken);

	private Task<byte[]> WriteOnceAsync(Blink1Command command, byte[] arguments, CancellationToken cancellationToken)
		=> ExchangeAsync(command, arguments, readBack: false, TimeSpan.Zero, 0, cancellationToken);

	private Task<byte[]> QueryAsync(Blink1Command command, byte[] arguments, CancellationToken cancellationToken)
		=> ExchangeAsync(command, arguments, readBack: true, TimeSpan.Zero, _options.MaxRetries, cancellationToken);

	private Task<byte[]> QueryAfterSettlingAsync(Blink1Command command, byte[] arguments, CancellationToken cancellationToken)
		=> ExchangeAsync(command, arguments, readBack: true, SettleTime, _options.MaxRetries, cancellationToken);

	// Retrying after a failed send is safe here, unlike HTTP: every command sets or reads state, so repeating it is harmless.
	private async Task<byte[]> ExchangeAsync(Blink1Command command, byte[] arguments, bool readBack, TimeSpan settle, int retries, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		EnsureAllowed(command);

		var report = new byte[command.ReportLength];
		report[0] = command.ReportId;
		report[1] = (byte)command.Code;
		arguments.CopyTo(report, 2);

		for (var attempt = 0; attempt < retries; attempt++)
		{
			try
			{
				return await ExchangeOnceAsync(command, report, readBack, settle, cancellationToken).ConfigureAwait(false);
			}
			catch (Blink1Exception ex) when (ex.InnerException is IOException or TimeoutException)
			{
				await Task.Delay(SettleTime, cancellationToken).ConfigureAwait(false);
			}
		}

		return await ExchangeOnceAsync(command, report, readBack, settle, cancellationToken).ConfigureAwait(false);
	}

	private async Task<byte[]> ExchangeOnceAsync(Blink1Command command, byte[] report, bool readBack, TimeSpan settle, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			Transfer(command, _transport.SetFeature, report);
			if (!readBack)
			{
				return report;
			}

			if (settle > TimeSpan.Zero)
			{
				await Task.Delay(settle, cancellationToken).ConfigureAwait(false);
			}

			var response = new byte[command.ReportLength];
			response[0] = command.ReportId;
			Transfer(command, _transport.GetFeature, response);
			return response;
		}
		finally
		{
			_gate.Release();
		}
	}

	private void Transfer(Blink1Command command, Action<byte[]> featureReport, byte[] buffer)
	{
		try
		{
			featureReport(buffer);
		}
		catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
		{
			throw new Blink1Exception($"The blink(1) '{Device.SerialNumber}' failed command '{command.Code}': {ex.Message}", ex);
		}
	}

	private void EnsureAllowed(Blink1Command command)
	{
		if (command.ChangesState && _options.IsReadOnly)
		{
			throw new InvalidOperationException($"Command '{command.Code}' changes the blink(1), and this client is read-only (Blink1ClientOptions.IsReadOnly).");
		}

		if (Device.Generation != Blink1Generation.Unknown && !command.Supports.Contains(Device.Generation))
		{
			throw new NotSupportedException(
				$"Command '{command.Code}' needs a blink(1) {string.Join(" or ", command.Supports)}; '{Device.SerialNumber}' is {Device.Generation}.");
		}
	}

	private void EnsurePosition(byte position, string parameterName)
	{
		if (Device.PatternLineCount > 0 && position >= Device.PatternLineCount)
		{
			throw new ArgumentOutOfRangeException(parameterName, position, $"A {Device.Generation} pattern has lines 0 to {Device.PatternLineCount - 1}.");
		}
	}

	private void EnsureLoopEnd(byte end, string parameterName)
	{
		if (Device.PatternLineCount > 0 && end > Device.PatternLineCount)
		{
			throw new ArgumentOutOfRangeException(parameterName, end, $"A {Device.Generation} loop ends at line {Device.PatternLineCount} at most (0 means the end).");
		}
	}

	private Blink1Color Outgoing(Blink1Color color) => _options.ApplyGammaCorrection ? Blink1Gamma.Apply(color) : color;

	private static (byte High, byte Low) EncodeDuration(TimeSpan duration, string parameterName)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, parameterName);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(duration, MaxDuration, parameterName);
		var steps = (int)(duration.Ticks / TimeSpan.TicksPerMillisecond / 10);
		return ((byte)(steps >> 8), (byte)steps);
	}

	private static TimeSpan DecodeDuration(byte high, byte low) => TimeSpan.FromMilliseconds(((high << 8) | low) * 10);

	private void ExpectEcho(byte[] response, Blink1Command command)
	{
		if (response[1] != command.Code)
		{
			throw new Blink1Exception($"The blink(1) '{Device.SerialNumber}' answered command '{command.Code}' with '{(char)response[1]}'.");
		}
	}
}
