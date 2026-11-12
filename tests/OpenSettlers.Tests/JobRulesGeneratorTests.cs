using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// The job rules generator (ADR 0016) writes the registry of the rules modules and fails the build on a malformed module.
[TestClass]
public sealed class JobRulesGeneratorTests
{
    private const string Framework = """
        namespace OpenSettlers.Domain
        {
            public abstract record Job;
            public sealed record Alpha : Job;
            public sealed record Beta : Job;
        }
        namespace OpenSettlers.Simulation
        {
            public sealed class JobParts;
            public abstract class JobRules { }
            public abstract class JobRules<TJob> : JobRules where TJob : OpenSettlers.Domain.Job
            {
                protected JobRules(JobParts parts) { }
            }
        }
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
        var compilation = CSharpCompilation.Create(
            "Application",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new JobRulesGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var text = string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        return (diagnostics, text, output);
    }

    private static readonly string[] WellFormed =
    [
        Framework,
        """
            namespace OpenSettlers.Simulation;
            public sealed class BetaRules(JobParts parts) : JobRules<OpenSettlers.Domain.Beta>(parts);
            """,
        """
            namespace OpenSettlers.Simulation;
            public sealed class AlphaRules(JobParts parts) : JobRules<OpenSettlers.Domain.Alpha>(parts);
            """,
    ];

    [TestMethod]
    public void AWellFormedSetOfModulesGivesNoDiagnosticAndARegistryThatCompiles()
    {
        var (diagnostics, text, output) = Run(WellFormed);

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
        StringAssert.Contains(text, "new global::OpenSettlers.Simulation.AlphaRules(parts)");
    }

    [TestMethod]
    public void TheRegistryBuildsTheModulesInTheOrderOfTheirFullTypeNames()
    {
        var (_, text, _) = Run(WellFormed);

        Assert.IsTrue(text.IndexOf("AlphaRules(parts)", StringComparison.Ordinal) > 0);
        Assert.IsTrue(
            text.IndexOf("AlphaRules(parts)", StringComparison.Ordinal)
                < text.IndexOf("BetaRules(parts)", StringComparison.Ordinal)
        );
    }

    [TestMethod]
    public void TheGeneratedTextIsTheSameWhateverTheOrderOfTheSources()
    {
        Assert.AreEqual(Run(WellFormed).Text, Run(WellFormed.Reverse().ToArray()).Text);
    }

    [TestMethod]
    public void AModuleWithoutTheJobPartsConstructorIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.Simulation;
            public sealed class LoneRules : JobRules<OpenSettlers.Domain.Alpha>
            {
                public LoneRules() : base(new JobParts()) { }
            }
            """
        );

        var error = diagnostics.Single(d => d.Id == "RUL001");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "LoneRules");
        Assert.IsFalse(text.Contains("LoneRules"));
    }

    [TestMethod]
    public void ASecondModuleForOneJobIsABuildErrorNamingBothClassesAndTheJob()
    {
        var (diagnostics, _, _) = Run(
            Framework,
            """
            namespace OpenSettlers.Simulation;
            public sealed class FirstRules(JobParts parts) : JobRules<OpenSettlers.Domain.Alpha>(parts);
            public sealed class SecondRules(JobParts parts) : JobRules<OpenSettlers.Domain.Alpha>(parts);
            """
        );

        var error = diagnostics.Single(d => d.Id == "RUL002");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "FirstRules");
        StringAssert.Contains(error.GetMessage(), "SecondRules");
        StringAssert.Contains(error.GetMessage(), "Alpha");
    }

    [TestMethod]
    public void AnAbstractModuleIsNoModule()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.Simulation;
            public abstract class BaseRules<T> : JobRules<T> where T : OpenSettlers.Domain.Job
            {
                protected BaseRules() : base(null!) { }
            }
            """
        );

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.IsFalse(text.Contains("BaseRules"));
    }

    [TestMethod]
    public void ACompilationWithNoModulesGetsAnEmptyRegistryThatCompiles()
    {
        var (diagnostics, text, output) = Run(Framework);

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        StringAssert.Contains(text, "JobModules");
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
    }

    [TestMethod]
    public void ACompilationThatDoesNotDeclareJobRulesGetsNoOutput()
    {
        var (diagnostics, text, _) = Run("namespace Other; public sealed class Alpha;");

        Assert.AreEqual(0, diagnostics.Length);
        Assert.AreEqual("", text);
    }
}
