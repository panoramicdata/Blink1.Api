using Blink1.Api.Protocol;

namespace Blink1.Api;

public sealed partial class Blink1Client
{
	/// <summary>
	/// Writes one line of the stored pattern, in RAM until <see cref="SavePatternsAsync"/> (commands <c>l</c> then
	/// <c>P</c>; on mk1, <c>P</c> alone, which also saves it).
	/// </summary>
	[Blink1Command('P')]
	[Blink1Command('l')]
	public async Task WritePatternLineAsync(byte position, Blink1PatternLine line, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(line);
		EnsurePosition(position, nameof(position));
		var (high, low) = EncodeDuration(line.FadeTime, nameof(line));
		if (Device.Generation == Blink1Generation.Mk1)
		{
			if (line.Led != Blink1Led.All)
			{
				throw new NotSupportedException($"A blink(1) Mk1 has one LED, so a pattern line cannot address LED {line.Led}.");
			}
		}
		else
		{
			await SetPatternLedAsync(line.Led, cancellationToken).ConfigureAwait(false);
		}

		var sent = Outgoing(line.Color);
		await WriteAsync(Blink1Command.WritePatternLine, [sent.Red, sent.Green, sent.Blue, high, low, position], cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Sets the LED that later <c>P</c> commands write pattern lines for (command <c>l</c>; mk2 or later).</summary>
	[Blink1Command('l')]
	public Task SetPatternLedAsync(byte led, CancellationToken cancellationToken)
		=> WriteAsync(Blink1Command.SetPatternLed, [led], cancellationToken);

	/// <summary>Reads one line of the stored pattern (command <c>R</c>).</summary>
	[Blink1Command('R')]
	public async Task<Blink1PatternLine> ReadPatternLineAsync(byte position, CancellationToken cancellationToken)
	{
		EnsurePosition(position, nameof(position));
		var response = await QueryAsync(Blink1Command.ReadPatternLine, [0, 0, 0, 0, 0, position], cancellationToken).ConfigureAwait(false);
		ExpectEcho(response, Blink1Command.ReadPatternLine);
		return new Blink1PatternLine(new Blink1Color(response[2], response[3], response[4]), DecodeDuration(response[5], response[6]), response[7]);
	}

	/// <summary>
	/// Saves the pattern in RAM to flash, so it survives power-down (command <c>W</c>; mk2 or later). The device answers
	/// slowly for about ten seconds afterwards while the write finishes.
	/// </summary>
	[Blink1Command('W')]
	public async Task SavePatternsAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Never repeated: each repeat would start another flash write.
			await WriteOnceAsync(Blink1Command.SavePatterns, [0xBE, 0xEF, 0xCA, 0xFE], cancellationToken).ConfigureAwait(false);
		}
		catch (Blink1Exception ex) when (ex.InnerException is IOException)
		{
			// The device stalls USB while it starts the flash write, so the send reports failure; an answer proves it is back.
			await GetFirmwareVersionAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Plays the stored pattern from <paramref name="startPosition"/> to the end, for ever (command <c>p</c>; mk2 or later).</summary>
	[Blink1Command('p')]
	public Task PlayAsync(byte startPosition, CancellationToken cancellationToken)
		=> PlayLoopAsync(startPosition, 0, 0, cancellationToken);

	/// <summary>
	/// Plays pattern lines <paramref name="startPosition"/> to <paramref name="endPosition"/> inclusive (0 for the end),
	/// <paramref name="count"/> times (0 for ever) (command <c>p</c>; mk2 or later). <see cref="Blink1PlayState.LoopEnd"/>
	/// then reads back as <paramref name="endPosition"/> + 1.
	/// </summary>
	[Blink1Command('p')]
	public Task PlayLoopAsync(byte startPosition, byte endPosition, byte count, CancellationToken cancellationToken)
	{
		EnsurePosition(startPosition, nameof(startPosition));
		EnsurePosition(endPosition, nameof(endPosition));
		return WriteAsync(Blink1Command.PlayLoop, [1, startPosition, endPosition, count], cancellationToken);
	}

	/// <summary>Stops pattern playback, leaving the LEDs as they are (command <c>p</c>; mk2 or later).</summary>
	[Blink1Command('p')]
	public Task StopAsync(CancellationToken cancellationToken)
		=> WriteAsync(Blink1Command.PlayLoop, [0], cancellationToken);

	/// <summary>Reads the playback state (command <c>S</c>; mk2 or later).</summary>
	[Blink1Command('S')]
	public async Task<Blink1PlayState> ReadPlayStateAsync(CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.ReadPlayState, [], cancellationToken).ConfigureAwait(false);
		ExpectEcho(response, Blink1Command.ReadPlayState);
		return new Blink1PlayState(response[2] != 0, response[3], response[4], response[5], response[6]);
	}

	/// <summary>Arms server tickle: unless the host calls again within <paramref name="timeout"/>, the device starts playing its pattern (command <c>D</c>).</summary>
	[Blink1Command('D')]
	public Task EnableServerTickleAsync(TimeSpan timeout, CancellationToken cancellationToken)
		=> EnableServerTickleAsync(timeout, false, 0, 0, cancellationToken);

	/// <summary>
	/// Arms server tickle: unless the host calls again within <paramref name="timeout"/>, the device plays pattern lines
	/// from <paramref name="startPosition"/> up to, but not including, <paramref name="endPosition"/> (0 and 0 for the
	/// whole pattern). Unlike <see cref="PlayLoopAsync"/>, the firmware takes this end exclusive. When
	/// <paramref name="maintainState"/> is false the LEDs are turned off first (mk2 or later) (command <c>D</c>).
	/// </summary>
	[Blink1Command('D')]
	public Task EnableServerTickleAsync(TimeSpan timeout, bool maintainState, byte startPosition, byte endPosition, CancellationToken cancellationToken)
	{
		var (high, low) = EncodeDuration(timeout, nameof(timeout));
		EnsurePosition(startPosition, nameof(startPosition));
		EnsureLoopEnd(endPosition, nameof(endPosition));
		return WriteAsync(Blink1Command.ServerTickle, [1, high, low, (byte)(maintainState ? 1 : 0), startPosition, endPosition], cancellationToken);
	}

	/// <summary>Disarms server tickle (command <c>D</c>).</summary>
	[Blink1Command('D')]
	public Task DisableServerTickleAsync(CancellationToken cancellationToken)
		=> WriteAsync(Blink1Command.ServerTickle, [0], cancellationToken);
}
