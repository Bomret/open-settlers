using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The queue of commands on its own: no map, no game.
[TestClass]
public sealed class CommandQueueTests
{
    static Command At(long tick, int x = 0, int y = 0, int player = 0) =>
        new()
        {
            Tick = tick,
            Player = player,
            Kind = CommandKind.PlaceFlag,
            X = x,
            Y = y,
        };

    [TestMethod]
    public void ACommandForATickThatHasRunIsRefused()
    {
        var queue = new CommandQueue(TestCatalog.Default);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => queue.Add(At(2), tickCount: 3));
    }

    [TestMethod]
    public void ACommandForTheCurrentTickIsQueued()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(At(3), tickCount: 3);

        Assert.IsTrue(queue.TryTakeDue(3, out var command));
        Assert.AreEqual(At(3), command);
    }

    [TestMethod]
    public void ACommandForALaterTickIsNotDue()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(At(5), tickCount: 0);

        Assert.IsFalse(queue.TryTakeDue(4, out _));
    }

    [TestMethod]
    public void TheCommandsOfOneTickComeInTheirTotalOrderWhateverTheOrderTheyWereAdded()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(At(1, x: 9), 0);
        queue.Add(At(1, x: 2, player: 1), 0);
        queue.Add(At(1, x: 5), 0);

        var taken = new System.Collections.Generic.List<Command>();
        while (queue.TryTakeDue(1, out var command))
        {
            taken.Add(command);
        }

        CollectionAssert.AreEqual(new[] { At(1, x: 5), At(1, x: 9), At(1, x: 2, player: 1) }, taken);
    }

    [TestMethod]
    public void ACommandEqualToOneAlreadyQueuedQueuesOnce()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(At(1), 0);
        queue.Add(At(1), 0);

        Assert.IsTrue(queue.TryTakeDue(1, out _));
        Assert.IsFalse(queue.TryTakeDue(1, out _));
    }

    [TestMethod]
    public void TheDirectionsOfARoadCompareOneByOneThenByLength()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        var longer = new Command
        {
            Tick = 1,
            Player = 0,
            Kind = CommandKind.BuildRoad,
            X = 1,
            Y = 1,
            Directions = new[] { 1, 2, 3 },
        };
        var shorter = new Command
        {
            Tick = 1,
            Player = 0,
            Kind = CommandKind.BuildRoad,
            X = 1,
            Y = 1,
            Directions = new[] { 1, 2 },
        };
        var other = new Command
        {
            Tick = 1,
            Player = 0,
            Kind = CommandKind.BuildRoad,
            X = 1,
            Y = 1,
            Directions = new[] { 0, 5 },
        };
        queue.Add(longer, 0);
        queue.Add(shorter, 0);
        queue.Add(other, 0);

        var order = new System.Collections.Generic.List<Command>();
        while (queue.TryTakeDue(1, out var command))
        {
            order.Add(command);
        }

        CollectionAssert.AreEqual(new[] { other, shorter, longer }, order);
    }

    [TestMethod]
    public void ACallerChangingTheDirectionsOfAQueuedCommandChangesNothing()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        var directions = new[] { 1, 2 };
        queue.Add(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.BuildRoad,
                X = 1,
                Y = 1,
                Directions = directions,
            },
            0
        );
        directions[0] = 5;

        Assert.IsTrue(queue.TryTakeDue(0, out var command));
        CollectionAssert.AreEqual(new[] { 1, 2 }, (System.Collections.ICollection)command.Directions!);
    }

    static Command Place(BuildingType? building) =>
        new()
        {
            Tick = 1,
            Player = 0,
            Kind = CommandKind.PlaceBuilding,
            X = 4,
            Y = 4,
            Building = building,
        };

    static Command Taken(CommandQueue queue)
    {
        Assert.IsTrue(queue.TryTakeDue(1, out var command));
        return command;
    }

    // The made-up catalog ranks each type by its original code, as the History Edition importer does: the woodcutter's hut (2) and the
    // stonecutter's hut (4) come before the forester's hut (9) and the sawmill (0x11), against the order of their stable names.
    [TestMethod]
    public void TwoPlacementsThatDifferInTheBuildingAreBothKeptInTheOrderOfTheirTieRanksWhicheverIsAddedFirst()
    {
        foreach (
            var (first, second) in new[]
            {
                (BuildingType.Woodcutter, BuildingType.Sawmill),
                (BuildingType.Stonecutter, BuildingType.Forester),
            }
        )
        {
            var forward = new CommandQueue(TestCatalog.Default);
            forward.Add(Place(first), 0);
            forward.Add(Place(second), 0);
            var backward = new CommandQueue(TestCatalog.Default);
            backward.Add(Place(second), 0);
            backward.Add(Place(first), 0);

            Assert.AreEqual(2, forward.Count);
            Assert.AreEqual(2, backward.Count);
            Assert.AreEqual(Place(first), Taken(forward), $"{first} before {second}");
            Assert.AreEqual(Place(first), Taken(backward), $"{first} before {second}, added last");
        }
    }

    // An added type with no code states its rank in its data: the tannery's 3 puts it before the stonecutter's hut (4), though its
    // stable name sorts after.
    [TestMethod]
    public void AnAddedTypeWithNoCodeGoesByTheTieRankItsDataStatesWhicheverIsAddedFirst()
    {
        var tannery = new BuildingType { Name = "tannery" };
        var catalog = new ContentCatalog(
            TestCatalog.Default.Goods,
            TestCatalog
                .Buildings()
                .Append(
                    TestCatalog.Buildings().First() with
                    {
                        Name = tannery.Name,
                        OriginalCode = null,
                        TieRank = 3,
                    }
                ),
            TestCatalog.Jobs()
        );
        var forward = new CommandQueue(catalog);
        forward.Add(Place(tannery), 0);
        forward.Add(Place(BuildingType.Stonecutter), 0);
        var backward = new CommandQueue(catalog);
        backward.Add(Place(BuildingType.Stonecutter), 0);
        backward.Add(Place(tannery), 0);

        Assert.AreEqual(Place(tannery), Taken(forward));
        Assert.AreEqual(Place(tannery), Taken(backward), "added last");
    }

    [TestMethod]
    public void ACommandWithNoBuildingAndOneWithABuildingAreBothKeptInAFixedOrder()
    {
        var none = Place(null);
        var built = Place(BuildingType.Woodcutter);
        var forward = new CommandQueue(TestCatalog.Default);
        forward.Add(none, 0);
        forward.Add(built, 0);
        var backward = new CommandQueue(TestCatalog.Default);
        backward.Add(built, 0);
        backward.Add(none, 0);

        Assert.AreEqual(2, forward.Count);
        Assert.AreEqual(2, backward.Count);
        Assert.AreEqual(Taken(forward), Taken(backward));
    }

    // A good the stock does not name counts 0, so a stock that names a good with 0 is the stock that leaves it out.
    [TestMethod]
    public void ACastleWhoseStockNamesAGoodWithNoneIsTheCastleWhoseStockLeavesItOut()
    {
        Command Castle(Dictionary<Good, int> stock) =>
            new()
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 4,
                Y = 4,
                Stock = stock,
            };
        var named = Castle(new() { [Good.Grain] = 0, [Good.Log] = 3 });
        var left = Castle(new() { [Good.Log] = 3 });

        Assert.AreEqual(named, left);
        Assert.AreEqual(left, named);
        Assert.AreNotEqual(Castle(new() { [Good.Log] = 3 }), Castle(new() { [Good.Log] = 3, [Good.Grain] = 1 }));
    }

    [TestMethod]
    public void AnExactRepeatOfAPlacementQueuesOnce()
    {
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(Place(BuildingType.Sawmill), 0);
        queue.Add(Place(BuildingType.Sawmill), 0);

        Assert.AreEqual(1, queue.Count);
    }
}
