using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The walk home of a settler whose work was taken away, built from hand-made tables and a fixed random sequence.
[TestClass]
public sealed class WalkHomeBehaviorTests
{
    private static WalkHomeBehavior Behaviour(BehaviourBench bench, System.Func<int, int, int>? route = null) =>
        new(
            bench.Movement,
            bench.Buildings,
            bench.Flags,
            bench.Roads,
            bench.History,
            bench.Draws,
            route ?? ((_, _) => -1)
        );

    private static Settler Walker(int x, int y, Activity activity) => new(new Woodcutter(), 0, x, y, 0, activity);

    [TestMethod]
    public void ASettlerClaimsItsNodeWhenNoOtherSettlerStandsThere()
    {
        var bench = new BehaviourBench();
        var settler = Walker(6, 6, new ClaimsHomeNode());
        var slot = bench.Settlers.Add(settler);
        var behaviour = Behaviour(bench);

        behaviour.Claim(slot, settler);

        Assert.AreEqual(
            (slot, true, typeof(SearchesHome)),
            (bench.Movement.Occupant(bench.Map.Index(6, 6)), behaviour.IsWalking(slot), settler.Activity.GetType())
        );
    }

    [TestMethod]
    public void ASettlerWaitsWhereAnotherSettlerStandsOnItsNode()
    {
        var bench = new BehaviourBench();
        bench.Place(Walker(6, 6, new RawState(0)));
        var settler = Walker(6, 6, new ClaimsHomeNode());
        var slot = bench.Settlers.Add(settler);
        var behaviour = Behaviour(bench);

        behaviour.Claim(slot, settler);

        Assert.AreEqual((false, typeof(ClaimsHomeNode)), (behaviour.IsWalking(slot), settler.Activity.GetType()));
    }

    [TestMethod]
    public void WithNoFlagNearASearchDrawsARandomNodeOfItsOwnPlayerAndWalksToIt()
    {
        // The draw 0x0a0b is the offsets 3 and 2 (its low byte and its high byte, masked with 15, less 8).
        var bench = new BehaviourBench(0x0a0b);
        bench.RaiseAndOwn();
        var settler = Walker(10, 10, new SearchesHome());
        var slot = bench.Place(settler);
        bench.Clock = 8;

        Behaviour(bench).Search(slot, settler);

        Assert.AreEqual(
            (new WalksFree(new Trip(new Vector(3, 2), new Home())), 1),
            (settler.Activity with { Residue = default }, bench.Draws.Taken)
        );
    }

    [TestMethod]
    public void NoStorageIsReachedFromAFlagNoRoadLeadsTo()
    {
        var bench = new BehaviourBench();
        bench.Buildings.Add(new Building(BuildingType.Castle, 3, 3, 0));
        bench.Flags.Add(4, 4);
        bench.Flags.Add(9, 9);
        var behaviour = Behaviour(bench);

        Assert.AreEqual((0, -1), (behaviour.StorageFlag(0), behaviour.StorageFlag(1)));
    }

    [TestMethod]
    public void ASettlerThatArrivesOffAFlagSearchesAgain()
    {
        var bench = new BehaviourBench();
        var settler = Walker(6, 6, new WalksFree(new Trip(new Vector(0, 0), new Home())));
        var slot = bench.Place(settler);

        Behaviour(bench).ArriveHome(slot, settler);

        Assert.AreEqual(typeof(SearchesHome), settler.Activity.GetType());
    }

    [TestMethod]
    public void ASettlerThatEntersTheStorageFreesItsNodeAndWaitsThere()
    {
        var bench = new BehaviourBench();
        var settler = Walker(6, 6, new Door(0, true));
        var slot = bench.Place(settler);
        var node = bench.Map.Index(6, 6);

        Behaviour(bench).EnterStorage(slot, node, settler);

        Assert.AreEqual((0, typeof(WaitsInCastle)), (bench.Movement.Occupant(node), settler.Activity.GetType()));
    }
}
