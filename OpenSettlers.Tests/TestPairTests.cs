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
            foreach (var earlier in Directory.GetFiles(folder, "*.csproj"))
            {
                File.Delete(earlier);
            }

            var items = string.Concat(
                (references ?? []).Select(reference =>
                    $"<ProjectReference Include=\"../{reference}/OpenSettlers.{reference}.csproj\" />"
                )
            );
            var visible = string.Concat((grants ?? []).Select(grant => $"<InternalsVisibleTo Include=\"{grant}\" />"));
            var properties = assembly is null
                ? ""
                : $"<PropertyGroup><AssemblyName>{assembly}</AssemblyName></PropertyGroup>";
            File.WriteAllText(
                Path.Combine(folder, file ?? $"OpenSettlers.{name}.csproj"),
                $"<Project Sdk=\"Microsoft.NET.Sdk\">{properties}<ItemGroup>{visible}</ItemGroup><ItemGroup>{items}</ItemGroup>{extra}</Project>"
            );

            return this;
        }

        // A source file in the folder src/<name>, which holds a project or not.
        public Tree Source(string name, string text)
        {
            Directory.CreateDirectory(Path.Combine(Root, "src", name));
            File.WriteAllText(Path.Combine(Root, "src", name, "Grant.cs"), Unmask(text));

            return this;
        }

        // A build file at the repository path (`Directory.Build.props`, `src/Directory.Build.targets`, ...).
        public Tree Build(string path, string xml)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, $"<Project>{xml}</Project>");

            return this;
        }

        // A text file at the repository path (`OpenSettlers.Tests/Grant.cs`, ...).
        public Tree Text(string path, string text)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, Unmask(text));

            return this;
        }

        // The attribute names of a grant of internals are written with a tilde in this file, so the source scan of
        // TestPairs finds no grant in the fixtures that hold them as text. The files of a tree carry them whole.
        static string Unmask(string text) => text.Replace("~", "");

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
        using var tree = new Tree()
            .Project("Domain")
            .Project("Elsewhere/Domain.Tests", ["Domain"], file: "OpenSettlers.Domain.Tests.csproj");

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
    public void AGrantToAnAssemblyOtherThanTheLibrarysOwnTestProjectIsAProblem()
    {
        using var tree = Chain().Project("Domain", grants: ["OpenSettlers.Domain.Tests", "OpenSettlers.Tests"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain");
        StringAssert.Contains(problems[0], "OpenSettlers.Tests");
    }

    [TestMethod]
    public void AGrantToAnotherLibrarysTestProjectIsAProblem()
    {
        using var tree = Chain()
            .Project("Import", ["Domain"], ["OpenSettlers.Import.Tests", "OpenSettlers.Domain.Tests"]);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Domain.Tests");
    }

    // The forms of a grant: an item, an assembly attribute item in its two names, and an attribute in a source file in its
    // short and its full name, with or without the suffix.
    [TestMethod]
    [DataRow(
        "<ItemGroup><AssemblyAttribute Include=\"System.Runtime.CompilerServices.InternalsVisibleTo\"><_Parameter1>OpenSettlers.Viewer</_Parameter1></AssemblyAttribute></ItemGroup>"
    )]
    [DataRow(
        "<ItemGroup><AssemblyAttribute Include=\"System.Runtime.CompilerServices.InternalsVisibleToAttribute\"><_Parameter1>OpenSettlers.Viewer</_Parameter1></AssemblyAttribute></ItemGroup>"
    )]
    [DataRow(
        "<ItemGroup><AssemblyAttribute Include=\"InternalsVisibleTo\" _Parameter1=\"OpenSettlers.Viewer\" /></ItemGroup>"
    )]
    [DataRow("<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Viewer\" /></ItemGroup>")]
    [DataRow("<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Viewer, PublicKey=0024\" /></ItemGroup>")]
    public void AGrantInAProjectFileIsAProblemInEveryFormItIsWritten(string xml)
    {
        using var tree = Chain().Project("Domain", grants: ["OpenSettlers.Domain.Tests"], extra: xml);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Viewer");
    }

    [TestMethod]
    [DataRow("[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Viewer\")]")]
    [DataRow("[assembly: System.Runtime.CompilerServices.InternalsVisible~ToAttribute(\"OpenSettlers.Viewer\")]")]
    [DataRow("using System.Runtime.CompilerServices;\n[assembly: InternalsVisible~To(\"OpenSettlers.Viewer\")]")]
    [DataRow(
        "using System.Runtime.CompilerServices;\n[assembly: InternalsVisible~ToAttribute(\"OpenSettlers.Viewer\")]"
    )]
    [DataRow(
        "using System.Runtime.CompilerServices;\n[assembly: InternalsVisible~To(assemblyName: \"OpenSettlers.Viewer\")]"
    )]
    [DataRow("using System.Runtime.CompilerServices;\n[assembly: InternalsVisible~To(@\"OpenSettlers.Viewer\")]")]
    [DataRow(
        "using System.Runtime.CompilerServices;\n[assembly: CLSCompliant(false), InternalsVisible~To(\"OpenSettlers.Viewer, PublicKey=0024\")]"
    )]
    public void AGrantInASourceFileIsAProblemInEveryFormItIsWritten(string text)
    {
        using var tree = Chain().Source("Domain", text);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Viewer");
    }

    [TestMethod]
    [DataRow("[assembly: System.Runtime.CompilerServices.InternalsVisible~To(Names.Viewer)]")]
    [DataRow("[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.\" + \"Viewer\")]")]
    public void AGrantWhoseAssemblyCannotBeReadIsAProblem(string text)
    {
        using var tree = Chain().Source("Domain", text);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain");
    }

    [TestMethod]
    public void AGrantItemWhoseAssemblyIsAPropertyExpressionIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain",
                extra: "<ItemGroup><InternalsVisibleTo Include=\"$(AssemblyName).Tests\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "$(AssemblyName).Tests");
    }

    [TestMethod]
    public void TheOwnTestProjectMayBeGrantedInASourceFileToo()
    {
        using var tree = Chain()
            .Source(
                "Import",
                "[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Import.Tests\")]"
            );

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void AGrantInASourceFileUnderObjOrBinIsNoGrantOfTheProject()
    {
        using var tree = Chain()
            .Source(
                "Domain/obj",
                "[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Viewer\")]"
            );

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ATestProjectOrAHelperProjectGrantingItsInternalsIsAProblem()
    {
        using var tree = Chain()
            .Project("Domain.Testing", ["Domain"], ["OpenSettlers.Domain.Tests"])
            .Source("Import.Tests", "[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"X\")]");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(2, problems);
        Assert.IsTrue(problems.Any(problem => problem.Contains("Domain.Testing")), string.Join("\n", problems));
        Assert.IsTrue(problems.Any(problem => problem.Contains("Import.Tests")), string.Join("\n", problems));
    }

    [TestMethod]
    public void AGrantInASourceFolderOfNoProjectIsAProblem()
    {
        using var tree = Chain()
            .Source(
                "Viewer",
                "[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Tests\")]"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Viewer");
    }

    // Reflection reaches internals with no grant when the runtime skips the access check of an assembly.
    [TestMethod]
    [DataRow("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecks~To(\"OpenSettlers.Domain\")]")]
    [DataRow("[assembly: IgnoresAccessChecks~ToAttribute(\"OpenSettlers.Domain\")]")]
    public void SkippingTheAccessCheckInASourceFileIsAProblem(string text)
    {
        using var tree = Chain().Source("Import.Tests", text);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "IgnoresAccessChecksTo");
    }

    [TestMethod]
    public void SkippingTheAccessCheckThroughAProjectItemIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Import.Tests",
                ["Import", "Domain", "Testing", "Import.Testing", "Domain.Testing"],
                extra: "<ItemGroup><IgnoresAccessChecksTo Include=\"OpenSettlers.Domain\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "IgnoresAccessChecksTo");
    }

    // A build file shared by several projects grants its internals to the assembly it names from each of them.
    [TestMethod]
    [DataRow(
        "Directory.Build.props",
        "<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Domain.Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "Directory.Build.targets",
        "<ItemGroup><InternalsVisibleTo Include=\"$(MSBuildProjectName).Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "src/Directory.Build.props",
        "<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Viewer\" /></ItemGroup>"
    )]
    [DataRow(
        "src/Domain/Directory.Build.targets",
        "<ItemGroup><InternalsVisibleTo Update=\"OpenSettlers.Domain.Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "src/Domain/Grants.props",
        "<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Domain.Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "build/Grants.targets",
        "<ItemGroup><AssemblyAttribute Include=\"System.Runtime.CompilerServices.InternalsVisibleTo\"><_Parameter1>X</_Parameter1></AssemblyAttribute></ItemGroup>"
    )]
    public void AGrantInASharedBuildFileIsAProblem(string path, string xml)
    {
        using var tree = Chain().Build(path, xml);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], Path.GetFileName(path));
    }

    [TestMethod]
    public void ASharedBuildFileWithNoGrantIsNoProblem()
    {
        using var tree = Chain()
            .Build("Directory.Build.props", "<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>")
            .Build("src/Directory.Build.targets", "<Target Name=\"Nothing\" />");

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ABuildFileUnderObjOrAHiddenFolderIsNoSharedBuildFile()
    {
        using var tree = Chain()
            .Build("src/Domain/obj/Domain.nuget.g.props", "<ItemGroup><InternalsVisibleTo Include=\"X\" /></ItemGroup>")
            .Build(
                ".superdev/copy/Directory.Build.props",
                "<ItemGroup><InternalsVisibleTo Include=\"X\" /></ItemGroup>"
            );

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ATestProjectTakingAnotherLibrarysTestAssemblyNameIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Launch.Tests",
                ["Launch", "Import", "Domain", "Testing", "Import.Testing"],
                assembly: "OpenSettlers.Import.Tests"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.Contains("Launch.Tests") && problem.Contains("OpenSettlers.Import.Tests")),
            string.Join("\n", problems)
        );
    }

    [TestMethod]
    public void ATestProjectWhoseAssemblyNameFollowsFromItsProjectFileIsCheckedToo()
    {
        using var tree = Chain()
            .Project(
                "Launch.Tests",
                ["Launch", "Import", "Domain", "Testing", "Import.Testing"],
                file: "Import.Tests.csproj"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.Contains("Launch.Tests") && problem.Contains("Import.Tests")),
            string.Join("\n", problems)
        );
    }

    // The project file may be called anything: the folder says what the project is, and <AssemblyName> what it builds.
    [TestMethod]
    public void AProjectFileWithAnyNameIsStillFound()
    {
        using var tree = Chain()
            .Project("Launch", ["Import"], ["OpenSettlers.Launch.Tests"], file: "Game.csproj")
            .Project(
                "Launch.Tests",
                ["Launch", "Import", "Domain", "Testing", "Import.Testing"],
                file: "Tests.csproj",
                assembly: "OpenSettlers.Launch.Tests"
            );

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    public void ALibraryWithAnyProjectFileNameNeedsItsTestProject()
    {
        using var tree = Chain().Project("Assets", ["Launch"], file: "Assets.csproj");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Assets.Tests");
    }

    [TestMethod]
    public void ATestProjectWithAnyProjectFileNameIsCheckedForItsReferences()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Import"],
                file: "Tests.csproj",
                assembly: "OpenSettlers.Domain.Tests"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Import");
    }

    [TestMethod]
    public void AGrantInAProjectFileWithAnyNameIsAProblem()
    {
        using var tree = Chain()
            .Project("Domain", grants: ["OpenSettlers.Domain.Tests", "OpenSettlers.Viewer"], file: "Core.csproj");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Viewer");
    }

    // The assembly a library grants its internals to is its test project's alone: no other project builds it, whether a
    // helper project, a library, or a project outside src.
    [TestMethod]
    public void AHelperProjectTakingALibrarysTestAssemblyNameIsAProblem()
    {
        using var tree = Chain().Project("Domain.Testing", ["Domain"], assembly: "OpenSettlers.Domain.Tests");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain.Testing");
        StringAssert.Contains(problems[0], "OpenSettlers.Domain.Tests");
    }

    [TestMethod]
    [DataRow("OpenSettlers.csproj")]
    [DataRow("OpenSettlers.Tests/OpenSettlers.Tests.csproj")]
    public void AProjectOutsideSrcTakingALibrarysTestAssemblyNameIsAProblem(string path)
    {
        using var tree = Chain()
            .Build(path, "<PropertyGroup><AssemblyName>OpenSettlers.Domain.Tests</AssemblyName></PropertyGroup>");

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], path);
        StringAssert.Contains(problems[0], "OpenSettlers.Domain.Tests");
    }

    [TestMethod]
    public void AProjectOutsideSrcWithItsOwnAssemblyNameAndNoGrantIsNoProblem()
    {
        using var tree = Chain()
            .Build(
                "OpenSettlers.Tests/OpenSettlers.Tests.csproj",
                "<PropertyGroup><AssemblyName>OpenSettlers.Repository.Tests</AssemblyName></PropertyGroup>"
            );

        CollectionAssert.AreEqual(Array.Empty<string>(), TestPairs.Problems(tree.Root));
    }

    [TestMethod]
    [DataRow(
        "OpenSettlers.csproj",
        "<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Domain.Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "OpenSettlers.Tests/OpenSettlers.Tests.csproj",
        "<ItemGroup><InternalsVisibleTo Include=\"OpenSettlers.Domain.Tests\" /></ItemGroup>"
    )]
    [DataRow(
        "OpenSettlers.Tests/OpenSettlers.Tests.csproj",
        "<ItemGroup><IgnoresAccessChecksTo Include=\"OpenSettlers.Domain\" /></ItemGroup>"
    )]
    public void AGrantInAProjectFileOutsideSrcIsAProblem(string path, string xml)
    {
        using var tree = Chain().Build(path, xml);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], path);
    }

    [TestMethod]
    [DataRow("[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Domain.Tests\")]")]
    [DataRow("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecks~To(\"OpenSettlers.Domain\")]")]
    public void AGrantInASourceFileOutsideSrcIsAProblem(string text)
    {
        using var tree = Chain().Text("OpenSettlers.Tests/Grant.cs", text);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Tests");
    }

    // An alias hides the attribute's name at the place it is used, so the alias itself is the problem.
    [TestMethod]
    [DataRow(
        "using IVT = System.Runtime.CompilerServices.InternalsVisible~ToAttribute;\n[assembly: IVT(\"OpenSettlers.Viewer\")]"
    )]
    [DataRow(
        "using IVT = System.Runtime.CompilerServices.InternalsVisible~To;\n[assembly: IVT(\"OpenSettlers.Viewer\")]"
    )]
    [DataRow(
        "using Skip = System.Runtime.CompilerServices.IgnoresAccessChecks~ToAttribute;\n[assembly: Skip(\"OpenSettlers.Domain\")]"
    )]
    [DataRow(
        "using IVT = global::System.Runtime.CompilerServices.InternalsVisible~ToAttribute;\n[assembly: IVT(\"OpenSettlers.Viewer\")]"
    )]
    public void AGrantThroughAUsingAliasIsAProblem(string text)
    {
        using var tree = Chain().Source("Domain", text);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain");
        StringAssert.Contains(problems[0], "using");
    }

    // MSBuild item types are case-insensitive.
    [TestMethod]
    [DataRow("<ItemGroup><internalsVisibleTo Include=\"OpenSettlers.Viewer\" /></ItemGroup>")]
    [DataRow(
        "<ItemGroup><ASSEMBLYATTRIBUTE Include=\"System.Runtime.CompilerServices.InternalsVisibleTo\" _Parameter1=\"OpenSettlers.Viewer\" /></ItemGroup>"
    )]
    public void AGrantItemWithAnotherCaseIsAProblem(string xml)
    {
        using var tree = Chain().Project("Domain", grants: ["OpenSettlers.Domain.Tests"], extra: xml);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Viewer");
    }

    [TestMethod]
    public void SkippingTheAccessCheckThroughAnItemWithAnotherCaseIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain",
                grants: ["OpenSettlers.Domain.Tests"],
                extra: "<ItemGroup><ignoresAccessChecksTo Include=\"OpenSettlers.Import\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "IgnoresAccessChecksTo");
    }

    // The scan reads the files of OpenSettlers.Tests like any others: a real grant in TestPairs.cs or TestPairTests.cs is
    // a problem too.
    [TestMethod]
    [DataRow("OpenSettlers.Tests/TestPairs.cs")]
    [DataRow("OpenSettlers.Tests/TestPairTests.cs")]
    public void AGrantInTheSourceOfThisScanIsAProblem(string path)
    {
        using var tree = Chain()
            .Text(
                path,
                "[assembly: System.Runtime.CompilerServices.InternalsVisible~To(\"OpenSettlers.Domain.Tests\")]"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "OpenSettlers.Tests");
    }

    // MSBuild element names are case-insensitive, and a project file may declare the old MSBuild namespace.
    [TestMethod]
    public void AProjectReferenceWithAnotherCaseIsReadToo()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Domain.Testing"],
                assembly: "OpenSettlers.Domain.Tests",
                extra: "<ItemGroup><projectReference Include=\"../Import/OpenSettlers.Import.csproj\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Import");
    }

    [TestMethod]
    [DataRow("<ItemGroup><ProjectReference Include=\"../Import/OpenSettlers.Import.csproj\" /></ItemGroup>", "Import")]
    [DataRow(
        "<PropertyGroup><AssemblyName>OpenSettlers.Import.Tests</AssemblyName></PropertyGroup>",
        "OpenSettlers.Import.Tests"
    )]
    public void AProjectFileInTheMSBuildNamespaceIsReadToo(string body, string named)
    {
        using var tree = Chain()
            .Text(
                "src/Domain.Tests/OpenSettlers.Domain.Tests.csproj",
                "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\" Sdk=\"Microsoft.NET.Sdk\">"
                    + "<ItemGroup><ProjectReference Include=\"../Domain/OpenSettlers.Domain.csproj\" />"
                    + "<ProjectReference Include=\"../Testing/OpenSettlers.Testing.csproj\" /></ItemGroup>"
                    + body
                    + "</Project>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(problems.Any(problem => problem.Contains(named)), string.Join("\n", problems));
    }

    // An AssemblyName in a build file applies to every project below it, so no build file may set one.
    [TestMethod]
    [DataRow("src/Domain.Testing/Directory.Build.props", "OpenSettlers.Domain.Tests")]
    [DataRow("Directory.Build.props", "OpenSettlers.Anything")]
    [DataRow("src/Directory.Build.targets", "OpenSettlers.Anything")]
    public void AnAssemblyNameInASharedBuildFileIsAProblem(string path, string name)
    {
        using var tree = Chain().Build(path, $"<PropertyGroup><AssemblyName>{name}</AssemblyName></PropertyGroup>");

        var problems = TestPairs.Problems(tree.Root);

        Assert.IsTrue(
            problems.Any(problem => problem.Contains(Path.GetFileName(path)) && problem.Contains("AssemblyName")),
            string.Join("\n", problems)
        );
    }

    [TestMethod]
    public void AnAssemblyAttributeItemSkippingTheAccessCheckIsAProblem()
    {
        using var tree = Chain()
            .Project(
                "Domain",
                grants: ["OpenSettlers.Domain.Tests"],
                extra: "<ItemGroup><AssemblyAttribute Include=\"System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute\"><_Parameter1>OpenSettlers.Import</_Parameter1></AssemblyAttribute></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "IgnoresAccessChecksTo");
    }

    [TestMethod]
    [DataRow("Foo.Tests")]
    [DataRow("Foo.Testing")]
    public void ATestOrHelperProjectWithNoLibraryBesideItIsAProblem(string name)
    {
        using var tree = Chain().Project(name, assembly: name == "Foo.Tests" ? "OpenSettlers.Foo.Tests" : null);

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], name);
        StringAssert.Contains(problems[0], "Foo");
    }

    [TestMethod]
    public void OnlyTheTestingFolderOfSrcIsTheSharedHelpers()
    {
        using var tree = Chain()
            .Project(
                "Domain.Tests",
                ["Domain", "Testing", "Domain.Testing"],
                assembly: "OpenSettlers.Domain.Tests",
                extra: "<ItemGroup><ProjectReference Include=\"../../Host/Testing/Testing.csproj\" /></ItemGroup>"
            );

        var problems = TestPairs.Problems(tree.Root);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "Domain.Tests");
        StringAssert.Contains(problems[0], "Testing");
    }

    [TestMethod]
    public void TheRepositoryHoldsTheLayout()
    {
        var problems = TestPairs.Problems(RepositoryRoot());

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
