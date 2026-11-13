using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class LayoutLoadTests
{
    [TestMethod]
    public void AMissingLayoutFileExitsFourAndNamesTheFile()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "layout-load-no-such-dir", "none.jsonc");

        var code = LayoutLoad.Run(path, out var layout, out var message);

        Assert.AreEqual(LaunchArgs.BadArgumentExitCode, code);
        Assert.AreEqual(4, code);
        Assert.IsNull(layout);
        StringAssert.StartsWith(message, "ERROR " + path + ": ");
    }

    [TestMethod]
    public void ALayoutThatFailsToLoadExitsFourAndNamesTheFileAndTheEntry()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-load").FullName;
        try
        {
            var bad = System.IO.Path.Combine(folder, "bad.jsonc");
            System.IO.File.WriteAllText(bad, LayoutTests.Text(size: "9"));

            var code = LayoutLoad.Run(bad, out var layout, out var message);

            Assert.AreEqual(4, code);
            Assert.IsNull(layout);
            StringAssert.StartsWith(message, "ERROR " + bad + ": size: ");
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void AGoodLayoutFileLoadsWithExitZero()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-load").FullName;
        try
        {
            var good = System.IO.Path.Combine(folder, "good.jsonc");
            System.IO.File.WriteAllText(good, LayoutTests.Text());

            var code = LayoutLoad.Run(good, out var layout, out var message);

            Assert.AreEqual(0, code);
            Assert.IsNotNull(layout);
            Assert.IsNull(message);
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void AnEmptyPathExitsFourWithOneProblem()
    {
        var code = LayoutLoad.Run("", out var layout, out var message);

        Assert.AreEqual(4, code);
        Assert.IsNull(layout);
        Assert.AreEqual("ERROR '': file: the path is empty", message);
    }

    [TestMethod]
    public void ADirectoryPathExitsFourWithOneProblemNamingIt()
    {
        // A directory cannot be read as a file whoever runs the test, root included.
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-load").FullName;
        try
        {
            var code = LayoutLoad.Run(folder, out var layout, out var message);

            Assert.AreEqual(4, code);
            Assert.IsNull(layout);
            StringAssert.StartsWith(message, "ERROR " + folder + ": ");
            Assert.DoesNotContain("; ", message);
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void SeveralProblemsAreJoinedWithSemicolonAndSpace()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-load").FullName;
        try
        {
            var bad = System.IO.Path.Combine(folder, "bad.jsonc");
            System.IO.File.WriteAllText(bad, LayoutTests.Text(size: "9", seed: "5"));

            var code = LayoutLoad.Run(bad, out _, out var message);

            Assert.AreEqual(4, code);
            StringAssert.StartsWith(message, "ERROR " + bad + ": size: ");
            StringAssert.Contains(message, "; " + bad + ": seed: ");
            Assert.AreEqual(2, message!.Split("; ").Length);
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    static Game NewGame(Layout layout) => GameFactory.Create(TestProgram.Made(), layout.BuildMap());

    [TestMethod]
    public void AStartWithARefusedEntryExitsFourWithTheFullMessageAndNoGame()
    {
        var layout = Layout
            .Read(LayoutTests.Text(extra: "\n\"flags\": [{\"x\": 14, \"y\": 11}]"), "test.jsonc")
            .Layout!;

        var code = LayoutLoad.Start(layout, NewGame(layout), out var game, out var message);

        Assert.AreEqual(4, code);
        Assert.IsNull(game);
        Assert.AreEqual(
            "ERROR test.jsonc: flags[0]: the original's flag button does not allow a flag on the node: the castle must stand, "
                + "and the node must hold no flag, no building, and no object that blocks it",
            message
        );
    }

    [TestMethod]
    public void AStartWithABuiltLayoutGivesTheGameWithExitZero()
    {
        var layout = Layout.Read(LayoutTests.Text(extra: "\n\"castle\": {\"x\": 10, \"y\": 10}"), "test.jsonc").Layout!;
        var made = NewGame(layout);

        var code = LayoutLoad.Start(layout, made, out var game, out var message);

        Assert.AreEqual(0, code);
        Assert.AreSame(made, game);
        Assert.IsNull(message);
        Assert.IsNotNull(game!.CastleStock.Castle);
    }

    [TestMethod]
    public void AStartWithNoLayoutGivesTheGameUntouched()
    {
        var made = GameFactory.Create(TestProgram.Made(), Layout.Read(LayoutTests.Text(), "t").Layout!.BuildMap());

        var code = LayoutLoad.Start(null, made, out var game, out _);

        Assert.AreEqual(0, code);
        Assert.AreSame(made, game);
    }
}
