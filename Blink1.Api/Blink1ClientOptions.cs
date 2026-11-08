namespace Blink1.Api;

/// <summary>Which blink(1) a <see cref="Blink1Client"/> opens, and how it behaves.</summary>
public sealed class Blink1ClientOptions
{
	/// <summary>The serial number of the device to open (case-insensitive). With neither this nor <see cref="DevicePath"/>, the first device is opened.</summary>
	public string? SerialNumber { get; set; }

	/// <summary>The platform device path of the device to open, as <see cref="Blink1DeviceInfo.DevicePath"/> reports it.</summary>
	public string? DevicePath { get; set; }

	/// <summary>When true, every command that changes the device's state is refused with <see cref="InvalidOperationException"/> before it is sent.</summary>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// When true (the default), colours sent to the device are gamma-corrected as ThingM's own tools do, so they look the
	/// same; colours read back are the corrected values the device holds.
	/// </summary>
	public bool ApplyGammaCorrection { get; set; } = true;

	/// <summary>
	/// How many times a command is re-sent after its USB transfer fails, 50 ms apart (default 3). Transfers fail now and
	/// then while the device finishes a flash write; every blink(1) command is safe to repeat.
	/// </summary>
	public int MaxRetries { get; set; } = 3;

	internal void Validate()
	{
		ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries);

		if (SerialNumber is not null && DevicePath is not null)
		{
			throw new ArgumentException("Set SerialNumber or DevicePath, not both.");
		}
	}
}
