namespace Blink1.Api;

/// <summary>The device's pattern playback state.</summary>
/// <param name="IsPlaying">Whether the pattern is playing.</param>
/// <param name="LoopStart">The first pattern line of the loop.</param>
/// <param name="LoopEnd">The pattern line the loop ends before.</param>
/// <param name="RepeatCount">How many times the loop repeats; 0 repeats for ever.</param>
/// <param name="Position">The pattern line playing now.</param>
public sealed record Blink1PlayState(bool IsPlaying, byte LoopStart, byte LoopEnd, byte RepeatCount, byte Position);
