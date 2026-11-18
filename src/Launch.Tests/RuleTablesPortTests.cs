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

        var port = LaunchInstall.RuleTables(args, "project");

        var failure = Assert.ThrowsExactly<IOException>(port.Read);
        Assert.AreEqual(problem, failure.Message);
    }

    [TestMethod]
    public void AnInstallWhoseProgramIsNoPeFileIsRefusedWithTheImportersText()
    {
        using var fake = new FakeInstall();
        Assert.IsTrue(Install.TryOpen(fake.Root, out var install, out _));
        var expected = Assert.ThrowsExactly<InvalidDataException>(new HistoryEditionRuleTables(install).Read);

        var port = LaunchInstall.RuleTables(LaunchArgs.Parse(["--game", fake.Root], null), "project");

        var refusal = Assert.ThrowsExactly<InvalidDataException>(port.Read);
        Assert.AreEqual(expected.Message, refusal.Message);
    }

    [TestMethod]
    public void AnInstallWithNoProgramFailsTheReadWithAnIoException()
    {
        using var fake = new FakeInstall();
        Assert.IsTrue(Install.TryOpen(fake.Root, out var install, out _));
        // The program is gone after the install opened.
        File.Delete(Path.Combine(fake.Root, "Siedler1.exe"));

        Assert.ThrowsExactly<FileNotFoundException>(new HistoryEditionRuleTables(install).Read);
    }
}
