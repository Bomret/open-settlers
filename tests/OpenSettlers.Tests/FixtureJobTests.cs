using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// A job written only as its own files (ADR 0016) appears in every layer's generated registry with no edit elsewhere: the four
// generators run over one compilation that holds the shared engine stand-ins and, in the second run, the fixture job's files.
// Test doubles in the test assembly are not found by the generators, so the fixture is compiled here.
[TestClass]
public sealed class FixtureJobTests
{
    // What the engine's layers declare, stood in for: the shared files a job module does not touch.
    private const string Engine = """
        using System;
        using System.Collections.Generic;
        [assembly: OpenSettlers.Simulation.CoreStateCodes(2, 3)]
        namespace OpenSettlers.Domain
        {
            public abstract partial record Job { public abstract int Code { get; } }
            public sealed record UnportedJob(int Code) : Job { public override int Code { get; } = Code; }
            public sealed class RuleTables;
        }
        namespace OpenSettlers.Simulation
        {
            using OpenSettlers.Domain;
            public sealed class JobParts;
            public abstract class JobRules { }
            public abstract class JobRules<TJob> : JobRules where TJob : Job
            {
                protected JobRules(JobParts parts) { }
            }
            internal interface IJobTables
            {
                Job Job { get; }
                void Read(byte[] exe, ICollection<(Job Job, object Data)> data);
            }
            public abstract class JobTables<TJob> : IJobTables where TJob : Job, new()
            {
                public Job Job { get; } = new TJob();
                public virtual void Read(byte[] exe, ICollection<(Job Job, object Data)> data) { }
            }
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class JobNameAttribute(string name, string evidence) : Attribute { }
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            public sealed class StateNameAttribute(int code, string name, string evidence) : Attribute { }
            [AttributeUsage(AttributeTargets.Assembly)]
            public sealed class CoreStateCodesAttribute(params int[] codes) : Attribute { }
        }
        namespace OpenSettlers.MapView
        {
            using OpenSettlers.Domain;
            public abstract class HistoryEditionLook { }
            public abstract class HistoryEditionLook<TJob> : HistoryEditionLook where TJob : Job
            {
                protected HistoryEditionLook(RuleTables tables) { }
            }
            public abstract class OpenSettlersLook { }
            public abstract class OpenSettlersLook<TJob> : OpenSettlersLook where TJob : Job
            {
                protected OpenSettlersLook(string key) { }
            }
        }
        """;

    // The fixture job, as the files a developer adds: its job record, its rules, its tables and names, and its two looks.
    private static readonly string[] Fixture =
    [
        """
            namespace OpenSettlers.Domain;
            public sealed record Fixture : Job { public const int Number = 12; public override int Code => Number; }
            """,
        """
            namespace OpenSettlers.Simulation;
            public sealed class FixtureRules(JobParts parts) : JobRules<OpenSettlers.Domain.Fixture>(parts);
            """,
        """
            namespace OpenSettlers.Simulation;
            [JobName("fixture", "a made-up job")]
            [StateName(0x30, "fixes", "a made-up state")]
            public sealed class FixtureTables : JobTables<OpenSettlers.Domain.Fixture>;
            """,
        """
            namespace OpenSettlers.MapView;
            using OpenSettlers.Domain;
            public sealed class FixtureLook(RuleTables tables) : HistoryEditionLook<Fixture>(tables);
            public sealed class FixtureKey() : OpenSettlersLook<Fixture>("fixture");
            """,
    ];

    private static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    private static (string Text, Compilation Output, int Diagnostics) Run(params string[] files)
    {
        var compilation = CSharpCompilation.Create(
            "Fixture",
            files
                .Prepend(Engine)
                .Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new JobLookupGenerator(),
            new JobRulesGenerator(),
            new JobTablesGenerator(),
            new JobLooksGenerator()
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var text = string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        return (text, output, diagnostics.Length);
    }

    [TestMethod]
    public void TheEngineAloneRegistersNoFixtureJob()
    {
        var (text, _, _) = Run();

        Assert.IsFalse(text.Contains("Fixture", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void AJobWrittenOnlyAsItsOwnFilesAppearsInEveryLayersRegistry()
    {
        var (text, output, diagnostics) = Run(Fixture);

        Assert.AreEqual(0, diagnostics);
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
        StringAssert.Contains(text, "Fixture.Number", "the Domain's Job.Of");
        StringAssert.Contains(text, "new global::OpenSettlers.Simulation.FixtureRules(parts)", "the rules registry");
        StringAssert.Contains(text, "new global::OpenSettlers.Simulation.FixtureTables()", "the tables registry");
        StringAssert.Contains(
            text,
            "(global::OpenSettlers.Domain.Fixture.Number, \"fixture\", \"a made-up job\")",
            "the name table's job names"
        );
        StringAssert.Contains(text, "(48, \"fixes\", \"a made-up state\")", "the name table's states");
        StringAssert.Contains(
            text,
            "new global::OpenSettlers.MapView.FixtureLook(tables)",
            "the History Edition looks"
        );
        StringAssert.Contains(text, "new global::OpenSettlers.MapView.FixtureKey()", "the content set's looks");
    }
}
