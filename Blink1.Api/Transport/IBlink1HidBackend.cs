namespace Blink1.Api.Transport;

/// <summary>Finds and opens blink(1) devices.</summary>
internal interface IBlink1HidBackend
{
	/// <summary>Every attached blink(1).</summary>
	IReadOnlyList<HidDeviceDescriptor> Enumerate();

	/// <summary>Opens the device at <paramref name="devicePath"/>.</summary>
	IBlink1Transport Open(string devicePath);
}
