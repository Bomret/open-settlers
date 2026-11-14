using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// The job looks generator (ADR 0016, INV-0010) writes one registry for each graphics source from the look modules of the
// compilation and fails the build on a look without its constructor and on two looks for one job under one source.
[TestClass]
public sealed class JobLooksGeneratorTests
{
    const string Framework = """
        namespace OpenSettlers.Domain
        {
            public abstract record Job;
            public sealed record Alpha : Job;
            public sealed record Beta : Job;
            public sealed class RuleTables;
        }
        namespace OpenSettlers.MapView
        {
            using OpenSettlers.Domain;
            public abstract class HistoryEditionLook { }
            public abstract class HistoryEditionLook<TJob> : HistoryEditionLook where TJob : Job { }
            public abstract class OpenSettlersLook { }
            public abstract class OpenSettlersLook<TJob> : OpenSettlersLook where TJob : Job
            {
                protected OpenSettlersLook(string key) { }
            }
        }
        """;

    static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    static (ImmutableArray<Diagnostic> Diagnostics, string Text, Compilation Output) Run(params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "MapView",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new JobLooksGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var text = string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        return (diagnostics, text, output);
    }

    static readonly string[] WellFormed =
    [
        Framework,
        """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class BetaLook(RuleTables tables) : HistoryEditionLook<Beta>;
            public sealed class BetaKey() : OpenSettlersLook<Beta>("beta");
            """,
        """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class AlphaLook(RuleTables tables) : HistoryEditionLook<Alpha>;
            public sealed class AlphaKey() : OpenSettlersLook<Alpha>("alpha");
            """,
    ];

    [TestMethod]
    public void WellFormedLooksGiveNoDiagnosticAndRegistriesThatCompile()
    {
        var (diagnostics, text, output) = Run(WellFormed);

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
        StringAssert.Contains(text, "new global::OpenSettlers.MapView.AlphaLook(tables)");
        StringAssert.Contains(text, "new global::OpenSettlers.MapView.AlphaKey()");
    }

    [TestMethod]
    public void EachRegistryHoldsTheLooksOfItsSourceInTheOrderOfTheirFullTypeNames()
    {
        var (_, text, _) = Run(WellFormed);

        Assert.IsTrue(
            text.IndexOf("AlphaLook(tables)", StringComparison.Ordinal)
                < text.IndexOf("BetaLook(tables)", StringComparison.Ordinal)
        );
        Assert.IsTrue(
            text.IndexOf("AlphaKey()", StringComparison.Ordinal) < text.IndexOf("BetaKey()", StringComparison.Ordinal)
        );
        Assert.IsTrue(
            text.IndexOf("BetaLook(tables)", StringComparison.Ordinal)
                < text.IndexOf("AlphaKey()", StringComparison.Ordinal),
            "the History Edition registry comes first"
        );
    }

    [TestMethod]
    public void TheGeneratedTextIsTheSameWhateverTheOrderOfTheSources()
    {
        Assert.AreEqual(Run(WellFormed).Text, Run([.. WellFormed.Reverse()]).Text);
    }

    [TestMethod]
    public void NothingIsWrittenWhereNoLookTypeIsDeclared()
    {
        var (diagnostics, text, _) = Run("namespace Other { public sealed class Plain { } }");

        Assert.AreEqual(0, diagnostics.Length);
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public void AHistoryEditionLookWithoutTheRuleTablesConstructorIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class LoneLook : HistoryEditionLook<Alpha>
            {
                public LoneLook() { }
            }
            """
        );

        var error = diagnostics.Single(d => d.Id == "LOOK001");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "LoneLook");
        Assert.IsFalse(text.Contains("LoneLook"));
    }

    [TestMethod]
    public void AnOpenSettlersLookWithoutTheParameterlessConstructorIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class LoneKey : OpenSettlersLook<Alpha>
            {
                public LoneKey(string key) : base(key) { }
            }
            """
        );

        var error = diagnostics.Single(d => d.Id == "LOOK001");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "LoneKey");
        Assert.IsFalse(text.Contains("LoneKey"));
    }

    [TestMethod]
    public void TwoHistoryEditionLooksForOneJobAreABuildErrorNamingBothClassesAndTheJob()
    {
        var (diagnostics, _, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class FirstLook(RuleTables tables) : HistoryEditionLook<Alpha>;
            public sealed class SecondLook(RuleTables tables) : HistoryEditionLook<Alpha>;
            """
        );

        var error = diagnostics.Single(d => d.Id == "LOOK002");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "FirstLook");
        StringAssert.Contains(error.GetMessage(), "SecondLook");
        StringAssert.Contains(error.GetMessage(), "Alpha");
    }

    [TestMethod]
    public void TwoOpenSettlersLooksForOneJobAreABuildError()
    {
        var (diagnostics, _, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class FirstKey() : OpenSettlersLook<Alpha>("one");
            public sealed class SecondKey() : OpenSettlersLook<Alpha>("two");
            """
        );

        Assert.AreEqual("LOOK002", diagnostics.Single().Id);
    }

    [TestMethod]
    public void AGenericLookOfEitherSourceIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class GenericLook<T>(RuleTables tables) : HistoryEditionLook<Alpha>;
            public sealed class GenericKey<T>() : OpenSettlersLook<Beta>("beta");
            """
        );

        CollectionAssert.AreEqual(new[] { "LOOK003", "LOOK003" }, diagnostics.Select(d => d.Id).ToArray());
        StringAssert.Contains(diagnostics[0].GetMessage(), "GenericLook");
        StringAssert.Contains(diagnostics[1].GetMessage(), "GenericKey");
        Assert.IsFalse(text.Contains("GenericLook<"));
    }

    [TestMethod]
    [DataRow("private")]
    [DataRow("protected")]
    public void ALookNestedAsPrivateOrProtectedIsABuildErrorNamingTheClass(string access)
    {
        var (diagnostics, text, _) = Run(
            Framework,
            $$"""
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public class Holder
            {
                {{access}} sealed class HiddenLook(RuleTables tables) : HistoryEditionLook<Alpha>;
                {{access}} sealed class HiddenKey() : OpenSettlersLook<Beta>("beta");
            }
            """
        );

        CollectionAssert.AreEqual(new[] { "LOOK004", "LOOK004" }, diagnostics.Select(d => d.Id).ToArray());
        StringAssert.Contains(diagnostics[0].GetMessage(), "HiddenLook");
        StringAssert.Contains(diagnostics[1].GetMessage(), "HiddenKey");
        Assert.IsFalse(text.Contains("Hidden"));
    }

    [TestMethod]
    [DataRow("private", "public")]
    [DataRow("private", "internal")]
    [DataRow("protected", "public")]
    public void ALookNestedInAPrivateOrProtectedTypeIsABuildErrorNamingTheClass(string outer, string inner)
    {
        var (diagnostics, text, _) = Run(
            Framework,
            $$"""
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public class Holder
            {
                {{outer}} class Mid
                {
                    {{inner}} sealed class DeepLook(RuleTables tables) : HistoryEditionLook<Alpha>;
                    {{inner}} sealed class DeepKey() : OpenSettlersLook<Beta>("beta");
                }
            }
            """
        );

        CollectionAssert.AreEqual(new[] { "LOOK004", "LOOK004" }, diagnostics.Select(d => d.Id).ToArray());
        StringAssert.Contains(diagnostics[0].GetMessage(), "DeepLook");
        StringAssert.Contains(diagnostics[1].GetMessage(), "DeepKey");
        Assert.IsFalse(text.Contains("Deep"));
    }

    [TestMethod]
    public void APrivateProtectedLookIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public class Holder
            {
                private protected sealed class HiddenLook(RuleTables tables) : HistoryEditionLook<Alpha>;
            }
            """
        );

        StringAssert.Contains(diagnostics.Single(d => d.Id == "LOOK004").GetMessage(), "HiddenLook");
        Assert.IsFalse(text.Contains("HiddenLook"));
    }

    [TestMethod]
    public void OneJobMayHaveALookUnderEachSource()
    {
        var (diagnostics, _, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class AlphaLook(RuleTables tables) : HistoryEditionLook<Alpha>;
            public sealed class AlphaKey() : OpenSettlersLook<Alpha>("alpha");
            """
        );

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
    }

    [TestMethod]
    public void AnAbstractLookIsNoLook()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public abstract class BaseLook : HistoryEditionLook<Alpha>;
            public abstract class BaseKey : OpenSettlersLook<Beta>
            {
                protected BaseKey() : base("beta") { }
            }
            """
        );

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.IsFalse(text.Contains("Base"));
    }

    [TestMethod]
    public void ALookNestedInAGenericTypeIsABuildErrorNamingTheClass()
    {
        var (diagnostics, text, _) = Run(
            Framework,
            """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public class Outer<T>
            {
                public sealed class InnerLook(RuleTables tables) : HistoryEditionLook<Alpha>;
                public sealed class InnerKey() : OpenSettlersLook<Beta>("beta");
            }
            """
        );

        CollectionAssert.AreEqual(new[] { "LOOK003", "LOOK003" }, diagnostics.Select(d => d.Id).ToArray());
        StringAssert.Contains(diagnostics[0].GetMessage(), "InnerLook");
        StringAssert.Contains(diagnostics[1].GetMessage(), "InnerKey");
        Assert.IsFalse(text.Contains("Inner"));
    }
}
