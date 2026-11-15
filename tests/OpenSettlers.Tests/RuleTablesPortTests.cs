using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class RuleTablesPortTests
{
    [TestMethod]
    public void NoInstallGivenFailsTheReadWithTheProblemOfTheLaunch()
    {
        var args = LaunchArgs.Parse([], null);
        LaunchInstall.TryOpen(args, out _, out var problem);

        var port = LaunchInstall.RuleTables(args);

        var failure = Assert.ThrowsExactly<IOException>(port.Read);
        Assert.AreEqual(problem, failure.Message);
    }

    [TestMethod]
    public void AnInstallWhoseProgramIsNoPeFileIsRefusedWithTheImportersText()
    {
        using var fake = new FakeInstall();
        var expected = Assert.ThrowsExactly<InvalidDataException>(() => ProgramTablesImporter.Import([1, 2, 3]));

        var port = LaunchInstall.RuleTables(LaunchArgs.Parse(["--game", fake.Root], null));

        var refusal = Assert.ThrowsExactly<InvalidDataException>(port.Read);
        Assert.AreEqual(expected.Message, refusal.Message);
    }

    [TestMethod]
    public void AnInstallWithNoProgramFailsTheReadWithAnIoException()
    {
        using var fake = new FakeInstall(withProgram: false);
        var install = new Install { Root = fake.Root };

        Assert.ThrowsExactly<FileNotFoundException>(new HistoryEditionRuleTables(install).Read);
    }
}
