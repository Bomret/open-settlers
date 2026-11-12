using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// The job lookup generator (ADR 0016) writes Job.Of from the sealed job types it finds and fails the build on a malformed job.
[TestClass]
public sealed class JobGeneratorTests
{
    private const string Domain = """
        namespace OpenSettlers.Domain;
        public abstract partial record Job { public abstract int Code { get; } }
        public sealed record UnportedJob(int Code) : Job { public override int Code { get; } = Code; }
        """;

    private static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    private static (ImmutableArray<Diagnostic> Diagnostics, string Text, Compilation Output) Run(
        params string[] sources
    )
    {
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source)).ToArray();
        var compilation = CSharpCompilation.Create(
            "Domain",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new JobLookupGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var text = string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        return (diagnostics, text, output);
    }

    private static string JobSource(string declaration, string name, string number) =>
        $$"""
            namespace OpenSettlers.Domain;
            {{declaration}} {{name}} : Job { {{number}} public override int Code => Number; }
            """;

    private static readonly string[] WellFormed =
    [
        Domain,
        JobSource("public sealed record", "Alpha", "public const int Number = 1;"),
        JobSource("public sealed record", "Beta", "public const int Number = 2;"),
        """
            namespace OpenSettlers.Domain;
            public sealed record Range(int Level) : Job
            {
                public const int Number = 10;
                public const int LastNumber = 12;
                public override int Code => Number + Level;
            }
            """,
    ];

    [TestMethod]
    public void AWellFormedSetOfJobsGivesNoDiagnosticAndAnOfThatCompiles()
    {
        var (diagnostics, text, output) = Run(WellFormed);

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
        StringAssert.Contains(text, "Domain.Alpha()");
        StringAssert.Contains(text, "Domain.Range(code - 10)");
        StringAssert.Contains(text, "new UnportedJob(code)");
    }

    [TestMethod]
    public void TheGeneratedTextIsTheSameOnEveryRunWhateverTheOrderOfTheSources()
    {
        var first = Run(WellFormed).Text;
        var second = Run(WellFormed).Text;
        var reversed = Run(WellFormed.Reverse().ToArray()).Text;

        Assert.AreEqual(first, second);
        Assert.AreEqual(first, reversed);
    }

    [TestMethod]
    public void AJobTypeThatIsNotSealedIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(Domain, JobSource("public record", "Loose", "public const int Number = 1;"));

        var error = diagnostics.Single(d => d.Id == "JOB001");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "Loose");
    }

    [TestMethod]
    public void TwoJobsWithOneNumberAreABuildErrorNamingBothClasses()
    {
        var (diagnostics, _, _) = Run(
            Domain,
            JobSource("public sealed record", "Alpha", "public const int Number = 4;"),
            JobSource("public sealed record", "Beta", "public const int Number = 4;")
        );

        var error = diagnostics.Single(d => d.Id == "JOB002");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "Alpha");
        StringAssert.Contains(error.GetMessage(), "Beta");
    }

    [TestMethod]
    public void ARangeThatOverlapsAnotherJobsNumberIsABuildError()
    {
        var (diagnostics, _, _) = Run(
            Domain,
            JobSource("public sealed record", "Alpha", "public const int Number = 11;"),
            WellFormed[3]
        );

        Assert.AreEqual(1, diagnostics.Count(d => d.Id == "JOB002"));
    }

    [TestMethod]
    public void AnOverlapNamesANumberTheTwoJobsShare()
    {
        var (diagnostics, _, _) = Run(
            Domain,
            JobSource("public sealed record", "Alpha", "public const int Number = 11;"),
            WellFormed[3]
        );

        StringAssert.Contains(diagnostics.Single(d => d.Id == "JOB002").GetMessage(), "number 11 ");
    }

    [TestMethod]
    public void AJobSplitOverPartsThatBothNameJobIsOneJob()
    {
        var (diagnostics, text, _) = Run(
            Domain,
            """
            namespace OpenSettlers.Domain;
            public sealed partial record Split : Job { public const int Number = 9; public override int Code => Number; }
            public sealed partial record Split : Job;
            """
        );

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        StringAssert.Contains(text, "9 => new global::OpenSettlers.Domain.Split()");
    }

    [TestMethod]
    public void TheGeneratedIsPortedIsNotPublicApi()
    {
        var (_, text, _) = Run(WellFormed);

        StringAssert.Contains(text, "internal static bool IsPorted");
        Assert.IsFalse(text.Contains("public static bool IsPorted"));
    }

    [TestMethod]
    public void ALastNumberThatIsNotAConstantIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(
            Domain,
            """
            namespace OpenSettlers.Domain;
            public sealed record Fickle(int Level) : Job
            {
                public const int Number = 3;
                public static readonly int LastNumber = 4;
                public override int Code => Number + Level;
            }
            """
        );

        var error = diagnostics.Single(d => d.Id == "JOB003");
        StringAssert.Contains(error.GetMessage(), "Fickle");
        StringAssert.Contains(error.GetMessage(), "'public const int LastNumber'");
    }

    [TestMethod]
    public void AnUnrelatedChangeToTheCompilationRerunsNoOutput()
    {
        var compilation = CSharpCompilation.Create(
            "Domain",
            WellFormed.Select(source => CSharpSyntaxTree.ParseText(source)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new JobLookupGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true)
        );
        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(
            compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Other; public sealed class Unrelated;"))
        );

        var reasons = driver
            .GetRunResult()
            .Results.Single()
            .TrackedOutputSteps.Values.SelectMany(steps => steps)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();
        Assert.IsTrue(reasons.Length > 0);
        CollectionAssert.AreEqual(
            Enumerable.Repeat(IncrementalStepRunReason.Unchanged, reasons.Length).ToArray(),
            reasons
                .Select(reason =>
                    reason == IncrementalStepRunReason.Cached ? IncrementalStepRunReason.Unchanged : reason
                )
                .ToArray()
        );
    }

    [TestMethod]
    public void ANumberThatIsNotAConstantIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(
            Domain,
            JobSource("public sealed record", "Fickle", "public static readonly int Number = 3;")
        );

        var error = diagnostics.Single(d => d.Id == "JOB003");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "Fickle");
        StringAssert.Contains(error.GetMessage(), "'public const int Number'");
    }

    [TestMethod]
    public void ACompilationThatDoesNotDeclareJobGetsNoOutput()
    {
        var (diagnostics, text, _) = Run("namespace Other; public sealed record Alpha;");

        Assert.AreEqual(0, diagnostics.Length);
        Assert.AreEqual("", text);
    }
}
