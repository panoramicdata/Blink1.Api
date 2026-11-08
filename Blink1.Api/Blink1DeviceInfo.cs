using Blink1.Api.Transport;
using System.Globalization;

namespace Blink1.Api;

/// <summary>A blink(1) attached to this computer.</summary>
/// <param name="DevicePath">The platform's path for the device, usable as <see cref="Blink1ClientOptions.DevicePath"/>.</param>
/// <param name="SerialNumber">The USB serial number, or empty when the platform could not read it.</param>
/// <param name="ProductName">The USB product string, such as <c>blink(1) mk2</c>, or empty.</param>
/// <param name="Manufacturer">The USB manufacturer string (<c>ThingM</c>), or empty.</param>
/// <param name="ReleaseNumberBcd">The USB device release number, in binary-coded decimal.</param>
/// <param name="Generation">The hardware generation.</param>
public sealed record Blink1DeviceInfo(
	string DevicePath,
	string SerialNumber,
	string ProductName,
	string Manufacturer,
	int ReleaseNumberBcd,
	Blink1Generation Generation)
{
	/// <summary>The ThingM USB vendor ID.</summary>
	public const int VendorId = 0x27B8;

	/// <summary>The blink(1) USB product ID.</summary>
	public const int ProductId = 0x01ED;

	/// <summary>How many lines the stored colour pattern has, or 0 when the generation is unknown.</summary>
	public int PatternLineCount => Generation switch
	{
		Blink1Generation.Unknown => 0,
		Blink1Generation.Mk1 => 12,
		_ => 32,
	};

	internal static Blink1DeviceInfo From(HidDeviceDescriptor descriptor)
		=> new(
			descriptor.DevicePath,
			descriptor.SerialNumber ?? string.Empty,
			descriptor.ProductName ?? string.Empty,
			descriptor.Manufacturer ?? string.Empty,
			descriptor.ReleaseNumberBcd,
			GenerationOf(descriptor.ProductName, descriptor.SerialNumber));

	// The product string wins: a real mk2 reports serial "20002", which ThingM's serial-range rule reads as mk1.
	internal static Blink1Generation GenerationOf(string? productName, string? serialNumber)
	{
		var product = productName ?? string.Empty;
		foreach (var (marker, generation) in ProductMarkers)
		{
			if (product.Contains(marker, StringComparison.OrdinalIgnoreCase))
			{
				return generation;
			}
		}

		if (serialNumber is not { Length: 8 } || !uint.TryParse(serialNumber, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var serial))
		{
			return Blink1Generation.Unknown;
		}

		return serial switch
		{
			>= 0x40000000 => Blink1Generation.Mk4,
			>= 0x30000000 => Blink1Generation.Mk3,
			>= 0x20000000 => Blink1Generation.Mk2,
			_ => Blink1Generation.Mk1,
		};
	}

	private static readonly (string Marker, Blink1Generation Generation)[] ProductMarkers =
	[
		("mk4", Blink1Generation.Mk4),
		("mk3", Blink1Generation.Mk3),
		("mk2", Blink1Generation.Mk2),
	];
}
