using Blink1.Api.Protocol;

namespace Blink1.Api;

public sealed partial class Blink1Client
{
	/// <summary>Fades every LED to <paramref name="color"/> over <paramref name="fadeTime"/> (command <c>c</c>).</summary>
	[Blink1Command('c')]
	public Task FadeToColorAsync(Blink1Color color, TimeSpan fadeTime, CancellationToken cancellationToken)
		=> FadeToColorAsync(color, fadeTime, Blink1Led.All, cancellationToken);

	/// <summary>Fades one LED, or <see cref="Blink1Led.All"/>, to <paramref name="color"/> over <paramref name="fadeTime"/> (command <c>c</c>; single LEDs need mk2 or later).</summary>
	[Blink1Command('c')]
	public Task FadeToColorAsync(Blink1Color color, TimeSpan fadeTime, byte led, CancellationToken cancellationToken)
	{
		var (high, low) = EncodeDuration(fadeTime, nameof(fadeTime));
		var sent = Outgoing(color);
		return WriteAsync(Blink1Command.FadeToColor, [sent.Red, sent.Green, sent.Blue, high, low, led], cancellationToken);
	}

	/// <summary>Turns every LED off at once (command <c>n</c> with black).</summary>
	[Blink1Command('n')]
	public Task TurnOffAsync(CancellationToken cancellationToken)
		=> SetColorAsync(Blink1Colors.Black, Blink1Led.All, cancellationToken);

	/// <summary>Sets every LED to <paramref name="color"/> immediately (command <c>n</c>).</summary>
	[Blink1Command('n')]
	public Task SetColorAsync(Blink1Color color, CancellationToken cancellationToken)
		=> SetColorAsync(color, Blink1Led.All, cancellationToken);

	/// <summary>
	/// Sets one LED, or <see cref="Blink1Led.All"/>, to <paramref name="color"/> immediately (command <c>n</c> for every
	/// LED; for one LED, command <c>c</c> with no fade, because mk2 firmware 204 misreads <c>n</c>'s LED byte and starts playback).
	/// A single LED changes on the device's next update, so <see cref="ReadColorAsync"/> sees it a few milliseconds later.
	/// </summary>
	[Blink1Command('n')]
	[Blink1Command('c')]
	public Task SetColorAsync(Blink1Color color, byte led, CancellationToken cancellationToken)
	{
		if (led != Blink1Led.All)
		{
			return FadeToColorAsync(color, TimeSpan.Zero, led, cancellationToken);
		}

		var sent = Outgoing(color);
		return WriteAsync(Blink1Command.SetColor, [sent.Red, sent.Green, sent.Blue], cancellationToken);
	}

	/// <summary>Reads the colour an LED is showing now (command <c>r</c>; mk2 or later). With gamma correction on, this is the corrected value.</summary>
	[Blink1Command('r')]
	public async Task<Blink1Color> ReadColorAsync(byte led, CancellationToken cancellationToken)
	{
		var response = await QueryAsync(Blink1Command.ReadColor, [0, 0, 0, 0, 0, led], cancellationToken).ConfigureAwait(false);
		ExpectEcho(response, Blink1Command.ReadColor);
		return new Blink1Color(response[2], response[3], response[4]);
	}
}
