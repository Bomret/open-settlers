using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ThingCheckTests
{
    const double Frame = 1.0 / 30;

    static readonly Thing Tree = new()
    {
        Kind = "object",
        X = 12,
        Y = 4,
        Values = [("code", "0x0a")],
        Name = "tree",
    };
    static readonly Thing Rock = new()
    {
        Kind = "object",
        X = 13,
        Y = 4,
        Values = [("code", "0x31")],
        Name = null,
    };

    PlayState _state = new()
    {
        CameraX = 0,
        CameraY = 0,
        Zoom = 1f,
        Seed = "1",
        Size = 1,
        Things = [Tree, Rock],
    };

    static PlayScript Parse(string text) => PlayScript.Parse(text, "play/t.play");

    IReadOnlyList<string> Problems(string script)
    {
        var run = new PlayRun(Parse(script), () => _state, "unused", _ => null);
        for (var i = 0; i < 5; i++)
            run.Advance(Frame);

        return run.Problems;
    }

    [TestMethod]
    public void TheChecksReadTheirArguments()
    {
        var script = Parse("expect-at 12 4 tree\nexpect-count carrier >= 0\nexpect-count object:0x0a = 1");

        Assert.IsEmpty(script.Problems);
        Assert.IsInstanceOfType<ExpectAtStep>(script.Steps[0]);
        Assert.IsInstanceOfType<ExpectCountStep>(script.Steps[1]);
        Assert.AreEqual(3, script.Steps[2].Line);
    }

    [TestMethod]
    [DataRow("expect-at a 4 tree", "'a' is not a whole number")]
    [DataRow("expect-at 12 -1 tree", "'-1' is not a whole number")]
    [DataRow("expect-at 12 4.5 tree", "'4.5' is not a whole number")]
    [DataRow("expect-at 12 4", "'expect-at' takes 3 arguments, found 2")]
    [DataRow("expect-at 12 4 tree tree", "'expect-at' takes 3 arguments, found 4")]
    [DataRow("expect-at 12 4 oak", "unknown name 'oak'")]
    [DataRow("expect-at 12 4 object:0x", "'object:0x' is a malformed raw code")]
    [DataRow("expect-at 12 4 object:zz", "'object:zz' is a malformed raw code")]
    [DataRow("expect-at 12 4 object:0xg1", "'object:0xg1' is a malformed raw code")]
    [DataRow("expect-at 12 4 object:0x100", "'object:0x100' is over 0xff")]
    [DataRow("expect-count tree >= -1", "'-1' is not a whole number")]
    [DataRow("expect-count tree >= many", "'many' is not a whole number")]
    [DataRow("expect-count tree == 1", "unknown operator '=='")]
    [DataRow("expect-count tree >=", "'expect-count' takes 3 arguments, found 2")]
    [DataRow("expect-count oak = 1", "unknown name 'oak'")]
    public void ABadCheckIsOneProblemNamingItsLine(string line, string reason)
    {
        var script = Parse("wait 1\n" + line);

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], reason);
        Assert.HasCount(1, script.Steps);
    }

    [TestMethod]
    public void ANameFromAnyTableIsKnown()
    {
        var script = Parse("expect-count carrier = 0\nexpect-count woodcutter = 0\nexpect-count tree = 1");

        Assert.IsEmpty(script.Problems);
    }

    [TestMethod]
    public void AnExpectAtPassesOnAMatchingNode()
    {
        Assert.IsEmpty(Problems("expect-at 12 4 tree"));
    }

    [TestMethod]
    public void AnExpectAtOnARawCodeMatchesTheCode()
    {
        Assert.IsEmpty(Problems("expect-at 12 4 object:0x0a\nexpect-at 13 4 object:0x31"));
    }

    [TestMethod]
    public void AnExpectAtOnAnEmptyNodeFailsSayingWhatTheNodeHolds()
    {
        var problems = Problems("expect-at 5 5 tree");

        Assert.AreEqual("play/t.play line 1: expect-at 5 5 tree failed: the node holds nothing", problems[0]);
    }

    [TestMethod]
    public void AnExpectAtOnTheWrongThingNamesWhatTheNodeHolds()
    {
        var problems = Problems("expect-at 12 4 object:0x31");

        Assert.AreEqual(
            "play/t.play line 1: expect-at 12 4 object:0x31 failed: the node holds object 12,4 code=0x0a tree",
            problems[0]
        );
    }

    [TestMethod]
    public void ANameSharedBySeveralCodesMatchesAnyOfThem()
    {
        _state = _state with
        {
            Things =
            [
                new Thing
                {
                    Kind = "object",
                    X = 1,
                    Y = 1,
                    Values = [("code", "0x17")],
                    Name = "tree",
                },
            ],
        };

        Assert.IsEmpty(Problems("expect-at 1 1 tree"));
    }

    [TestMethod]
    public void AnExpectAtOutsideTheMapIsAProblem()
    {
        var problems = Problems("expect-at 32 4 tree");

        StringAssert.Contains(problems[0], "line 1: ");
        StringAssert.Contains(problems[0], "outside the 32 by 32 map");
    }

    [TestMethod]
    public void AnExpectAtOnTheLastNodeOfTheMapIsInside()
    {
        var problems = Problems("expect-at 31 31 tree");

        StringAssert.Contains(problems[0], "the node holds nothing");
    }

    [TestMethod]
    public void AnExpectCountCountsTheMatchingThings()
    {
        Assert.IsEmpty(Problems("expect-count tree = 1\nexpect-count object = 2\nexpect-count stone = 0"));
        Assert.IsEmpty(Problems("expect-count tree >= 1\nexpect-count tree > 0\nexpect-count tree < 2"));
        Assert.IsEmpty(Problems("expect-count tree <= 1\nexpect-count object:0x31 = 1"));
    }

    [TestMethod]
    public void AFailedExpectCountStatesTheExpectationAndTheCount()
    {
        var problems = Problems("expect-count tree >= 30");

        Assert.AreEqual("play/t.play line 1: expect-count tree >= 30 failed: actual 1", problems[0]);
    }

    [TestMethod]
    public void AnExpectCountOnAKindTheMapViewCannotListFails()
    {
        var problems = Problems("expect-count settler = 0");

        StringAssert.Contains(problems[0], "line 1: ");
        StringAssert.Contains(problems[0], "the map view cannot list settler");
    }

    [TestMethod]
    [DataRow("building")]
    [DataRow("flag")]
    [DataRow("road")]
    public void AnExpectAtOnAKindTheMapViewCannotListFails(string kind)
    {
        var problems = Problems($"expect-at 12 4 {kind}");

        StringAssert.Contains(problems[0], $"the map view cannot list {kind}");
    }

    [TestMethod]
    [DataRow("carrier")]
    [DataRow("builder")]
    [DataRow("walks")]
    [DataRow("castle")]
    public void AJobStateOrBuildingTypeNameTheMapViewCannotListFails(string name)
    {
        Assert.Contains($"the map view cannot list {name}", Problems($"expect-count {name} = 0")[0]);
        Assert.Contains($"the map view cannot list {name}", Problems($"expect-at 12 4 {name}")[0]);
    }

    [TestMethod]
    public void ARawCodeValueWithoutThePrefixMatchesNothingAndDoesNotThrow()
    {
        _state = _state with
        {
            Things =
            [
                new Thing
                {
                    Kind = "object",
                    X = 1,
                    Y = 1,
                    Values = [("code", "a")],
                    Name = null,
                },
            ],
        };

        StringAssert.Contains(Problems("expect-at 1 1 object:0x0a")[0], "the node holds object 1,1 code=a");
    }

    [TestMethod]
    public void AKindTheGameViewListsIsCounted()
    {
        var settler = new Thing
        {
            Kind = "settler",
            X = 3,
            Y = 3,
            Values = [],
            Name = null,
        };
        _state = _state with { Ticks = 0, Things = [Tree, settler] };

        Assert.IsEmpty(Problems("expect-count settler = 1\nexpect-at 3 3 settler"));
    }

    [TestMethod]
    [DataRow("flag")]
    [DataRow("building")]
    public void InTheMapViewTheKindShadowsTheObjectNameAndTheRawCodeReachesTheObject(string word)
    {
        _state = _state with
        {
            Things =
            [
                new Thing
                {
                    Kind = "object",
                    X = 5,
                    Y = 5,
                    Values = [("code", "0x01")],
                    Name = word,
                },
            ],
        };

        StringAssert.Contains(Problems($"expect-count {word} = 1")[0], $"the map view cannot list {word}");
        Assert.IsEmpty(Problems("expect-at 5 5 object:0x01"));
    }
}
