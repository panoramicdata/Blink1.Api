namespace Blink1.Api;

/// <summary>A blink(1) hardware generation, which decides the commands the device supports.</summary>
public enum Blink1Generation
{
	/// <summary>The generation could not be determined; no command is refused on generation grounds.</summary>
	Unknown = 0,

	/// <summary>The original single-LED blink(1) (2012).</summary>
	Mk1 = 1,

	/// <summary>The two-LED blink(1) mk2.</summary>
	Mk2 = 2,

	/// <summary>The blink(1) mk3 (2018), which adds notes, startup parameters, the chip ID and bootloader commands.</summary>
	Mk3 = 3,

	/// <summary>The blink(1) mk4.</summary>
	Mk4 = 4,
}
