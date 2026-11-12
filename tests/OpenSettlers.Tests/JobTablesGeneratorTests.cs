using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Generators;

namespace OpenSettlers.Tests;

// The job tables generator (ADR 0016) writes the registry of the tables modules and fails the build on a malformed module.
[TestClass]
public sealed class JobTablesGeneratorTests
{
    private const string Base = """
        using System;
        using System.Collections.Generic;
        namespace OpenSettlers.Domain
        {
            public abstract partial record Job { public abstract int Code { get; } }
            public sealed record Alpha : Job { public const int Number = 1; public override int Code => Number; }
            public sealed record Beta : Job { public const int Number = 2; public override int Code => Number; }
        }
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

    // The assembly that holds the core states 2 and 3 of the made-up name table.
    private const string Core = "[assembly: OpenSettlers.Simulation.CoreStateCodes(2, 3)]";

    private static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    private static (ImmutableArray<Diagnostic> Diagnostics, string Text, Compilation Output) Run(
        params string[] modules
    ) => Run(modules.Prepend(Base).Prepend(Core).ToArray(), References);

    private static (ImmutableArray<Diagnostic> Diagnostics, string Text, Compilation Output) Run(
        string[] sources,
        MetadataReference[] references
    )
    {
        var trees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "Adapters",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new JobTablesGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var text = string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        return (diagnostics, text, output);
    }

    private static string Module(string name, string job, string attributes = "", string body = "") =>
        $$"""
            namespace OpenSettlers.Simulation;
            {{attributes}}
            public sealed class {{name}} : JobTables<OpenSettlers.Domain.{{job}}> { {{body}} }
            """;

    private static readonly string[] WellFormed =
    [
        Module("AlphaTables", "Alpha", "[JobName(\"alpha\", \"why alpha\")][StateName(0x11, \"acts\", \"why acts\")]"),
        Module("BetaTables", "Beta", "[StateName(0x12, \"rests\", \"why rests\")]"),
    ];

    [TestMethod]
    public void WellFormedModulesGiveARegistryThatBuildsEachAndListsTheirNames()
    {
        var (diagnostics, text, output) = Run(WellFormed);

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
        Assert.AreEqual(
            0,
            output.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error),
            string.Join("; ", output.GetDiagnostics())
        );
        StringAssert.Contains(text, "new global::OpenSettlers.Simulation.AlphaTables()");
        StringAssert.Contains(text, "new global::OpenSettlers.Simulation.BetaTables()");
        StringAssert.Contains(text, "(global::OpenSettlers.Domain.Alpha.Number, \"alpha\", \"why alpha\")");
        StringAssert.Contains(text, "(17, \"acts\", \"why acts\")");
        StringAssert.Contains(text, "(18, \"rests\", \"why rests\")");
    }

    [TestMethod]
    public void TheGeneratedTextIsTheSameWhateverTheOrderOfTheSources()
    {
        Assert.AreEqual(Run(WellFormed).Text, Run(WellFormed.Reverse().ToArray()).Text);
    }

    [TestMethod]
    public void AModuleWithoutItsConstructorIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(Module("AlphaTables", "Alpha", body: "private AlphaTables() { }"));

        var error = diagnostics.Single(d => d.Id == "TAB001");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "AlphaTables");
    }

    [TestMethod]
    public void AModuleWhoseConstructorTakesArgumentsIsABuildErrorToo()
    {
        var (diagnostics, _, _) = Run(Module("AlphaTables", "Alpha", body: "public AlphaTables(int arg) { }"));

        StringAssert.Contains(diagnostics.Single(d => d.Id == "TAB001").GetMessage(), "AlphaTables");
    }

    [TestMethod]
    public void ASecondModuleForOneJobIsABuildErrorNamingBothClasses()
    {
        var (diagnostics, _, _) = Run(Module("FirstTables", "Alpha"), Module("SecondTables", "Alpha"));

        var error = diagnostics.Single(d => d.Id == "TAB002");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "FirstTables");
        StringAssert.Contains(error.GetMessage(), "SecondTables");
    }

    [TestMethod]
    public void TwoModulesThatNameOneJobAreABuildErrorNamingBothClasses()
    {
        var (diagnostics, _, _) = Run(
            Module("AlphaTables", "Alpha", "[JobName(\"same\", \"one\")]"),
            Module("BetaTables", "Beta", "[JobName(\"same\", \"two\")]")
        );

        var error = diagnostics.Single(d => d.Id == "TAB003");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "AlphaTables");
        StringAssert.Contains(error.GetMessage(), "BetaTables");
        StringAssert.Contains(error.GetMessage(), "same");
    }

    [TestMethod]
    public void TwoModulesThatNameOneStateAreABuildErrorNamingBothClasses()
    {
        var (diagnostics, _, _) = Run(
            Module("AlphaTables", "Alpha", "[StateName(5, \"one\", \"e\")]"),
            Module("BetaTables", "Beta", "[StateName(5, \"two\", \"e\")]")
        );

        var error = diagnostics.Single(d => d.Id == "TAB004");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "AlphaTables");
        StringAssert.Contains(error.GetMessage(), "BetaTables");
    }

    [TestMethod]
    public void AModuleThatNamesAStateOfTheCoreIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(Module("AlphaTables", "Alpha", "[StateName(2, \"walks\", \"e\")]"));

        var error = diagnostics.Single(d => d.Id == "TAB006");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "AlphaTables");
    }

    [TestMethod]
    public void AnAssemblyAttributeOfTheSameNameInAnotherNamespaceNamesNoStateOfTheCore()
    {
        var (diagnostics, _, _) = Run(
            """
            [assembly: Other.CoreStateCodes(5)]
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class CoreStateCodesAttribute(params int[] codes) : System.Attribute { }
            }
            """,
            Module("AlphaTables", "Alpha", "[StateName(5, \"walks\", \"e\")]")
        );

        Assert.AreEqual(0, diagnostics.Length, string.Join("; ", diagnostics));
    }

    [TestMethod]
    public void AModuleThatNamesOneStateTwiceIsABuildErrorNamingTheClass()
    {
        var (diagnostics, _, _) = Run(
            Module("AlphaTables", "Alpha", "[StateName(9, \"one\", \"e\")][StateName(9, \"two\", \"e\")]")
        );

        var error = diagnostics.Single(d => d.Id == "TAB005");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        StringAssert.Contains(error.GetMessage(), "AlphaTables");
    }

    [TestMethod]
    public void NoRegistryIsWrittenWhereNoModuleIsDeclared()
    {
        var (diagnostics, text, _) = Run();

        Assert.AreEqual(0, diagnostics.Length);
        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    public void NoRegistryIsWrittenWhereTheCompilationDeclaresNoJobTablesInSource()
    {
        var (diagnostics, text, _) = Run([Module("AlphaTables", "Alpha")], References);

        Assert.AreEqual(0, diagnostics.Count(d => d.Id.StartsWith("TAB")));
        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    public void NoRegistryIsWrittenWhereJobTablesComesOnlyFromMetadata()
    {
        var library = CSharpCompilation.Create(
            "Library",
            [CSharpSyntaxTree.ParseText(Base)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        using var stream = new MemoryStream();
        Assert.IsTrue(library.Emit(stream).Success);
        stream.Position = 0;
        MetadataReference[] references = [.. References, MetadataReference.CreateFromStream(stream)];

        var (diagnostics, text, _) = Run([Module("AlphaTables", "Alpha")], references);

        Assert.AreEqual(0, diagnostics.Count(d => d.Id.StartsWith("TAB")));
        Assert.AreEqual(string.Empty, text);
    }
}
