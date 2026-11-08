using Microsoft.Extensions.Configuration;

namespace Blink1.Api.IntegrationTest;

/// <summary>
/// A <see cref="Blink1Client"/> for a real, attached blink(1), with its colours and stored pattern restored afterwards.
/// </summary>
/// <remarks>
/// Opens the first blink(1) attached, or the one named by <c>Blink1:SerialNumber</c> (user secrets or the
/// <c>Blink1__SerialNumber</c> environment variable). With no blink(1) attached the tests fail with a message saying so;
/// nothing is skipped. Gamma correction is off, so colours read back are the colours sent.
/// </remarks>
public sealed class Blink1Fixture : IAsyncLifetime
{
	private readonly List<Blink1PatternLine> _originalPattern = [];
	private Blink1Color[] _originalColors = [];

	public Blink1Fixture()
	{
		Configuration = new ConfigurationBuilder()
			.AddUserSecrets<Blink1Fixture>()
			.AddEnvironmentVariables()
			.Build();
		Client = Open(static _ => { });
	}

	public IConfiguration Configuration { get; }

	public Blink1Client Client { get; }

	/// <summary>The device's LEDs: one on mk1, two on later generations.</summary>
	public byte[] Leds => Client.Device.Generation == Blink1Generation.Mk1 ? [Blink1Led.All] : [Blink1Led.First, Blink1Led.Second];

	/// <summary>The stored pattern as it was before any test ran.</summary>
	public IReadOnlyList<Blink1PatternLine> OriginalPattern => _originalPattern;

	/// <summary>Opens another client on the same device, letting <paramref name="configure"/> change its options.</summary>
	public Blink1Client Open(Action<Blink1ClientOptions> configure)
	{
		var options = new Blink1ClientOptions { SerialNumber = Configuration["Blink1:SerialNumber"], ApplyGammaCorrection = false };
		configure(options);
		try
		{
			return Blink1Client.Open(options);
		}
		catch (Blink1Exception ex)
		{
			throw new InvalidOperationException(
				"Integration tests need a blink(1) attached by USB. " + ex.Message
				+ " To pick one of several, set 'dotnet user-secrets set Blink1:SerialNumber <serial> --project Blink1.Api.IntegrationTest'.",
				ex);
		}
	}

	public async ValueTask InitializeAsync()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		await Client.StopAsync(cancellationToken);
		// mk1 cannot read its colour, so it is left dark afterwards.
		_originalColors = Client.Device.Generation == Blink1Generation.Mk1
			? [Blink1Colors.Black]
			: [.. await Task.WhenAll(Leds.Select(led => Client.ReadColorAsync(led, cancellationToken)))];
		for (byte position = 0; position < Client.Device.PatternLineCount; position++)
		{
			_originalPattern.Add(await Client.ReadPatternLineAsync(position, cancellationToken));
		}
	}

	public async ValueTask DisposeAsync()
	{
		await Client.StopAsync(CancellationToken.None);
		await Client.DisableServerTickleAsync(CancellationToken.None);
		await RestorePatternAsync(CancellationToken.None);
		for (var i = 0; i < Leds.Length; i++)
		{
			await Client.SetColorAsync(_originalColors[i], Leds[i], CancellationToken.None);
		}

		Client.Dispose();
	}

	/// <summary>Writes the original pattern back into the device's RAM.</summary>
	public async Task RestorePatternAsync(CancellationToken cancellationToken)
	{
		for (var position = 0; position < _originalPattern.Count; position++)
		{
			await Client.WritePatternLineAsync((byte)position, _originalPattern[position], cancellationToken);
		}
	}
}
