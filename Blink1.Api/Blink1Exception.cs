namespace Blink1.Api;

/// <summary>A blink(1) could not be found, opened or talked to, or it answered unexpectedly.</summary>
public class Blink1Exception : Exception
{
	/// <summary>Creates the exception.</summary>
	public Blink1Exception()
	{
	}

	/// <summary>Creates the exception with a message.</summary>
	public Blink1Exception(string message) : base(message)
	{
	}

	/// <summary>Creates the exception with a message and the transport failure that caused it.</summary>
	public Blink1Exception(string message, Exception innerException) : base(message, innerException)
	{
	}
}
