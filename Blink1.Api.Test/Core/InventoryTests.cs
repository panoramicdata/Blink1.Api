using Blink1.Api.Protocol;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Blink1.Api.Test.Core;

/// <summary>
/// Keeps <c>docs/endpoints/commands.md</c> (one row per firmware command) and <see cref="Blink1Client"/> in step: every
/// row names real client methods that send its command, and a real test; every command method is listed; and every row
/// is filled unless its category is in <c>docs/pending-categories.txt</c>.
/// </summary>
public partial class InventoryTests
{
	private const int DocumentedCommands = 21;

	private static readonly string RepoRoot = FindRepoRoot();
	private static readonly List<Row> Rows = ParseRows();
	private static readonly HashSet<string> PendingCategories = ReadPendingCategories();
	private static readonly List<(string Name, char Code)> Operations = FindOperations();

	public sealed record Row(string File, int Line, string Category, char Code, string[] ClientMethods, string[] Tests)
	{
		public bool IsComplete => ClientMethods.Length > 0 && Tests.Length > 0;

		public bool IsStarted => ClientMethods.Length > 0 || Tests.Length > 0;

		public override string ToString() => $"{File}:{Line} '{Code}'";
	}

	[Fact]
	public void Docs_HaveEveryDocumentedCommand()
		=> Rows.Select(r => r.Code).Should().HaveCount(DocumentedCommands).And.OnlyHaveUniqueItems("docs/endpoints lists each blink(1) firmware command once");

	[Fact]
	public void Docs_CoverEveryCommandTheClientKnows()
		=> typeof(Blink1Command).GetProperties(BindingFlags.Public | BindingFlags.Static)
			.Select(p => ((Blink1Command)p.GetValue(null)!).Code)
			.Should().BeEquivalentTo(Rows.Select(r => r.Code));

	[Fact]
	public void PendingCategories_AreCategoriesInTheDocs()
	{
		var categories = Rows.Select(r => r.Category).ToHashSet(StringComparer.Ordinal);

		AssertNone(PendingCategories.Where(c => !categories.Contains(c)), "every line of docs/pending-categories.txt must name a docs/endpoints file");
	}

	[Fact]
	public void NonPendingCategories_HaveEveryRowImplemented()
		=> AssertNone(
			Rows.Where(r => !PendingCategories.Contains(r.Category) && !r.IsComplete).Select(r => r.ToString()),
			"a category not in docs/pending-categories.txt must fill Client method and Test on every row");

	[Fact]
	public void StartedRows_HaveBothClientMethodAndTest()
		=> AssertNone(Rows.Where(r => r.IsStarted && !r.IsComplete).Select(r => r.ToString()), "a row must fill both Client method and Test, or neither");

	[Fact]
	public void ClientMethods_ExistAndSendTheirRowsCommand()
	{
		var offending = Rows
			.SelectMany(r => r.ClientMethods.Select(m => (Row: r, Name: m)))
			.Where(x => !Operations.Contains((x.Name, x.Row.Code)))
			.Select(x => $"{x.Row}: {x.Name}");

		AssertNone(offending, "each Client method must be a Blink1Client method marked [Blink1Command] with the row's code");
	}

	[Fact]
	public void Tests_Exist()
	{
		var tests = typeof(InventoryTests).Assembly.GetTypes()
			.SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttributes<FactAttribute>(true).Any()).Select(m => $"{t.Name}.{m.Name}"))
			.ToHashSet(StringComparer.Ordinal);

		AssertNone(Rows.SelectMany(r => r.Tests.Where(t => !tests.Contains(t)).Select(t => $"{r}: {t}")), "each Test must be TestClass.TestMethod of a [Fact] or [Theory] in Blink1.Api.Test");
	}

	[Fact]
	public void EveryCommandMethod_IsListed()
	{
		var listed = Rows.SelectMany(r => r.ClientMethods.Select(m => (m, r.Code))).ToHashSet();

		Operations.Should().NotBeEmpty();
		AssertNone(Operations.Where(o => !listed.Contains(o)).Select(o => $"{o.Name} '{o.Code}'"), "every [Blink1Command] method must appear in the row for its command");
	}

	[Fact]
	public void EveryPublicAsyncMethod_SendsACommand()
		=> AssertNone(
			typeof(Blink1Client).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal) && !m.GetCustomAttributes<Blink1CommandAttribute>().Any())
				.Select(m => m.Name),
			"every public async Blink1Client method must be marked with the command it sends");

	private static void AssertNone(IEnumerable<string> offending, string because)
	{
		var list = offending.ToList();
		list.Should().BeEmpty("{0}; found {1}:{2}{3}", because, list.Count, Environment.NewLine, string.Join(Environment.NewLine, list));
	}

	[GeneratedRegex(@"^\| `(.)` \|[^|]*\|[^|]*\|[^|]*\|([^|]*)\|([^|]*)\|$")]
	private static partial Regex RowRegex();

	private static List<Row> ParseRows()
	{
		var rows = new List<Row>();
		foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot, "docs", "endpoints"), "*.md").Order(StringComparer.Ordinal))
		{
			var lines = File.ReadAllLines(file);
			for (var i = 0; i < lines.Length; i++)
			{
				if (RowRegex().Match(lines[i].TrimEnd()) is { Success: true } match)
				{
					rows.Add(new Row(
						Path.GetFileName(file),
						i + 1,
						Path.GetFileNameWithoutExtension(file),
						match.Groups[1].Value[0],
						SplitCell(match.Groups[2].Value),
						SplitCell(match.Groups[3].Value)));
				}
			}
		}

		return rows;
	}

	private static string[] SplitCell(string cell)
		=> [.. cell.Replace("`", string.Empty, StringComparison.Ordinal).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

	private static HashSet<string> ReadPendingCategories()
		=> [.. File.ReadAllLines(Path.Combine(RepoRoot, "docs", "pending-categories.txt"))
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))];

	private static List<(string Name, char Code)> FindOperations()
		=> [.. typeof(Blink1Client).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.SelectMany(m => m.GetCustomAttributes<Blink1CommandAttribute>().Select(a => ($"Blink1Client.{m.Name}", a.Code)))
			.Distinct()];

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Blink1.Api.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root (Blink1.Api.slnx).");
	}
}
