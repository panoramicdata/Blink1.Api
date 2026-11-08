namespace Blink1.Api.Protocol;

/// <summary>One firmware command: its letter, feature report, and where it may be sent.</summary>
/// <param name="Code">The command letter, sent as byte 1 of the report.</param>
/// <param name="ReportId">1 for the 8-byte report; 2 for the 60-byte report (mk3 and later).</param>
/// <param name="ChangesState">Whether the command changes the device, so <see cref="Blink1ClientOptions.IsReadOnly"/> refuses it.</param>
/// <param name="Supports">The generations that accept the command.</param>
internal sealed record Blink1Command(char Code, byte ReportId, bool ChangesState, Blink1Generation[] Supports)
{
	private static readonly Blink1Generation[] All = [Blink1Generation.Mk1, Blink1Generation.Mk2, Blink1Generation.Mk3, Blink1Generation.Mk4];
	private static readonly Blink1Generation[] Mk2OrLater = [Blink1Generation.Mk2, Blink1Generation.Mk3, Blink1Generation.Mk4];
	private static readonly Blink1Generation[] Mk3OrLater = [Blink1Generation.Mk3, Blink1Generation.Mk4];
	private static readonly Blink1Generation[] Mk1Only = [Blink1Generation.Mk1];

	public static Blink1Command FadeToColor { get; } = new('c', 1, true, All);
	public static Blink1Command SetColor { get; } = new('n', 1, true, All);
	public static Blink1Command ReadColor { get; } = new('r', 1, false, Mk2OrLater);
	public static Blink1Command ServerTickle { get; } = new('D', 1, true, All);
	public static Blink1Command PlayLoop { get; } = new('p', 1, true, Mk2OrLater);
	public static Blink1Command ReadPlayState { get; } = new('S', 1, false, Mk2OrLater);
	public static Blink1Command WritePatternLine { get; } = new('P', 1, true, All);
	public static Blink1Command SavePatterns { get; } = new('W', 1, true, Mk2OrLater);
	public static Blink1Command ReadPatternLine { get; } = new('R', 1, false, All);
	public static Blink1Command SetPatternLed { get; } = new('l', 1, true, Mk2OrLater);
	public static Blink1Command ReadEeprom { get; } = new('e', 1, false, Mk1Only);
	public static Blink1Command WriteEeprom { get; } = new('E', 1, true, Mk1Only);
	public static Blink1Command GetVersion { get; } = new('v', 1, false, All);
	public static Blink1Command Test { get; } = new('!', 1, false, All);
	public static Blink1Command WriteNote { get; } = new('F', 2, true, Mk3OrLater);
	public static Blink1Command ReadNote { get; } = new('f', 2, false, Mk3OrLater);
	public static Blink1Command GoToBootloader { get; } = new('G', 2, true, Mk3OrLater);
	public static Blink1Command LockBootloader { get; } = new('L', 2, true, Mk3OrLater);
	public static Blink1Command SetStartupParameters { get; } = new('B', 1, true, Mk3OrLater);
	public static Blink1Command GetStartupParameters { get; } = new('b', 1, false, Mk3OrLater);
	public static Blink1Command GetChipId { get; } = new('U', 2, false, Mk3OrLater);

	/// <summary>The full feature-report buffer length, including the report ID byte.</summary>
	public int ReportLength => ReportId == 1 ? 9 : 61;
}
