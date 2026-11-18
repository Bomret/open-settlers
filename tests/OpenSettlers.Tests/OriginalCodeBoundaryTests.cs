using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// INV-0014, ADR 0020: no rule of the domain or the application reads or branches on an original code of a good, a building
// type, or a job. The scan reads the source text, so a rule that reaches a code through a new path is caught without a list of
// the rules. Map object, settler state (activity), owner, and terrain codes are not original codes of the catalog and stay.
[TestClass]
public sealed class OriginalCodeBoundaryTests
{
    // A read of an original code found in one file.
    sealed record Violation(string File, int Line, string Rule)
    {
        public override string ToString() => $"{File}:{Line}: {Rule}";
    }

    // A declaration site that stays: the file it lies in, the exact line, and why it is no rule's read.
    sealed record Allowed(string FileRegex, string LineRegex, string Reason);

    static readonly Allowed[] AllowList =
    [
        new(
            @"^src/Domain/(Good|Building|Job)Entry\.cs$",
            @"^public int\? OriginalCode \{ get; init; \}$",
            "the catalog entry record declares the field the importer fills and the exporters read"
        ),
        new(
            @"^src/Domain/Job\.cs$",
            @"^public abstract int\? OriginalCode \{ get; \}$",
            "the job type declares the number the original gives it"
        ),
        new(
            @"^src/Domain/Jobs/\w+\.cs$",
            @"^public override int\? OriginalCode => (Number|Raw|Number \+ Level);$",
            "each job type declares its own number; UnportedJob declares its raw field"
        ),
        new(
            @"^src/Domain/Jobs/UnportedJob\.cs$",
            @"^public bool Equals\(UnportedJob\? other\) => other is not null && Raw == other\.Raw;$",
            "UnportedJob compares its own raw field for value equality"
        ),
        new(
            @"^src/Domain/Job\.cs$",
            @"^Name\.Length > 0 \? Name : OriginalCode\?\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) \?\? """";$",
            "ToString shows the number of a job that has no stable name; it is a display, no rule reads it"
        ),
        new(
            @"^src/Domain/ContentCatalog\.cs$",
            @"^if \((good|building|job)\.OriginalCode is \{ \} \w+ && !\w+\.TryAdd\(\w+, (good|building|job)\)\)$",
            "the catalog constructor refuses two entries with one original code; it validates the table, no rule branches on a code"
        ),
    ];

    static readonly (string Rule, Regex Pattern)[] Reads =
    [
        ("OriginalCode read", new Regex(@"\bOriginalCode\b")),
        ("Job.Of( read", new Regex(@"\bJob\.Of\(")),
        // The lookup written without its type prefix, as inside Job or through a using static.
        ("Of( read without its type", new Regex(@"(?<![\w.])(?<!\b(?!return\b)\w+\s)(?<![>\]?]\s)Of\(")),
        ("job number read", new Regex(@"\.(Number|Raw)\b")),
        // A good, building type, or job: the last name before .Code carries the term. Object, Owner, Activity, Terrain stay.
        ("good, building, or job .Code", new Regex(@"\b\w*(?:[Gg]ood|[Bb]uilding|[Jj]ob)\w*\.Code\b")),
    ];

    static string StripComments(string text)
    {
        text = Regex.Replace(
            text,
            @"/\*.*?\*/",
            m => new string('\n', m.Value.Count(c => c == '\n')),
            RegexOptions.Singleline
        );

        return Regex.Replace(text, @"//[^\n]*", "");
    }

    static List<Violation> Violations(string file, string text)
    {
        var found = new List<Violation>();
        var lines = StripComments(text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (rule, pattern) in Reads)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    found.Add(new Violation(file, i + 1, rule));
                }
            }
        }

        return found;
    }

    static string RootOf()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return dir;
    }

    // The files the domain and the application hold, counted from the file system.
    static List<string> HeldFiles(string root) =>
        [
            .. new[] { "src/Domain", "src/Application" }
                .SelectMany(folder =>
                    Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
                )
                .Where(file =>
                {
                    var parts = Path.GetRelativePath(root, file).Split('/', '\\');

                    return !parts.Contains("obj") && !parts.Contains("bin");
                })
                .Order(StringComparer.Ordinal),
        ];

    static void AssertScannedAll(int scanned, int held)
    {
        Assert.IsGreaterThan(0, held);
        Assert.AreEqual(
            held,
            scanned,
            $"the scan reached {scanned} of the {held} files the domain and the application hold"
        );
    }

    [TestMethod]
    public void NoRuleOfTheDomainOrTheApplicationReadsAnOriginalCode()
    {
        var root = RootOf();
        var held = HeldFiles(root);
        var scanned = 0;
        var violations = new List<string>();
        var used = new HashSet<Allowed>();
        foreach (var file in held)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            var lines = StripComments(text).Split('\n');
            scanned++;
            foreach (var v in Violations(relative, text))
            {
                var allowed = AllowList.FirstOrDefault(a =>
                    Regex.IsMatch(relative, a.FileRegex) && Regex.IsMatch(lines[v.Line - 1].Trim(), a.LineRegex)
                );
                if (allowed is null)
                {
                    violations.Add(v.ToString());
                }
                else
                {
                    used.Add(allowed);
                }
            }
        }

        AssertScannedAll(scanned, HeldFiles(root).Count);
        Assert.IsEmpty(violations, "a rule reads an original code:\n" + string.Join("\n", violations));
        // A stale allow-list entry would hide a later read at its site.
        CollectionAssert.AreEquivalent(AllowList, used.ToList(), "an allow-list entry matches no declaration site");
    }

    [TestMethod]
    public void TheScanFailsWhenItReachesFewerFilesThanTheDomainAndTheApplicationHold()
    {
        var held = HeldFiles(RootOf());

        Assert.IsGreaterThan(50, held.Count);
        Assert.Throws<AssertFailedException>(() => AssertScannedAll(held.Count - 1, held.Count));
        Assert.Throws<AssertFailedException>(() => AssertScannedAll(0, held.Count));
        AssertScannedAll(held.Count, held.Count);
    }

    [TestMethod]
    public void EachPlantedReadOfAnOriginalCodeIsReportedWithItsFileName()
    {
        CollectionAssert.AreEqual(
            new[] { "src/Application/Planted.cs:2: OriginalCode read" },
            Violations("src/Application/Planted.cs", "class P\n{ var c = entry.OriginalCode; }")
                .Select(v => v.ToString())
                .ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "src/Domain/Planted.cs:1: Job.Of( read" },
            Violations("src/Domain/Planted.cs", "var job = Job.Of(code);").Select(v => v.ToString()).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "src/Domain/Planted.cs:1: Of( read without its type" },
            Violations("src/Domain/Planted.cs", "var job = Of(code);").Select(v => v.ToString()).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "src/Application/Planted.cs:1: OriginalCode read" },
            Violations("src/Application/Planted.cs", "if (OriginalCode == 5) { }").Select(v => v.ToString()).ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { "src/Application/Planted.cs:1: job number read" },
            Violations("src/Application/Planted.cs", "var n = Woodcutter.Number;").Select(v => v.ToString()).ToArray()
        );
    }

    [TestMethod]
    [DataRow("good.Code")]
    [DataRow("entry.Good.Code")]
    [DataRow("building.Code")]
    [DataRow("site.BuildingType.Code")]
    [DataRow("settler.Job.Code")]
    public void APlantedCodeOnAGoodABuildingTypeOrAJobIsReported(string read)
    {
        CollectionAssert.AreEqual(
            new[] { "src/Application/Planted.cs:1: good, building, or job .Code" },
            Violations("src/Application/Planted.cs", $"var c = {read};").Select(v => v.ToString()).ToArray()
        );
    }

    [TestMethod]
    [DataRow("var c = heap.Object.Code;")]
    [DataRow("if (Map.At(x, y).Object.Code is 3 or 4) { }")]
    [DataRow("var c = settler.Activity.Code;")]
    [DataRow("var c = flag.Owner.Code;")]
    [DataRow("var c = Terrain.Lowland.Code;")]
    [DataRow("RoadSet.Of(3);")]
    [DataRow("static int DirectionOf(int node) => 1;")]
    [DataRow("// a comment cites job.Code, OriginalCode, Job.Of(5), and Woodcutter.Number")]
    [DataRow("/// <see cref=\"OriginalCode\"/> is read by the exporters")]
    [DataRow("/* good.Code\n and Job.Of(3) */")]
    public void APlantedNonViolationIsNotReported(string source)
    {
        Assert.IsEmpty(Violations("src/Domain/Planted.cs", source));
    }

    [TestMethod]
    public void ACommentThatSpansLinesKeepsTheLineNumbersOfTheCodeAfterIt()
    {
        CollectionAssert.AreEqual(
            new[] { "src/Domain/Planted.cs:3: OriginalCode read" },
            Violations("src/Domain/Planted.cs", "/* good.Code\n */\nvar c = OriginalCode;")
                .Select(v => v.ToString())
                .ToArray()
        );
    }
}
