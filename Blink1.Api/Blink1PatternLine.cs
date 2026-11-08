namespace Blink1.Api;

/// <summary>One line of the colour pattern stored on the device.</summary>
/// <param name="Color">The colour to fade to.</param>
/// <param name="FadeTime">The fade time, in steps of 10 ms, up to 655.35 s.</param>
/// <param name="Led">The LED the line drives: <see cref="Blink1Led.All"/>, or an LED number (mk2 and later).</param>
public sealed record Blink1PatternLine(Blink1Color Color, TimeSpan FadeTime, byte Led);
