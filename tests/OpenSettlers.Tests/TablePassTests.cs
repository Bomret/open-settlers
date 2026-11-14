using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The slices of the item tables a pass runs for a phase: the flag table's, the building table's, then the settler table's, each
// settler handed to the behaviour of its activity type (ADR 0005, docs/rules/timing.md).
[TestClass]
public sealed class TablePassTests
{
    static SettlerTable Settlers(int count)
    {
        var table = new SettlerTable();
        for (var slot = 0; slot < count; slot++)
        {
            table.Add(new Settler(new Carrier(), 0, 0, 0, 0, slot % 2 == 0 ? new WaitsInCastle() : new BuildsCastle()));
        }

        return table;
    }

    static BuildingTable Buildings(int count)
    {
        var table = new BuildingTable();
        for (var slot = 0; slot < count; slot++)
        {
            table.Add(new Building(BuildingType.Woodcutter, slot, 0, slot));
        }

        return table;
    }

    [TestMethod]
    public void APhaseRunsTheFlagSliceThenTheBuildingsOfThePhaseThenItsSettlersInTheOrderOfTheirSlots()
    {
        var turns = new List<string>();
        var registry = new ActivityRegistry();
        registry.Register<WaitsInCastle>((slot, _, _) => turns.Add($"settler {slot}"));
        registry.Register<BuildsCastle>((slot, _, _) => turns.Add($"settler {slot}"));
        var pass = new TablePass(Settlers(40), Buildings(70), registry);

        pass.Run(1, () => turns.Add("flags"), (slot, _) => turns.Add($"building {slot}"));

        var expected = new[] { "flags" }
            .Concat(Enumerable.Range(32, 32).Select(slot => $"building {slot}"))
            .Concat(new[] { "settler 1", "settler 17", "settler 33" });
        CollectionAssert.AreEqual(expected.ToArray(), turns);
    }

    [TestMethod]
    public void ABuildingSlotTurnsInThePhaseOfItsGroupOf32EveryThousandAndTwentyFour()
    {
        var turned = new List<int>();
        var pass = new TablePass(Settlers(0), Buildings(1030), new ActivityRegistry());

        pass.Run(0, () => { }, (slot, _) => turned.Add(slot));

        Assert.AreEqual(32 + 6, turned.Count);
        Assert.AreEqual(1024, turned[32]);
    }

    [TestMethod]
    public void ASettlerTableThatIsNotAMultipleOf16TurnsEachSettlerOfThePhaseOnce()
    {
        var seen = new List<int>();
        var registry = new ActivityRegistry();
        registry.Register<WaitsInCastle>((slot, _, _) => seen.Add(slot));
        registry.Register<BuildsCastle>((slot, _, _) => seen.Add(slot));
        var pass = new TablePass(Settlers(19), new BuildingTable(), registry);

        pass.Run(2, () => { }, (_, _) => { });
        pass.Run(3, () => { }, (_, _) => { });

        CollectionAssert.AreEqual(new[] { 2, 18, 3 }, seen);
    }

    [TestMethod]
    public void AFreeBuildingSlotTakesNoTurn()
    {
        var buildings = Buildings(3);
        buildings.Free(1);
        var turned = new List<int>();
        var pass = new TablePass(Settlers(0), buildings, new ActivityRegistry());

        pass.Run(0, () => { }, (slot, _) => turned.Add(slot));

        CollectionAssert.AreEqual(new[] { 0, 2 }, turned);
    }

    [TestMethod]
    public void ASettlerVisitedOutsideItsSliceGoesToTheBehaviourOfItsActivityType()
    {
        var seen = new List<string>();
        var registry = new ActivityRegistry();
        registry.Register<WaitsInCastle>((slot, _, _) => seen.Add($"castle {slot}"));
        registry.Register<BuildsCastle>((slot, _, _) => seen.Add($"builds {slot}"));
        var pass = new TablePass(Settlers(3), new BuildingTable(), registry);

        pass.Visit(1);
        pass.Visit(2);

        CollectionAssert.AreEqual(new[] { "builds 1", "castle 2" }, seen);
    }

    [TestMethod]
    public void ASettlerHoldsNoPackedStateFieldsOfTheOriginal()
    {
        var names = typeof(Settler).GetProperties().Select(property => property.Name).ToList();

        CollectionAssert.DoesNotContain(names, "State");
        CollectionAssert.DoesNotContain(names, "Mode");
        CollectionAssert.DoesNotContain(names, "Target");
        CollectionAssert.DoesNotContain(names, "Heading");
        CollectionAssert.DoesNotContain(names, "Next");
    }
}
