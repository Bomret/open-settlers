using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// A job written only as its own files (ADR 0016) appears in every layer's generated registry with no edit elsewhere. Each layer is
// its own compilation with the shared engine files of that layer stood in for, as the build has them (the Domain, the Application, the
// Adapters, the map view), and the four generators run over each. Test doubles in the test assembly are not found by the domain,
// application, and adapter generators, so the fixture is compiled here.
[TestClass]
public sealed class FixtureJobTests
{
    // What each layer's engine declares, stood in for: the shared files a job module does not touch.
    const string DomainEngine = """
        using System;
        using System.Collections.Generic;
        namespace OpenSettlers.Domain
        {
            public abstract partial record Job
            {
                public abstract int Code { get; }
                public virtual IReadOnlyList<Type> Data => [];
            }
            public sealed record UnportedJob(int Code) : Job { public override int Code { get; } = Code; }
            public sealed class RuleTables;
        }
        """;

    const string ApplicationEngine = """
        namespace OpenSettlers.Simulation
        {
            using OpenSettlers.Domain;
            public sealed class JobParts;
            public abstract class JobRules { }
            public abstract class JobRules<TJob> : JobRules where TJob : Job
            {
                protected JobRules(JobParts parts) { }
            }
        }
        """;

    const string AdaptersEngine = """
        using System;
        using System.Collections.Generic;
        [assembly: OpenSettlers.Simulation.CoreStateCodes(2, 3)]
        namespace OpenSettlers.Simulation
        {
            using OpenSettlers.Domain;
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
        """;

    const string MapViewEngine = """
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

    // The fixture job, as the files a developer adds, each in the layer it belongs to: its job record and data, its rules, its tables
    // (which read its data) and names, and its two looks.
    const string FixtureDomain = """
        using System;
        using System.Collections.Generic;
        namespace OpenSettlers.Domain;
        public readonly record struct FixtureAdds(ushort Walk);
        public sealed record Fixture : Job
        {
            public const int Number = 12;
            public override int Code => Number;
            public override IReadOnlyList<Type> Data => [typeof(FixtureAdds)];
        }
        """;

    const string FixtureApplication = """
        namespace OpenSettlers.Simulation;
        public sealed class FixtureRules(JobParts parts) : JobRules<OpenSettlers.Domain.Fixture>(parts);
        """;

    const string FixtureAdapters = """
        using System.Collections.Generic;
        using OpenSettlers.Domain;
        namespace OpenSettlers.Simulation;
        [JobName("fixture", "a made-up job")]
        [StateName(0x30, "fixes", "a made-up state")]
        public sealed class FixtureTables : JobTables<Fixture>
        {
            public override void Read(byte[] exe, ICollection<(Job Job, object Data)> data) =>
                data.Add((Job, new FixtureAdds(7)));
        }
        """;

    const string FixtureMapView = """
        namespace OpenSettlers.MapView;
        using OpenSettlers.Domain;
        public sealed class FixtureLook(RuleTables tables) : HistoryEditionLook<Fixture>;
        public sealed class FixtureKey() : OpenSettlersLook<Fixture>("fixture");
        """;

    static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("OpenSettlers.", StringComparison.Ordinal))
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    // One layer: the generated text, the compilation with the generated files, and the diagnostics of the generators.
    sealed record Layer
    {
        public required string Text { get; init; }

        public required Compilation Output { get; init; }

        public required int Diagnostics { get; init; }
    }

    static Layer Run(string name, string engine, string? fixture, params Compilation[] below)
    {
        var compilation = CSharpCompilation.Create(
            name,
            new[] { engine, fixture }
                .OfType<string>()
                .Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))),
            [.. References, .. below.Select(layer => layer.ToMetadataReference())],
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

        return new Layer
        {
            Text = text,
            Output = output,
            Diagnostics = diagnostics.Length,
        };
    }

    // The four layers, each compiled on the Domain's output, with or without the fixture's files.
    static (Layer Domain, Layer Application, Layer Adapters, Layer MapView) Build(bool withFixture)
    {
        var domain = Run("Domain", DomainEngine, withFixture ? FixtureDomain : null);

        return (
            domain,
            Run("Application", ApplicationEngine, withFixture ? FixtureApplication : null, domain.Output),
            Run("Adapters", AdaptersEngine, withFixture ? FixtureAdapters : null, domain.Output),
            Run("MapView", MapViewEngine, withFixture ? FixtureMapView : null, domain.Output)
        );
    }

    [TestMethod]
    public void TheEngineAloneRegistersNoFixtureJob()
    {
        var (domain, application, adapters, mapView) = Build(withFixture: false);

        foreach (var layer in new[] { domain, application, adapters, mapView })
        {
            Assert.IsFalse(layer.Text.Contains("Fixture", StringComparison.Ordinal), layer.Text);
        }
    }

    [TestMethod]
    public void AJobWrittenOnlyAsItsOwnFilesAppearsInEveryLayersRegistry()
    {
        var (domain, application, adapters, mapView) = Build(withFixture: true);

        foreach (var layer in new[] { domain, application, adapters, mapView })
        {
            Assert.AreEqual(0, layer.Diagnostics);
            Assert.AreEqual(
                0,
                layer.Output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
                string.Join("; ", layer.Output.GetDiagnostics())
            );
        }

        StringAssert.Contains(domain.Text, "12 => new global::OpenSettlers.Domain.Fixture()", "the Domain's Job.Of");
        StringAssert.Contains(
            application.Text,
            "new global::OpenSettlers.Simulation.FixtureRules(parts)",
            "the rules registry"
        );
        StringAssert.Contains(
            adapters.Text,
            "new global::OpenSettlers.Simulation.FixtureTables()",
            "the tables registry"
        );
        StringAssert.Contains(
            adapters.Text,
            "(global::OpenSettlers.Domain.Fixture.Number, \"fixture\", \"a made-up job\")",
            "the name table's job names"
        );
        StringAssert.Contains(adapters.Text, "(48, \"fixes\", \"a made-up state\")", "the name table's states");
        StringAssert.Contains(
            mapView.Text,
            "new global::OpenSettlers.MapView.FixtureLook(tables)",
            "the History Edition looks"
        );
        StringAssert.Contains(mapView.Text, "new global::OpenSettlers.MapView.FixtureKey()", "the content set's looks");
    }

    [TestMethod]
    public void EachRegistryLandsOnlyInItsOwnLayer()
    {
        var (domain, application, adapters, mapView) = Build(withFixture: true);

        Assert.IsFalse(domain.Text.Contains("FixtureRules", StringComparison.Ordinal));
        Assert.IsFalse(domain.Text.Contains("FixtureTables", StringComparison.Ordinal));
        Assert.IsFalse(application.Text.Contains("FixtureTables", StringComparison.Ordinal));
        Assert.IsFalse(application.Text.Contains("FixtureLook", StringComparison.Ordinal));
        Assert.IsFalse(adapters.Text.Contains("FixtureRules", StringComparison.Ordinal));
        Assert.IsFalse(mapView.Text.Contains("FixtureTables", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheFixtureJobsRecordIsFoundByNumberAndDeclaresItsData()
    {
        var (domain, _, _, _) = Build(withFixture: true);
        using var image = new MemoryStream();
        Assert.IsTrue(domain.Output.Emit(image).Success);

        var assembly = Assembly.Load(image.ToArray());
        var job = assembly.GetType("OpenSettlers.Domain.Job")!.GetMethod("Of")!.Invoke(null, [12])!;
        var data = (IReadOnlyList<Type>)job.GetType().GetProperty("Data")!.GetValue(job)!;

        Assert.AreEqual("Fixture", job.GetType().Name);
        Assert.AreEqual("FixtureAdds", data.Single().Name);
    }
}
