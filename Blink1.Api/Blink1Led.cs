namespace Blink1.Api;

/// <summary>LED numbers for the commands that address a single LED (mk2 and later).</summary>
public static class Blink1Led
{
	/// <summary>Every LED on the device.</summary>
	public const byte All = 0;

	/// <summary>The first LED (the top, on mk2 and later).</summary>
	public const byte First = 1;

	/// <summary>The second LED (the bottom, on mk2 and later).</summary>
	public const byte Second = 2;
}
