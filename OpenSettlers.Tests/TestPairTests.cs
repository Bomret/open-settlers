using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// INV-0012 and INV-0013 (ADR 0019): the layout of the libraries and their test projects under src/. The cases build a
// small src/ tree in a temporary folder, so each rule is pinned by a tree that breaks it; one case reads the repository.
[TestClass]
public sealed class TestPairTests
{
    // A src/ tree in a temporary folder: one folder per project, with the folders it references and the assemblies it
    // grants its internals to.
    sealed class Tree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "test-pairs-" + Guid.NewGuid().ToString("N"));

        // `file` names the project file (default OpenSettlers.<name>.csproj), `assembly` sets <AssemblyName>, and
        // `extra` is raw project XML.
        public Tree Project(
            string name,
            string[]? references = null,
            string[]? grants = null,
            string? file = null,
            string? assembly = null,
            string extra = ""
        )
        {
            var folder = Path.Combine(Root, "src", name);
            Directory.CreateDirectory(folder);
            var items = string.Concat(
                (references ?? []).Select(reference =>
                    $"<ProjectReference Include=\"../{reference}/OpenSettlers.{reference}.csproj\" />"
                )
            );
            var visible = string.Concat((grants ?? []).Select(grant => $"<InternalsVisibleTo Include=\"{grant}\" />"));
            var properties = assembly is null ? "" : $"<PropertyGroup><AssemblyName>{assembly}</AssemblyName></PropertyGroup>";
            File.WriteAllText(
                Path.Combine(folder, file ?? $"OpenSettlers.{name}.csproj"),
                $"<Project Sdk=\"Microsoft.NET.Sdk\">{properties}<ItemGroup>{visible}</ItemGroup><ItemGroup>{items}</ItemGroup>{extra}</Project>"
            );

            return this;
        }

        public Tree Source(string name, string text)
        {
            File.WriteAllText(Path.Combine(Root, "src", name, "Grant.cs"), text);

            return this;
        }

        public void Dispose() => Directory.Delete(Root, true);
    }

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.slnx not found above the test output");
    }

    [TestMethod]
    public void ALibraryWithATestProjectBesideItIsNoProblem()
    {
        using var tree = new Tree()
            .Project("Domain", grants: ["OpenSettlers.Domain.Tests"])
            .Project("Domain.Tests", ["Domain"], assembly: "OpenSettlers.Domain.Tests");

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ALibraryWithNoTestProjectBesideItIsAProblem()
    {
        using var tree = new Tree().Project("Domain");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain");
        StringAssert.Contains(problems[0], "Domain.Tests");
    }

    [TestMethod]
    public void ATestProjectInAnotherFolderIsNoPairOfTheLibrary()
    {
        using var tree = new Tree().Project("Domain").Project("Elsewhere/Domain.Tests", ["Domain"], file: "OpenSettlers.Domain.Tests.csproj");

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(problems.Any(problem => problem.Contains("Domain.Tests")), string.Join("\n", problems));
    }

    // Domain <- Import <- Launch <- Assets, each with its test project and helper project, and the shared helpers.
    static Tree Chain() =>
        new Tree()
            .Project("Testing")
            .Project("Domain", grants: ["OpenSettlers.Domain.Tests"])
            .Project("Domain.Testing", ["Domain"])
            .Project("Domain.Tests", ["Domain", "Testing", "Domain.Testing"], assembly: "OpenSettlers.Domain.Tests")
            .Project("Import", ["Domain"], ["OpenSettlers.Import.Tests"])
            .Project("Import.Testing", ["Import", "Domain.Testing"])
            .Project(
                "Import.Tests",
                ["Import", "Domain", "Testing", "Import.Testing", "Domain.Testing"],
                assembly: "OpenSettlers.Import.Tests"
            )
            .Project("Launch", ["Import"], ["OpenSettlers.Launch.Tests"])
            .Project(
                "Launch.Tests",
                ["Launch", "Import", "Domain", "Testing", "Import.Testing"],
                assembly: "OpenSettlers.Launch.Tests"
            );

    [TestMethod]
    public void ATestProjectMayReferenceItsLibraryTheLibrariesItUsesThroughAnotherLibraryAndTheirHelpers()
    {
        using var tree = Chain();

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ATestProjectReferencingALibraryOutsideItsReachIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Domain.Testing", "Import"],
                assembly: "OpenSettlers.Domain.Tests"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain.Tests");
        StringAssert.Contains(problems[0], "Import");
    }

    [TestMethod]
    public void ATestProjectReferencingAnotherLibrarysTestProjectOrHelperIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Import.Tests", "Import.Testing"],
                assembly: "OpenSettlers.Domain.Tests"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(problems.Any(problem => problem.Contains("Import.Tests")), string.Join("\n", problems));
        Assert.IsTrue(problems.Any(problem => problem.Contains("Import.Testing")), string.Join("\n", problems));
        Assert.IsTrue(problems.All(problem => problem.Contains("Domain.Tests")), string.Join("\n", problems));
    }

    [TestMethod]
    public void AHelperProjectReferencingALibraryOutsideItsReachIsAProblem()
    {
        using var tree = Chain().Project("Domain.Testing", ["Domain", "Import"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.Contains("Domain.Testing") && problem.Contains("Import")),
            string.Join("\n", problems)
        );
    }

    // A test project reaches what its helper references, so a helper that leaves its library's reach breaks the pair
    // that uses it as well.
    [TestMethod]
    public void ATestProjectReachingAProjectOutsideItsReachThroughAHelperIsAProblem()
    {
        using var tree = Chain().Project("Domain.Testing", ["Domain", "Import"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.Contains("Domain.Tests") && problem.Contains("Import")),
            string.Join("\n", problems)
        );
    }

    [TestMethod]
    public void ATestProjectReferencingAProjectOutsideSrcIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Domain.Testing"],
                assembly: "OpenSettlers.Domain.Tests",
                extra: "<ItemGroup><ProjectReference Include=\"../../Host/Host.csproj\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Host");
    }

    [TestMethod]
    public void TheSharedHelpersReferencingALibraryIsAProblem()
    {
        using var tree = Chain().Project("Testing", ["Domain"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Testing");
        StringAssert.Contains(problems[0], "Domain");
    }

    [TestMethod]
    public void TheSharedHelpersReferencingALibraryThroughAHelperProjectIsAProblem()
    {
        using var tree = Chain().Project("Testing", ["Domain.Testing"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.StartsWith("Testing ") && problem.Contains("Domain")),
            string.Join("\n", problems)
        );
    }

    [TestMethod]
    public void TheRepositoryHoldsTheLayout()
    {
        var problems = TestPairs.Problems(RepositoryRoot());

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
