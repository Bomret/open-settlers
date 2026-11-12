using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The status byte of a building (byte 5) and the readout of a building site (ADR 0013): each flag has its bit, and a site's
// readout carries what it still needs and the height it is levelled to.
[TestClass]
public sealed class BuildingEncoderTests
{
    private static Building Hut() => new(BuildingType.Woodcutter, 3, 4, 1);

    [TestMethod]
    public void ABuildingWithNoFlagSetHasStatusZero()
    {
        Assert.AreEqual(0, BuildingEncoder.Status(Hut()));
    }

    [TestMethod]
    public void RequestFailedIsBit2()
    {
        var hut = Hut();
        hut.RequestFailed = true;

        Assert.AreEqual(0x04, BuildingEncoder.Status(hut));
    }

    [TestMethod]
    public void DemolishingIsBit5()
    {
        var hut = Hut();
        hut.IsDemolishing = true;

        Assert.AreEqual(0x20, BuildingEncoder.Status(hut));
    }

    [TestMethod]
    public void WorkerInsideIsBit6()
    {
        var hut = Hut();
        hut.WorkerInside = true;

        Assert.AreEqual(0x40, BuildingEncoder.Status(hut));
    }

    [TestMethod]
    public void WorkerOnTheWayIsBit7()
    {
        var hut = Hut();
        hut.WorkerOnTheWay = true;

        Assert.AreEqual(0x80, BuildingEncoder.Status(hut));
    }

    [TestMethod]
    public void TheFourBitsAndTheOtherStatusBitsAddUpInOneByte()
    {
        var hut = Hut();
        hut.RequestFailed = true;
        hut.IsDemolishing = true;
        hut.WorkerInside = true;
        hut.WorkerOnTheWay = true;
        hut.OtherStatus = 0x13;

        Assert.AreEqual(0xF7, BuildingEncoder.Status(hut));
    }

    [TestMethod]
    public void TheReadoutOfABuildingSiteCarriesItsPlanksStonesAndHeight()
    {
        var game = ConstructionWorld.Create();
        ConstructionWorld.Site(game, BuildingType.Forester, 17, 5);
        var site = game.Buildings[1].Site!;

        var state = game.History.Events.Of(game.History.BuildingId(1)).Last().State;

        Assert.AreEqual(ConstructionTables.ForesterPlanks, state.Get("planks"));
        Assert.AreEqual(0, state.Get("stones"));
        Assert.AreEqual(site.TargetHeight, state.Get("height"));
    }

    [TestMethod]
    public void TheReadoutOfAFinishedBuildingCarriesNoSiteFields()
    {
        var game = ConstructionWorld.Create();

        var state = game.History.Events.Of(game.History.BuildingId(0)).Last().State;

        Assert.IsNull(state.Get("planks"));
        Assert.IsNull(state.Get("height"));
    }
}
