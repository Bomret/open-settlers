using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// INV-0014, ADR 0020: no rule of the domain or the application reads or branches on an original code of a good, a building
// type, or a job. The scan parses each file's syntax, so a rule that reaches a code through a new path is caught without a list of
// the rules, and comments and string literals are told apart from code. Every read of the names below is a violation unless an
// allow-list entry anchors its line. Map object, settler state (activity), owner, and terrain codes are not original codes of the
// catalog; their `.Code` reads are allow-listed by line.
[TestClass]
public sealed class OriginalCodeBoundaryTests
{
    // A read of an original code found in one file.
    sealed class Violation(string file, int line, string rule, string text)
    {
        public string File { get; } = file;

        public string Text { get; } = text;

        public int Line { get; } = line;

        public string Rule { get; } = rule;

        public override string ToString() => $"{File}:{Line}: {Rule}";
    }

    // A declaration site that stays: the file it lies in, the exact line, and why it is no rule's read.
    sealed class Allowed(string fileRegex, string lineRegex, string reason)
    {
        public string FileRegex { get; } = fileRegex;

        public string LineRegex { get; } = lineRegex;

        public string Reason { get; } = reason;
    }

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
            @"^src/Domain/Jobs/\w+\.cs$",
            @"^public const int (Number|LastNumber) = \d+;$",
            "each job type declares the number or the numbers the lookup maps to it; the job lookup generator reads them"
        ),
        new(
            @"^src/Domain/Jobs/UnportedJob\.cs$",
            @"^(public sealed record UnportedJob\(int Raw\) : Job|public int Raw \{ get; \} =|IsPorted\(Raw\) \? throw new ArgumentException\(.*nameof\(Raw\)\) : Raw;|public override int GetHashCode\(\) => Raw;)$",
            "UnportedJob declares, validates, and hashes its own raw field, the one thing it holds"
        ),
        new(
            @"^src/Domain/Jobs/UnportedJob\.cs$",
            @"^public override bool ArrivesHomeGenerically => !\(Raw is 14 or 20 or >= 22\);$",
            "a stand-in for a job with no catalog entry and no stable name, where the number is all there is to go by; it pins the table at 0x437dfb and retires when the job is ported (INV-0014, Statement)"
        ),
        new(
            @"^src/Domain/Terrain\.cs$",
            @"^public static Terrain Higher\(Terrain a, Terrain b\) => a\.Code >= b\.Code \? a : b;$",
            "a terrain code is no original code of the catalog"
        ),
        new(
            @"^src/(Domain/(RuleTables|MapObject|Fnv1a)|Application/(HutWork|StonecutterRules|FarmerRules))\.cs$",
            @"\b(mapObject|sapling|Object)\.Code\b",
            "a map object code is no original code of the catalog"
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

    static readonly CSharpParseOptions ParseOptions = new(documentationMode: DocumentationMode.None);

    // The identifiers that carry a job's number: flagged wherever a token spells them, declaration sites included.
    static readonly HashSet<string> NumberNames = ["Number", "LastNumber", "Raw"];

    static bool IsPlain(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    // The text with every comment blanked, so the lines and the columns stay; string literals are tokens and stay whole.
    static SourceText WithoutComments(SyntaxNode root, string text)
    {
        var chars = text.ToCharArray();
        foreach (var trivia in root.DescendantTrivia().Where(IsPlain))
        {
            for (var i = trivia.Span.Start; i < trivia.Span.End; i++)
            {
                if (chars[i] is not ('\n' or '\r'))
                {
                    chars[i] = ' ';
                }
            }
        }

        return SourceText.From(new string(chars));
    }

    static string? RuleOf(SyntaxToken token)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken))
        {
            return null;
        }

        var name = token.Parent as IdentifierNameSyntax;
        var qualified = name?.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Name == name,
            MemberBindingExpressionSyntax or NameColonSyntax => true,
            _ => false,
        };

        return token.ValueText switch
        {
            "OriginalCode" => "OriginalCode read",
            var n when NumberNames.Contains(n) => "job number read",
            // The lookup through its type, or written without its type as inside Job, through a using static, or as a method group.
            "Of" when name?.Parent is MemberAccessExpressionSyntax { Expression: var owner } && qualified => owner
                .ToString()
                .Split('.')[^1] == "Job"
                ? "Job.Of( read"
                : null,
            "Of" when name is not null && !qualified => "Of( read without its type",
            // A member read of Code: whichever receiver, it is no code of the catalog only where an allow-list entry says so.
            "Code" when qualified => "Code read",
            _ => null,
        };
    }

    static List<Violation> Violations(string file, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, ParseOptions);
        var root = tree.GetRoot();
        var stripped = WithoutComments(root, text);
        var found = new List<Violation>();
        foreach (var token in root.DescendantTokens())
        {
            if (RuleOf(token) is { } rule)
            {
                var line = tree.GetLineSpan(token.Span).StartLinePosition.Line;
                found.Add(new Violation(file, line + 1, rule, stripped.Lines[line].ToString().Trim()));
            }
        }

        return found;
    }

    // Splits the violations into those no allow-list entry anchors and the entries that anchored one.
    static (List<string> Reported, HashSet<Allowed> Used) Classify(
        IEnumerable<Violation> violations,
        Allowed[] allowList
    )
    {
        var reported = new List<string>();
        var used = new HashSet<Allowed>();
        foreach (var v in violations)
        {
            var allowed = allowList.FirstOrDefault(a =>
                Regex.IsMatch(v.File, a.FileRegex) && Regex.IsMatch(v.Text, a.LineRegex)
            );
            if (allowed is null)
            {
                reported.Add(v.ToString());
            }
            else
            {
                used.Add(allowed);
            }
        }

        return (reported, used);
    }

    // A stale allow-list entry would hide a later read at its site.
    static void AssertEveryEntryUsed(Allowed[] allowList, HashSet<Allowed> used) =>
        CollectionAssert.AreEquivalent(allowList, used.ToList(), "an allow-list entry matches no declaration site");

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

    // The same files counted by a walk that shares nothing with HeldFiles.
    static int CountHeld(string root) =>
        new[] { "src/Domain", "src/Application" }.Sum(folder => CountIn(Path.Combine(root, folder)));

    static int CountIn(string directory) =>
        Directory.GetFiles(directory, "*.cs").Length
        + Directory.GetDirectories(directory).Where(sub => Path.GetFileName(sub) is not ("obj" or "bin")).Sum(CountIn);

    static void AssertScannedAll(int scanned, int held)
    {
        Assert.IsGreaterThan(50, held);
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
        var scanned = 0;
        var violations = new List<Violation>();
        foreach (var file in HeldFiles(root))
        {
            scanned++;
            violations.AddRange(
                Violations(Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file))
            );
        }

        AssertScannedAll(scanned, CountHeld(root));
        var (reported, used) = Classify(violations, AllowList);
        Assert.IsEmpty(reported, "a rule reads an original code:\n" + string.Join("\n", reported));
        AssertEveryEntryUsed(AllowList, used);
    }

    [TestMethod]
    public void TheScanFailsWhenItReachesFewerFilesThanTheDomainAndTheApplicationHold()
    {
        var held = CountHeld(RootOf());

        Assert.Throws<AssertFailedException>(() => AssertScannedAll(held - 1, held));
        Assert.Throws<AssertFailedException>(() => AssertScannedAll(0, held));
        Assert.Throws<AssertFailedException>(() => AssertScannedAll(40, 40));
        AssertScannedAll(held, held);
    }

    [TestMethod]
    public void AnAllowListEntryThatMatchesNoLineFails()
    {
        var stale = new Allowed(@"^src/Domain/Nowhere\.cs$", "^never$", "matches nothing");
        var live = new Allowed(@"^src/Domain/Planted\.cs$", @"^var c = Object\.Code;$", "a map object code");
        Allowed[] list = [live, stale];

        var (reported, used) = Classify(Violations("src/Domain/Planted.cs", "var c = Object.Code;"), list);

        Assert.IsEmpty(reported);
        Assert.Throws<AssertFailedException>(() => AssertEveryEntryUsed(list, used));
        AssertEveryEntryUsed([live], used);
    }

    static string[] Reported(string file, string source) => [.. Violations(file, source).Select(v => v.ToString())];

    [TestMethod]
    [DataRow("var c = entry.OriginalCode;", "OriginalCode read")]
    [DataRow("if (OriginalCode == 5) { }", "OriginalCode read")]
    [DataRow("var job = Job.Of(code);", "Job.Of( read")]
    [DataRow("var jobs = codes.Select(Job.Of);", "Job.Of( read")]
    [DataRow("var job = Of(code);", "Of( read without its type")]
    [DataRow("var j = a ?? Of(3);", "Of( read without its type")]
    [DataRow("var j = await Of(3);", "Of( read without its type")]
    [DataRow("var j = x is Of(3);", "Of( read without its type")]
    [DataRow("var j = codes.Select(Of);", "Of( read without its type")]
    [DataRow("Func<int, Job> f = x => Of(x);", "Of( read without its type")]
    [DataRow("var n = Woodcutter.Number;", "job number read")]
    [DataRow("var b = Raw is 14;", "job number read")]
    [DataRow("var n = Knight.LastNumber;", "job number read")]
    [DataRow("var c = entry.Code;", "Code read")]
    [DataRow("var c = entry?.Code;", "Code read")]
    [DataRow("var c = jobs[0].Code;", "Code read")]
    [DataRow("var c = settler.Job.Code;", "Code read")]
    [DataRow("var b = x is { Code: 5 };", "Code read")]
    [DataRow("var s = \"http://x\"; var c = entry.OriginalCode;", "OriginalCode read")]
    public void APlantedReadOfAnOriginalCodeIsReportedWithItsFileName(string source, string rule)
    {
        CollectionAssert.AreEqual(
            new[] { $"src/Application/Planted.cs:1: {rule}" },
            Reported("src/Application/Planted.cs", source)
        );
    }

    [TestMethod]
    public void APlantedReadInAMemberBodyIsReportedAtItsLine()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "src/Domain/Planted.cs:3: Of( read without its type",
                "src/Domain/Planted.cs:4: job number read",
                "src/Domain/Planted.cs:4: job number read",
            },
            Reported(
                "src/Domain/Planted.cs",
                "class P\n{\n    public static Job W => Of(5);\n    bool B => Level < LastNumber - Number;\n}"
            )
        );
    }

    [TestMethod]
    [DataRow("RoadSet.Of(3);")]
    [DataRow("var h = Fnv1a.Of(name);")]
    [DataRow("static int DirectionOf(int node) => 1;")]
    [DataRow("public static RoadSet Of(int direction) => new();")]
    [DataRow("var s = \"Job.Of(5) and entry.Code and OriginalCode\";")]
    [DataRow("// a comment cites entry.Code, OriginalCode, Job.Of(5), and Woodcutter.Number")]
    [DataRow("/// <see cref=\"OriginalCode\"/> is read by the exporters")]
    [DataRow("var x = 1; // OriginalCode")]
    [DataRow("/* entry.Code\n and Job.Of(3) */")]
    public void APlantedNonViolationIsNotReported(string source)
    {
        Assert.IsEmpty(Violations("src/Domain/Planted.cs", source));
    }

    [TestMethod]
    public void ACommentThatSpansLinesKeepsTheLineNumbersOfTheCodeAfterIt()
    {
        CollectionAssert.AreEqual(
            new[] { "src/Domain/Planted.cs:3: OriginalCode read" },
            Reported("src/Domain/Planted.cs", "/* entry.Code\n */\nvar c = OriginalCode;")
        );
    }

    [TestMethod]
    public void ABareJobNumberIsReportedAnywhereAndTheUnportedJobRuleIsTheOneAllowedException()
    {
        const string Source =
            "class U\n{\n    public override bool ArrivesHomeGenerically => !(Raw is 14 or 20 or >= 22);\n}";

        var (inPlace, used) = Classify(Violations("src/Domain/Jobs/UnportedJob.cs", Source), AllowList);
        var (elsewhere, _) = Classify(Violations("src/Domain/Jobs/Planted.cs", Source), AllowList);

        Assert.IsEmpty(inPlace);
        Assert.HasCount(1, used);
        CollectionAssert.AreEqual(new[] { "src/Domain/Jobs/Planted.cs:3: job number read" }, elsewhere);
    }

    [TestMethod]
    public void AnEntryCodeIsReportedWhereAMapObjectCodeIsAllowed()
    {
        var (entry, _) = Classify(Violations("src/Application/HutWork.cs", "var c = entry.Code;"), AllowList);
        var (heap, used) = Classify(Violations("src/Application/HutWork.cs", "var c = heap.Object.Code;"), AllowList);

        CollectionAssert.AreEqual(new[] { "src/Application/HutWork.cs:1: Code read" }, entry);
        Assert.IsEmpty(heap);
        Assert.HasCount(1, used);
    }
}
