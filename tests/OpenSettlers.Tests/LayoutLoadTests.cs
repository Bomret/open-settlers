using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Launch;

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
}
