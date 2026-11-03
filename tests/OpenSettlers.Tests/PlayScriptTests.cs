using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayScriptTests
{
    static PlayScript Parse(string text) => PlayScript.Parse(text, "play/t.play");

    [TestMethod]
    public void CommentsAndBlankLinesAreSkippedAndStepsKeepTheirLine()
    {
        var script = Parse("# a comment\n\n  wait 1.5  \n\t# another\nwait 2");

        Assert.IsEmpty(script.Problems);
        Assert.HasCount(2, script.Steps);
        Assert.AreEqual(new WaitStep { Line = 3, Seconds = 1.5 }, script.Steps[0]);
        Assert.AreEqual(5, script.Steps[1].Line);
    }

    [TestMethod]
    public void TheStemIsTheFileNameWithoutItsExtension()
    {
        Assert.AreEqual("t", Parse("").Stem);
    }

    [TestMethod]
    public void AnUnknownStepIsAProblemNamingScriptAndLine()
    {
        var script = Parse("wait 1\n\nfly 3");

        Assert.HasCount(1, script.Problems);
        Assert.AreEqual("play/t.play line 3: unknown step 'fly'", script.Problems[0]);
        Assert.HasCount(1, script.Steps);
    }

    [TestMethod]
    [DataRow("wait", "takes 1 argument")]
    [DataRow("wait soon", "not a number of seconds")]
    [DataRow("wait -1", "not a number of seconds")]
    [DataRow("wait NaN", "not a number of seconds")]
    [DataRow("wait 1 2", "takes 1 argument")]
    public void ABadWaitIsAProblemNamingItsLine(string line, string reason)
    {
        var script = Parse("# first\n" + line);

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], reason);
    }

    [TestMethod]
    public void EveryBadLineIsReported()
    {
        var script = Parse("nope\nwait x\nwait 1");

        Assert.HasCount(2, script.Problems);
        Assert.HasCount(1, script.Steps);
    }

    [TestMethod]
    public void AMissingFileIsOneProblem()
    {
        var script = PlayScript.Read("/no/such/dir/x.play");

        Assert.HasCount(1, script.Problems);
        StringAssert.Contains(script.Problems[0], "/no/such/dir/x.play");
    }
}
