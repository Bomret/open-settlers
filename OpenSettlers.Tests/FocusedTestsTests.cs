using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The selection of tools/focused-tests.py, run with --list on a stand-in repository in a temporary folder. No case
// runs dotnet: the listing prints the selection, and a run that selects nothing exits before it starts dotnet.
[TestClass]
public sealed class FocusedTestsTests
{
    string _folder = "";

    [TestInitialize]
    public void Initialize()
    {
        _folder = Path.Combine(Path.GetTempPath(), "focused-tests-" + Guid.NewGuid().ToString("N"));
        Write("src/Domain/Hut.cs", "namespace D;\n// A class HutSite stands beside it.\npublic sealed class Hut { }\n");
        Write("src/Domain/HutSite.cs", "namespace D;\npublic sealed record struct HutSite(int X);\n");
        Write("src/Domain/Mill.cs", "namespace D;\npublic sealed class Mill { }\n");
        Write("tools/play-run.sh", "echo run\n");
        Write("content/open-settlers/manifest.json", "{}\n");
        Write("src/Domain.Tests/Needs.cs", "namespace OpenSettlers.Tests;\nstatic class Needs { }\n");
        Write("src/Domain.Tests/Huts.cs", "namespace OpenSettlers.Tests;\nstatic class Huts { Hut A; }\n");
        Test("HutTests", "new Hut();");
        Test("HutSiteTests", "new HutSite(1);");
        Test("HelperTests", "Huts.A;");
        Test("WrapperTests", "Run(\"tools/play-run.sh\");");
        Test("ContentTests", "Path.Combine(root, \"content\", \"open-settlers\");");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    void Write(string path, string text)
    {
        var full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    void Test(string name, string body, string project = "src/Domain.Tests") =>
        Write(
            $"{project}/{name}.cs",
            $"namespace OpenSettlers.Tests;\n[TestClass]\npublic sealed class {name}\n{{\n    void Case() {{ {body} }}\n}}\n"
        );

    ToolRun Run(params string[] arguments) =>
        PythonTool.Run(_folder, [PythonTool.InRepo("tools", "focused-tests.py"), .. arguments]);

    string[] List(params string[] paths)
    {
        var run = Run(["--list", .. paths]);
        Assert.AreEqual(0, run.Exit, run.Stderr);

        return run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    static string[] Classes(params string[] names) => Array.ConvertAll(names, name => "OpenSettlers.Tests." + name);

    [TestMethod]
    public void AChangedTestFileSelectsItsOwnClass() =>
        CollectionAssert.AreEqual(Classes("HutSiteTests"), List("src/Domain.Tests/HutSiteTests.cs"));

    [TestMethod]
    public void ASourceFileSelectsTheClassesNamingItsTypesAsAWholeWord() =>
        CollectionAssert.AreEqual(Classes("HutSiteTests"), List("src/Domain/HutSite.cs"));

    // Hut.cs names HutSite after the word class in a comment, which declares nothing.
    [TestMethod]
    public void AHelperOfTheTestProjectThatNamesATypeAddsTheClassesUsingTheHelper() =>
        CollectionAssert.AreEqual(Classes("HelperTests", "HutTests"), List("./src/Domain/Hut.cs"));

    [TestMethod]
    public void ADeletedSourceFileStandsForTheTypeNamedAsTheFile()
    {
        File.Delete(Path.Combine(_folder, "src", "Domain", "HutSite.cs"));

        CollectionAssert.AreEqual(Classes("HutSiteTests"), List("src/Domain/HutSite.cs"));
    }

    [TestMethod]
    public void AnotherFileSelectsTheClassesNamingItsPath() =>
        CollectionAssert.AreEqual(Classes("WrapperTests"), List("tools/play-run.sh"));

    [TestMethod]
    public void AFileNoClassNamesSelectsTheClassesNamingItsFolderAsQuotedSegments() =>
        CollectionAssert.AreEqual(Classes("ContentTests"), List("content/open-settlers/manifest.json"));

    [TestMethod]
    [DataRow("mise.toml")]
    [DataRow("Directory.Build.props")]
    [DataRow("src/Domain/OpenSettlers.Domain.csproj")]
    [DataRow("src/Generators/Place.cs")]
    [DataRow("src/Domain.Tests/Needs.cs")]
    public void ASharedFileSelectsTheWholeSuite(string path) =>
        CollectionAssert.AreEqual(new[] { "all" }, List("docs/notes.md", path));

    [TestMethod]
    [DataRow("src/Domain/Mill.cs")]
    [DataRow("./src/Domain/Mill.cs")]
    public void ASourceFileNoTestNamesSelectsTheWholeSuite(string path) =>
        CollectionAssert.AreEqual(new[] { "all" }, List(path));

    [TestMethod]
    public void ASourceFileNoTestNamesSelectsTheWholeSuiteByItsAbsolutePath() =>
        CollectionAssert.AreEqual(new[] { "all" }, List(Path.Combine(_folder, "src", "Domain", "Mill.cs")));

    [TestMethod]
    public void ADeletedSourceFileNoTestNamesSelectsTheWholeSuite()
    {
        File.Delete(Path.Combine(_folder, "src", "Domain", "Mill.cs"));

        CollectionAssert.AreEqual(new[] { "all" }, List("src/Domain/Mill.cs"));
    }

    // The test project does not compile src/Viewer/.
    [TestMethod]
    public void AGodotOnlySourceFileNoTestNamesSelectsNothing() =>
        CollectionAssert.AreEqual(Array.Empty<string>(), List("src/Viewer/Panel.cs"));

    [TestMethod]
    [DataRow("docs/rules/notes.md")]
    [DataRow("NOTES.md")]
    public void AMarkdownFileNoTestNamesSelectsNothingWhereATestNamesItsFolder(string path)
    {
        Write(path, "# Notes\n");
        Test("RulesTests", "Path.Combine(root, \"docs\", \"rules\"); Path.Combine(root, \"docs/\");");

        CollectionAssert.AreEqual(Array.Empty<string>(), List(path));
    }

    [TestMethod]
    public void AMarkdownFileATestNamesSelectsThatTest()
    {
        Test("RulesTests", "Path.Combine(root, \"docs\", \"rules\", \"map-form.md\");");

        CollectionAssert.AreEqual(Classes("RulesTests"), List("docs/rules/map-form.md"));
    }

    [TestMethod]
    [DataRow("layouts/new.jsonc", "layouts")]
    [DataRow("play/new.play", "play")]
    public void ANewFileSelectsTheClassesNamingItsFolder(string path, string folder)
    {
        Test("ReadsEveryFileTests", $"Path.Combine(root, \"{folder}\");");

        CollectionAssert.AreEqual(Classes("ReadsEveryFileTests"), List(path));
    }

    [TestMethod]
    [DataRow]
    [DataRow("--list")]
    public void NoPathRefusesWithAUsageLine(params string[] arguments)
    {
        var run = Run(arguments);

        Assert.AreEqual(2, run.Exit);
        Assert.AreEqual("", run.Stdout);
        StringAssert.StartsWith(run.Stderr, "usage: ");
    }

    // Every test project of the repository takes part in the selection: a *.Tests folder at any depth.
    [TestMethod]
    [DataRow("OpenSettlers.Tests")]
    [DataRow("src/Import.Tests")]
    [DataRow("src/Import/Import.Tests")]
    [DataRow("src/Import/Deep/Import.Tests")]
    public void AChangedTestFileOfAnotherTestProjectSelectsItsClass(string project)
    {
        Test("ElsewhereTests", "new Hut();", project);

        CollectionAssert.AreEqual(Classes("ElsewhereTests"), List($"{project}/ElsewhereTests.cs"));
        CollectionAssert.AreEqual(Classes("ElsewhereTests", "HelperTests", "HutTests"), List("src/Domain/Hut.cs"));
    }

    [TestMethod]
    public void AHelperProjectFileNamingATypeSelectsTheClassesNamingTheHelper()
    {
        Write("src/Testing/OpenSettlers.Testing.csproj", "<Project />");
        Write("src/Testing/Rigs.cs", "namespace OpenSettlers.Testing;\npublic static class Rigs { Hut A; }\n");
        Test("RigTests", "Rigs.A;");

        CollectionAssert.AreEqual(Classes("HelperTests", "HutTests", "RigTests"), List("src/Domain/Hut.cs"));
    }

    [TestMethod]
    public void ClassesWithAProjectNameOnlyTheClassesOfThatProject()
    {
        Test("ElsewhereTests", "new Hut();", "OpenSettlers.Tests");
        var run = Run("--classes", "OpenSettlers.Tests");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("OpenSettlers.Tests.ElsewhereTests\n", run.Stdout);
    }

    // Run with a stand-in dotnet that prints its arguments: a selection runs each test project that holds a selected
    // class, and the whole suite runs the solution. A filter that selects no test of a project is no failure.
    ToolRun RunWithStandInDotnet(params string[] arguments)
    {
        var bin = PythonTool.StandIn(Path.Combine(_folder, "bin"), "dotnet", "echo \"dotnet $*\"\n");

        return PythonTool.RunOnPath(
            _folder,
            [PythonTool.InRepo("tools", "focused-tests.py"), .. arguments],
            [bin, "/bin", "/usr/bin"],
            null
        );
    }

    [TestMethod]
    public void ASelectionRunsEachTestProjectHoldingASelectedClass()
    {
        Test("ElsewhereTests", "new Hut();", "OpenSettlers.Tests");
        var run = RunWithStandInDotnet("src/Domain/HutSite.cs", "OpenSettlers.Tests/ElsewhereTests.cs");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        CollectionAssert.AreEqual(
            new[]
            {
                "dotnet test --project OpenSettlers.Tests --filter "
                    + "(ClassName=OpenSettlers.Tests.ElsewhereTests)&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
                "dotnet test --project src/Domain.Tests --filter "
                    + "(ClassName=OpenSettlers.Tests.HutSiteTests)&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
            },
            run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        );
    }

    // A class may be split across two test projects under one full name, as JobCatalogTests is.
    [TestMethod]
    public void AClassSplitAcrossTwoProjectsSelectsBothHalves()
    {
        Test("SplitTests", "new Hut();", "src/Domain.Tests");
        Test("SplitTests", "new Hut();", "src/Import.Tests");
        var run = RunWithStandInDotnet("src/Domain/Hut.cs");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        var commands = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(
            "dotnet test --project src/Domain.Tests --filter "
                + "(ClassName=OpenSettlers.Tests.HelperTests|ClassName=OpenSettlers.Tests.HutTests|ClassName=OpenSettlers.Tests.SplitTests)"
                + "&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
            commands
        );
        Assert.Contains(
            "dotnet test --project src/Import.Tests --filter "
                + "(ClassName=OpenSettlers.Tests.SplitTests)&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
            commands
        );
        CollectionAssert.AreEqual(Classes("HelperTests", "HutTests", "SplitTests"), List("src/Domain/Hut.cs"));
    }

    [TestMethod]
    public void AChangedTestFileOfASplitClassSelectsTheClassInBothProjects()
    {
        Test("SplitTests", "new Hut();", "src/Domain.Tests");
        Test("SplitTests", "new Hut();", "src/Import.Tests");
        var run = RunWithStandInDotnet("src/Import.Tests/SplitTests.cs");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        CollectionAssert.AreEqual(
            new[]
            {
                "dotnet test --project src/Domain.Tests --filter "
                    + "(ClassName=OpenSettlers.Tests.SplitTests)&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
                "dotnet test --project src/Import.Tests --filter "
                    + "(ClassName=OpenSettlers.Tests.SplitTests)&TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8",
            },
            run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        );
    }

    // A failing project does not stop the later projects: every selected project runs, and the run fails.
    [TestMethod]
    public void AFailingTestProjectDoesNotStopTheLaterOnes()
    {
        Test("SplitTests", "new Hut();", "src/Domain.Tests");
        Test("SplitTests", "new Hut();", "src/Import.Tests");
        var bin = PythonTool.StandIn(Path.Combine(_folder, "bin"), "dotnet", "echo \"dotnet $*\"\nexit 1\n");

        var run = PythonTool.RunOnPath(
            _folder,
            [PythonTool.InRepo("tools", "focused-tests.py"), "src/Import.Tests/SplitTests.cs"],
            [bin, "/bin", "/usr/bin"],
            null
        );

        Assert.AreEqual(1, run.Exit);
        Assert.AreEqual(2, run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [TestMethod]
    public void TheWholeSuiteRunsTheSolution()
    {
        var run = RunWithStandInDotnet("mise.toml");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual(
            "dotnet test --solution OpenSettlers.slnx --filter TestCategory!=Install&TestCategory!=Ghidra --ignore-exit-code 8\n",
            run.Stdout
        );
    }

    // The selector run on this repository knows exactly the test classes of this assembly, by the names the test
    // host matches (ClassName=<Type.FullName>).
    [TestMethod]
    public void TheSelectorKnowsExactlyTheTestClassesOfTheAssembly()
    {
        var run = PythonTool.Run(
            PythonTool.RepoRoot(),
            [PythonTool.InRepo("tools", "focused-tests.py"), "--classes", "OpenSettlers.Tests"]
        );
        Assert.AreEqual(0, run.Exit, run.Stderr);
        var expected = typeof(FocusedTestsTests)
            .Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<TestClassAttribute>() is not null)
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            expected,
            run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray()
        );
    }

    [TestMethod]
    public void AChangeNamedOnlyInCommentsRunsNothingAndPasses()
    {
        Write("docs/notes.md", "# Notes\n");
        Test("CitingTests", "// docs/notes.md\n/* docs/notes.md */");

        var run = Run("docs/notes.md", "src/Viewer/Panel.cs");

        Assert.AreEqual(0, run.Exit, run.Stderr);
        Assert.AreEqual("focused-tests: no unit-test class names a changed path; nothing to run\n", run.Stdout);
    }
}
