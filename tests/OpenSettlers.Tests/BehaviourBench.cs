using System;
using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The pieces a settler behaviour is built from, made by hand: the map, the item tables, the movement, a history, and a fixed
// sequence of random draws. A test builds a behaviour from them and runs its turns without a castle or a game.
internal sealed class BehaviourBench
{
    public BehaviourBench(params ushort[] draws)
        : this((byte[]?)null, draws) { }

    // The tables hold the made-up transport priorities (index good - 1), or all 0 for none.
    public BehaviourBench(byte[]? transportPriorities, params ushort[] draws)
        : this(LayoutSettlersTests.MadeTables(transportPriorities: transportPriorities), draws) { }

    // The rule tables are the given ones: a test of the materials that reach a building site gives tables that name the goods' classes.
    public BehaviourBench(RuleTables tables, params ushort[] draws)
    {
        Draws = new QueuedDraws(draws);
        Tables = tables;
        Movement = new SettlerMovement(Map, Buildings, Tables, () => Clock, _ => new Residue());
        History = new GameHistory(new NoSnapshots(), () => 0);
        // Slot 0 of the settler table reads as a free node in an occupant list: the settlers under test take later slots.
        Settlers.Add(new Settler(new Jobless(), 0, 0, 0, 0, new RawState(0)));
    }

    public ushort Clock { get; set; }

    public Map Map { get; } = new(MapSize.Offered[0]);

    public RuleTables Tables { get; }

    public SettlerTable Settlers { get; } = new();

    public BuildingTable Buildings { get; } = new();

    public FlagTable Flags { get; } = new();

    public RoadNetwork Roads { get; } = new();

    public SettlerMovement Movement { get; }

    public GameHistory History { get; }

    public QueuedDraws Draws { get; }

    // A settler on the node, in the next slot of the settler table and the node's occupant list.
    public int Place(Settler settler)
    {
        var slot = Settlers.Add(settler);
        Movement.Occupy(Map.Index(settler.X, settler.Y), slot);

        return slot;
    }

    // A road of `steps` steps from the flag `from` in `direction` to the flag `to` (which it leaves in `back`), laid in the road
    // network and the flags' sides, with a carrier on it or none. The map is not touched: the goods rules read the flag table.
    public Road Join(int from, int direction, int to, int back, int steps = 2, bool carrier = true)
    {
        var road = new Road(from, direction, to, back, [.. System.Linq.Enumerable.Repeat(direction, steps)], false);
        Roads.Add(road);
        var length = LengthClass.OfSteps(steps);
        var start = Flags[from].Side(direction);
        var end = Flags[to].Side(back);
        start.SetLength(length, carrierOnTheWay: false);
        end.SetLength(length, carrierOnTheWay: false);
        start.FarDirection = back;
        end.FarDirection = direction;
        start.HoldsCarrier = carrier;
        end.HoldsCarrier = carrier;

        return road;
    }

    // Every node of the map at a height of 1 and of the player 0: the node a settler may walk home to.
    public void RaiseAndOwn()
    {
        for (var index = 0; index < Map.Count; index++)
        {
            Map.At(index).Height = 1;
            Map.At(index).Owner = Owner.Player(0);
        }
    }
}

internal sealed class QueuedDraws(ushort[] draws) : IRandomDraws
{
    private readonly Queue<ushort> _draws = new(draws);

    public int Taken { get; private set; }

    public void Enqueue(ushort draw) => _draws.Enqueue(draw);

    public RandomGenerator Generator => default;

    public ushort Draw()
    {
        Taken++;

        return _draws.Count > 0 ? _draws.Dequeue() : throw new InvalidOperationException("the fixed sequence ran out");
    }
}

internal sealed class NoSnapshots : IThingSnapshots
{
    public ThingState Building(int slot) => new([]);

    public ThingState Flag(int slot) => new([]);

    public ThingState Settler(int slot) => new([]);
}
