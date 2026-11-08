using Blink1.Api.Transport;

namespace Blink1.Api.Test.Support;

/// <summary>A HID layer with a fixed set of devices, all sharing one <see cref="FakeTransport"/>.</summary>
internal sealed class FakeBackend(params HidDeviceDescriptor[] devices) : IBlink1HidBackend
{
	public FakeTransport Transport { get; } = new();

	public string? OpenedPath { get; private set; }

	public IReadOnlyList<HidDeviceDescriptor> Enumerate() => devices;

	public IBlink1Transport Open(string devicePath)
	{
		OpenedPath = devicePath;
		return Transport;
	}
}
