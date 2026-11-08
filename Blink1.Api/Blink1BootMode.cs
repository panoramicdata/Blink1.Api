namespace Blink1.Api;

/// <summary>The device's behaviour at power-up.</summary>
public enum Blink1BootMode
{
	/// <summary>Play the stored pattern if no host talks to the device (the factory default).</summary>
	Normal = 0,

	/// <summary>Play the stored pattern between the configured lines.</summary>
	Play = 1,

	/// <summary>Stay dark.</summary>
	Off = 2,
}
