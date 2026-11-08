namespace Blink1.Api.Transport;

/// <summary>Feature-report access to one open blink(1). Byte 0 of every buffer is the report ID.</summary>
internal interface IBlink1Transport : IDisposable
{
	/// <summary>Sends a feature report.</summary>
	void SetFeature(byte[] report);

	/// <summary>Reads the feature report whose ID is in <c>report[0]</c> into <paramref name="report"/>.</summary>
	void GetFeature(byte[] report);
}
