using Blink1.Api.Transport;

namespace Blink1.Api.Test.Support;

/// <summary>Device descriptors for each generation, shaped like real ones (the mk2 is the attached test device's).</summary>
internal static class TestDevices
{
	public static HidDeviceDescriptor Mk1 { get; } = new("path-mk1", "00001234", "blink(1)", "ThingM", 0x0001);

	public static HidDeviceDescriptor Mk2 { get; } = new("path-mk2", "20002", "blink(1) mk2", "ThingM", 0x0002);

	public static HidDeviceDescriptor Mk3 { get; } = new("path-mk3", "30001234", "blink(1) mk3", "ThingM", 0x0003);

	public static HidDeviceDescriptor Unknown { get; } = new("path-unknown", null, null, null, 0);

	/// <summary>A client on <paramref name="device"/> with gamma correction off, so sent colours are the ones given.</summary>
	public static (Blink1Client Client, FakeTransport Transport) Open(HidDeviceDescriptor device)
		=> Open(device, static _ => { });

	public static (Blink1Client Client, FakeTransport Transport) Open(HidDeviceDescriptor device, Action<Blink1ClientOptions> configure)
	{
		var backend = new FakeBackend(device);
		var options = new Blink1ClientOptions { ApplyGammaCorrection = false };
		configure(options);
		return (new Blink1Client(options, backend), backend.Transport);
	}
}
