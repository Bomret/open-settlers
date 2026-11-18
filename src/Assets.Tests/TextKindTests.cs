using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

// The test of the text kind that runs through the asset check (the library of the asset check owns it); the tests of the
// kind itself are in the History Edition source's test project, in the class of the same name.
[TestClass]
public sealed class TextKindTests
{
    static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    static FakeInstall Install3(string eng, string ger, string fre) =>
        new FakeInstall()
            .Put("loca/eng.txt", Bytes(eng))
            .Put("loca/ger.txt", Bytes(ger))
            .Put("loca/fre.txt", Bytes(fre));

    [TestMethod]
    public void TheCheckNamesTheFileAndLineOfARejectedLineAndStillReadsTheOtherFiles()
    {
        using var fake = Install3("ALPHA=\"one\"\r\n", "ALPHA=\"eins\"\r\nbroken\r\n", "ALPHA=\"un\"\r\n");

        var args = LaunchArgs.Parse(["--check", "--game", fake.Root], null);

        var report = AssetCheck.Run(args, NamedKinds.Over(args, "text"));

        Assert.AreEqual(1, report.ExitCode);
        Assert.IsTrue(report.Lines.Any(l => l.StartsWith("FAIL loca/ger.txt: line 2")));
        Assert.IsTrue(report.Lines.Contains("loca/eng.txt: 1 entry (text)"));
        Assert.IsTrue(report.Lines.Contains("loca/fre.txt: 1 entry (text)"));
    }
}
