namespace Blink1.Api.Transport;

/// <summary>What the HID layer reports about one attached blink(1).</summary>
internal sealed record HidDeviceDescriptor(
	string DevicePath,
	string? SerialNumber,
	string? ProductName,
	string? Manufacturer,
	int ReleaseNumberBcd);
