using Blink1.Api.Protocol;
using System.Text;

namespace Blink1.Api;

public sealed partial class Blink1Client
{
	/// <summary>The size of one note, in bytes.</summary>
	public static int NoteSize => 50;

	/// <summary>Reads the firmware version as a number, such as 204 for version 2.04 (command <c>v</c>).</summary>
	[Blink1Command('v')]
	public async Task<int> GetFirmwareVersionAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.GetVersion, [], cancellationToken).ConfigureAwait(false);
		ExpectEcho(response, Blink1Command.GetVersion);
		if (!char.IsAsciiDigit((char)response[3]) || !char.IsAsciiDigit((char)response[4]))
		{
			throw new Blink1Exception($"The blink(1) '{Device.SerialNumber}' reported a firmware version that is not two digits.");
		}

		return ((response[3] - '0') * 100) + (response[4] - '0');
	}

	/// <summary>Sends the firmware's test command and returns its raw answer, without the report ID (command <c>!</c>).</summary>
	[Blink1Command('!')]
	public async Task<byte[]> RunTestCommandAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAfterSettlingAsync(Blink1Command.Test, [], cancellationToken).ConfigureAwait(false);
		return response[1..];
	}

	/// <summary>Reads one EEPROM byte (command <c>e</c>; mk1 only).</summary>
	[Blink1Command('e')]
	public async Task<byte> ReadEepromAsync(byte address, CancellationToken cancellationToken)
	{
		var response = await QueryAfterSettlingAsync(Blink1Command.ReadEeprom, [address], cancellationToken).ConfigureAwait(false);
		return response[3];
	}

	/// <summary>Writes one EEPROM byte (command <c>E</c>; mk1 only). Addresses 0 to 5 hold calibration, boot mode and the serial number.</summary>
	[Blink1Command('E')]
	public Task WriteEepromAsync(byte address, byte value, CancellationToken cancellationToken)
		=> WriteAsync(Blink1Command.WriteEeprom, [address, value], cancellationToken);

	/// <summary>Stores up to <see cref="NoteSize"/> bytes in note <paramref name="noteId"/>, zero-padded (command <c>F</c>; mk3 or later).</summary>
	[Blink1Command('F')]
	public Task WriteNoteAsync(byte noteId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
	{
		if (data.Length > NoteSize)
		{
			throw new ArgumentException($"A note holds {NoteSize} bytes; {data.Length} were given.", nameof(data));
		}

		return WriteAsync(Blink1Command.WriteNote, [noteId, .. data.Span], cancellationToken);
	}

	/// <summary>Reads the <see cref="NoteSize"/> bytes of note <paramref name="noteId"/> (command <c>f</c>; mk3 or later).</summary>
	[Blink1Command('f')]
	public async Task<byte[]> ReadNoteAsync(byte noteId, CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.ReadNote, [noteId], cancellationToken).ConfigureAwait(false);
		return response[3..(3 + NoteSize)];
	}

	/// <summary>
	/// Restarts the device into its USB bootloader for a firmware update; it stops being a blink(1) until updated or
	/// replugged. Returns false when the bootloader is locked (command <c>G</c>; mk3 or later).
	/// </summary>
	[Blink1Command('G')]
	public async Task<bool> EnterBootloaderAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.GoToBootloader, [.. "oBoot"u8], cancellationToken).ConfigureAwait(false);
		return Answered(response, "GOBOOT");
	}

	/// <summary>
	/// Permanently stops <see cref="EnterBootloaderAsync"/> working, so firmware can only be updated by modifying the
	/// hardware. This cannot be undone. Returns whether the device confirmed the lock (command <c>L</c>; mk3 or later).
	/// </summary>
	[Blink1Command('L')]
	public async Task<bool> LockBootloaderAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.LockBootloader, [.. "ockBootload"u8], cancellationToken).ConfigureAwait(false);
		return Answered(response, "LOCKED");
	}

	/// <summary>Sets what the device does at power-up (command <c>B</c>; mk3 or later).</summary>
	[Blink1Command('B')]
	public Task SetStartupParametersAsync(Blink1StartupParameters parameters, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		EnsurePosition(parameters.PlayStart, nameof(parameters));
		EnsureLoopEnd(parameters.PlayEnd, nameof(parameters));
		return WriteAsync(
			Blink1Command.SetStartupParameters,
			[(byte)parameters.BootMode, parameters.PlayStart, parameters.PlayEnd, parameters.PlayCount],
			cancellationToken);
	}

	/// <summary>Reads what the device does at power-up (command <c>b</c>; mk3 or later).</summary>
	[Blink1Command('b')]
	public async Task<Blink1StartupParameters> GetStartupParametersAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.GetStartupParameters, [], cancellationToken).ConfigureAwait(false);
		return new Blink1StartupParameters((Blink1BootMode)response[2], response[3], response[4], response[5]);
	}

	/// <summary>Reads the microcontroller's unique ID block: the 58 bytes after the command byte (command <c>U</c>; mk3 or later).</summary>
	[Blink1Command('U')]
	public async Task<byte[]> GetChipIdAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.GetChipId, [], cancellationToken).ConfigureAwait(false);
		return response[2..60];
	}

	private static bool Answered(byte[] response, string expected)
		=> Encoding.ASCII.GetString(response, 1, expected.Length) == expected;
}
