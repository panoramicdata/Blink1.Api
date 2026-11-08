using Blink1.Api.Transport;

namespace Blink1.Api.Test.Support;

/// <summary>Records every feature report sent, and answers reads from a queue.</summary>
internal sealed class FakeTransport : IBlink1Transport
{
	public List<byte[]> Sent { get; } = [];

	public Queue<byte[]> Answers { get; } = new();

	public List<int> ReadLengths { get; } = [];

	public Exception? SetFailure { get; set; }

	/// <summary>Failures thrown by the next sends, one each, before <see cref="SetFailure"/> applies.</summary>
	public Queue<Exception> SetFailures { get; } = new();

	public Exception? GetFailure { get; set; }

	public bool IsDisposed { get; private set; }

	/// <summary>Queues an answer: byte 1 onwards of the report, after the report ID.</summary>
	public void Answer(params byte[] afterReportId) => Answers.Enqueue(afterReportId);

	public int SetAttempts { get; private set; }

	public void SetFeature(byte[] report)
	{
		SetAttempts++;
		if (SetFailures.TryDequeue(out var next))
		{
			throw next;
		}

		if (SetFailure is not null)
		{
			throw SetFailure;
		}

		Sent.Add([.. report]);
	}

	/// <summary>Failures thrown by the next reads, one each, before <see cref="GetFailure"/> applies.</summary>
	public Queue<Exception> GetFailures { get; } = new();

	public void GetFeature(byte[] report)
	{
		if (GetFailures.TryDequeue(out var next))
		{
			throw next;
		}

		if (GetFailure is not null)
		{
			throw GetFailure;
		}

		ReadLengths.Add(report.Length);
		Answers.Dequeue().CopyTo(report, 1);
	}

	public void Dispose() => IsDisposed = true;
}
