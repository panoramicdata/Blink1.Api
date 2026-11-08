using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Blink1.Api;

/// <summary>A 24-bit RGB colour.</summary>
/// <param name="Red">The red component, 0 to 255.</param>
/// <param name="Green">The green component, 0 to 255.</param>
/// <param name="Blue">The blue component, 0 to 255.</param>
public readonly record struct Blink1Color(byte Red, byte Green, byte Blue)
{
	/// <summary>Parses <c>#RRGGBB</c>, <c>RRGGBB</c> or <c>0xRRGGBB</c>, ignoring case.</summary>
	/// <exception cref="FormatException"><paramref name="value"/> is not a colour in one of those forms.</exception>
	public static Blink1Color Parse(string value)
		=> TryParse(value, out var color)
			? color
			: throw new FormatException($"'{value}' is not a colour: expected #RRGGBB, RRGGBB or 0xRRGGBB.");

	/// <summary>Parses <c>#RRGGBB</c>, <c>RRGGBB</c> or <c>0xRRGGBB</c>, ignoring case, without throwing.</summary>
	public static bool TryParse([NotNullWhen(true)] string? value, out Blink1Color color)
	{
		color = default;
		if (value is null)
		{
			return false;
		}

		var hex = value.Trim();
		if (hex.StartsWith('#'))
		{
			hex = hex[1..];
		}
		else if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			hex = hex[2..];
		}

		if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
		{
			return false;
		}

		color = new Blink1Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
		return true;
	}

	/// <summary>The colour as <c>#RRGGBB</c>.</summary>
	public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}
