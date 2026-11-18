using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The asset check on the launch's MissingInstall (internal to the launch library, so it needs the OpenSettlers.Tests
// grant and belongs to no pair); the rest of this class is in src/Assets.Tests.
[TestClass]
public sealed class AssetCheckTests
{
    static readonly LaunchArgs Check = LaunchArgs.Parse(["--check"], null);

    [TestMethod]
    public void APortWithNoUsableDataExitsTwoWithItsReasonAndReadsNothing()
    {
        var report = AssetCheck.Run(
            Check,
            new MissingInstall("no install given: pass --game <dir> or set SIEDLER_DIR")
        );

        Assert.AreEqual(2, report.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "ERROR no install given: pass --game <dir> or set SIEDLER_DIR" },
            report.Lines.ToList()
        );
    }

    [TestMethod]
    public void ABadArgumentExitsFourBeforeThePortIsAsked()
    {
        var report = AssetCheck.Run(LaunchArgs.Parse(["--check", "--map"], null), new MissingInstall("never asked"));

        Assert.AreEqual(4, report.ExitCode);
        Assert.IsFalse(report.Lines.Any(l => l.Contains("never asked")));
    }
}
