using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayStateTests
{
    static readonly PlayState State = new(CameraX: 120, CameraY: 40, Zoom: 1.25f, Seed: "1234567812345678", Size: 2);

    static ExpectStep Expect(string field, string op, string value) =>
        new()
        {
            Line = 7,
            Field = field,
            Op = op,
            Value = value,
        };

    [TestMethod]
    public void TheDumpListsEveryFieldOnItsOwnLine()
    {
        var expected = "camera-x=120\ncamera-y=40\nzoom=1.2500\nseed=1234567812345678\nsize=2\n";

        Assert.AreEqual(expected, State.Dump());
    }

    [TestMethod]
    [DataRow("camera-x", "=", "120")]
    [DataRow("camera-x", "<", "121")]
    [DataRow("camera-x", "<=", "120")]
    [DataRow("camera-y", ">=", "40")]
    [DataRow("camera-y", ">", "39")]
    [DataRow("zoom", "=", "1.25")]
    [DataRow("zoom", ">", "1.2499")]
    [DataRow("seed", "=", "1234567812345678")]
    [DataRow("size", "=", "2")]
    public void AnExpectThatHoldsGivesNoProblem(string field, string op, string value)
    {
        Assert.IsNull(State.Check(Expect(field, op, value)));
    }

    [TestMethod]
    public void AFailedExpectStatesTheExpectationAndTheActualValue()
    {
        var problem = State.Check(Expect("camera-x", ">", "200"));

        Assert.AreEqual("expect camera-x > 200 failed: actual 120", problem);
    }

    [TestMethod]
    public void ZoomComparesAtFourDecimals()
    {
        var zoom = new PlayState(0, 0, 1.00001f, "1", 1);

        Assert.IsNull(zoom.Check(Expect("zoom", "=", "1")));
        Assert.IsNotNull(zoom.Check(Expect("zoom", "=", "1.0001")));
    }

    [TestMethod]
    public void AFailedSeedExpectStatesTheActualSeed()
    {
        var problem = State.Check(Expect("seed", "=", "1"));

        Assert.AreEqual("expect seed = 1 failed: actual 1234567812345678", problem);
    }
}
