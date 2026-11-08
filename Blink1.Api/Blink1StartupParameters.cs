namespace Blink1.Api;

/// <summary>What the device does when it is powered up (mk3 and later).</summary>
/// <param name="BootMode">The behaviour at power-up.</param>
/// <param name="PlayStart">The first pattern line played when <paramref name="BootMode"/> is <see cref="Blink1BootMode.Play"/>.</param>
/// <param name="PlayEnd">The pattern line that playback ends before.</param>
/// <param name="PlayCount">How many times playback repeats; 0 repeats for ever.</param>
public sealed record Blink1StartupParameters(Blink1BootMode BootMode, byte PlayStart, byte PlayEnd, byte PlayCount);
