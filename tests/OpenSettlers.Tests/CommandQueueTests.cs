using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The queue of commands on its own: no map, no game.
[TestClass]
public sealed class CommandQueueTests
{
    static Command At(long tick, int x = 0, int y = 0, int player = 0) =>
        new(tick, player, CommandKind.PlaceFlag, x, y);

    [TestMethod]
    public void ACommandForATickThatHasRunIsRefused()
    {
        var queue = new CommandQueue();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => queue.Add(At(2), tickCount: 3));
    }

    [TestMethod]
    public void ACommandForTheCurrentTickIsQueued()
    {
        var queue = new CommandQueue();
        queue.Add(At(3), tickCount: 3);

        Assert.IsTrue(queue.TryTakeDue(3, out var command));
        Assert.AreEqual(At(3), command);
    }

    [TestMethod]
    public void ACommandForALaterTickIsNotDue()
    {
        var queue = new CommandQueue();
        queue.Add(At(5), tickCount: 0);

        Assert.IsFalse(queue.TryTakeDue(4, out _));
    }

    [TestMethod]
    public void TheCommandsOfOneTickComeInTheirTotalOrderWhateverTheOrderTheyWereAdded()
    {
        var queue = new CommandQueue();
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
        var queue = new CommandQueue();
        queue.Add(At(1), 0);
        queue.Add(At(1), 0);

        Assert.IsTrue(queue.TryTakeDue(1, out _));
        Assert.IsFalse(queue.TryTakeDue(1, out _));
    }

    [TestMethod]
    public void TheDirectionsOfARoadCompareOneByOneThenByLength()
    {
        var queue = new CommandQueue();
        var longer = new Command(1, 0, CommandKind.BuildRoad, 1, 1, new[] { 1, 2, 3 });
        var shorter = new Command(1, 0, CommandKind.BuildRoad, 1, 1, new[] { 1, 2 });
        var other = new Command(1, 0, CommandKind.BuildRoad, 1, 1, new[] { 0, 5 });
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
        var queue = new CommandQueue();
        var directions = new[] { 1, 2 };
        queue.Add(new Command(0, 0, CommandKind.BuildRoad, 1, 1, directions), 0);
        directions[0] = 5;

        Assert.IsTrue(queue.TryTakeDue(0, out var command));
        CollectionAssert.AreEqual(new[] { 1, 2 }, (System.Collections.ICollection)command.Directions!);
    }

    static Command Place(BuildingType? building) => new(1, 0, CommandKind.PlaceBuilding, 4, 4, Building: building);

    static Command Taken(CommandQueue queue)
    {
        Assert.IsTrue(queue.TryTakeDue(1, out var command));
        return command;
    }

    [TestMethod]
    public void TwoPlacementsThatDifferInTheBuildingAreBothKeptInTheSameOrderWhicheverIsAddedFirst()
    {
        var woodcutter = Place(BuildingType.Woodcutter);
        var sawmill = Place(BuildingType.Sawmill);
        var forward = new CommandQueue();
        forward.Add(woodcutter, 0);
        forward.Add(sawmill, 0);
        var backward = new CommandQueue();
        backward.Add(sawmill, 0);
        backward.Add(woodcutter, 0);

        Assert.AreEqual(2, forward.Count);
        Assert.AreEqual(2, backward.Count);
        Assert.AreEqual(Taken(forward), Taken(backward));
    }

    [TestMethod]
    public void ACommandWithNoBuildingAndOneWithABuildingAreBothKeptInAFixedOrder()
    {
        var none = Place(null);
        var built = Place(BuildingType.Woodcutter);
        var forward = new CommandQueue();
        forward.Add(none, 0);
        forward.Add(built, 0);
        var backward = new CommandQueue();
        backward.Add(built, 0);
        backward.Add(none, 0);

        Assert.AreEqual(2, forward.Count);
        Assert.AreEqual(2, backward.Count);
        Assert.AreEqual(Taken(forward), Taken(backward));
    }

    [TestMethod]
    public void AnExactRepeatOfAPlacementQueuesOnce()
    {
        var queue = new CommandQueue();
        queue.Add(Place(BuildingType.Sawmill), 0);
        queue.Add(Place(BuildingType.Sawmill), 0);

        Assert.AreEqual(1, queue.Count);
    }
}
