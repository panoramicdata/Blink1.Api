using HidSharp;
using System.Diagnostics.CodeAnalysis;

namespace Blink1.Api.Transport;

/// <summary>The HidSharp-backed HID layer used outside tests.</summary>
[ExcludeFromCodeCoverage(Justification = "Hardware boundary with no logic of its own; Blink1.Api.IntegrationTest exercises it against a real device.")]
internal sealed class HidSharpBackend : IBlink1HidBackend
{
	public static HidSharpBackend Instance { get; } = new();

	public IReadOnlyList<HidDeviceDescriptor> Enumerate()
		=> [.. Devices().Select(static device => new HidDeviceDescriptor(
			device.DevicePath,
			TryRead(device.GetSerialNumber),
			TryRead(device.GetProductName),
			TryRead(device.GetManufacturer),
			device.ReleaseNumberBcd))];

	public IBlink1Transport Open(string devicePath)
	{
		var device = Devices().FirstOrDefault(d => string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase))
			?? throw new Blink1Exception($"The blink(1) at '{devicePath}' is no longer attached.");
		try
		{
			return new HidSharpTransport(device.Open(), device.GetMaxFeatureReportLength());
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			throw new Blink1Exception($"The blink(1) at '{devicePath}' could not be opened: {ex.Message}", ex);
		}
	}

	private static IEnumerable<HidDevice> Devices() => DeviceList.Local.GetHidDevices(Blink1DeviceInfo.VendorId, Blink1DeviceInfo.ProductId);

	private static string? TryRead(Func<string> read)
	{
		try
		{
			return read();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			return null;
		}
	}

	private sealed class HidSharpTransport(HidStream stream, int maxFeatureReportLength) : IBlink1Transport
	{
		public void SetFeature(byte[] report) => stream.SetFeature(Sized(report));

		public void GetFeature(byte[] report)
		{
			var buffer = Sized(report);
			stream.GetFeature(buffer);
			if (!ReferenceEquals(buffer, report))
			{
				Array.Copy(buffer, report, report.Length);
			}
		}

		public void Dispose() => stream.Dispose();

		// Windows wants every feature buffer at the device's longest feature report length (as hidapi pads it).
		private byte[] Sized(byte[] report)
		{
			if (!OperatingSystem.IsWindows() || report.Length >= maxFeatureReportLength)
			{
				return report;
			}

			var padded = new byte[maxFeatureReportLength];
			report.CopyTo(padded, 0);
			return padded;
		}
	}
}
