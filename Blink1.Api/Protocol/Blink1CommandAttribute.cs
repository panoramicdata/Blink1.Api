namespace Blink1.Api.Protocol;

/// <summary>Names the firmware command a <see cref="Blink1Client"/> method sends, so the command inventory can be checked.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class Blink1CommandAttribute(char code) : Attribute
{
	public char Code { get; } = code;
}
