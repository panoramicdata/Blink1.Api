namespace Blink1.Api;

/// <summary>Common colours.</summary>
public static class Blink1Colors
{
	/// <summary>All channels off.</summary>
	public static Blink1Color Black { get; } = new(0, 0, 0);

	/// <summary>All channels full.</summary>
	public static Blink1Color White { get; } = new(255, 255, 255);

	/// <summary>Full red.</summary>
	public static Blink1Color Red { get; } = new(255, 0, 0);

	/// <summary>Full green.</summary>
	public static Blink1Color Green { get; } = new(0, 255, 0);

	/// <summary>Full blue.</summary>
	public static Blink1Color Blue { get; } = new(0, 0, 255);

	/// <summary>Red and green.</summary>
	public static Blink1Color Yellow { get; } = new(255, 255, 0);

	/// <summary>Green and blue.</summary>
	public static Blink1Color Cyan { get; } = new(0, 255, 255);

	/// <summary>Red and blue.</summary>
	public static Blink1Color Magenta { get; } = new(255, 0, 255);
}
