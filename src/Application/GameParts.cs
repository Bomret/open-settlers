using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// The parts a <see cref="Game"/> is made of, built and wired by hand (<c>GameFactory</c> in the adapters): the game takes them
/// as they are and holds no state besides the tick count and the commands applied.
/// </summary>
public sealed class GameParts
{
    /// <summary>The rule tables.</summary>
    public required RuleTables Tables { get; init; }

    /// <summary>The map the game is played on.</summary>
    public required Map Map { get; init; }

    /// <summary>The castle and its stock.</summary>
    public required CastleStock CastleStock { get; init; }

    /// <summary>The flag table.</summary>
    public required FlagTable Flags { get; init; }

    /// <summary>The finished roads.</summary>
    public required RoadNetwork Network { get; init; }

    /// <summary>The building table.</summary>
    public required BuildingTable Buildings { get; init; }

    /// <summary>The settler table.</summary>
    public required SettlerTable Settlers { get; init; }

    /// <summary>The history.</summary>
    public required GameHistory History { get; init; }

    /// <summary>The causes the rules give their events.</summary>
    public required Causes Causes { get; init; }

    /// <summary>The game clock.</summary>
    public required GameClock Clock { get; init; }

    /// <summary>The game's random generator.</summary>
    public required IRandomDraws Draws { get; init; }

    /// <summary>The ground the settlers share.</summary>
    public required SettlerMovement Movement { get; init; }

    /// <summary>The behaviour of each activity type.</summary>
    public required ActivityRegistry Behaviours { get; init; }

    /// <summary>The pass of the main loop.</summary>
    public required MainPass Pass { get; init; }

    /// <summary>The castle's settlers.</summary>
    public required CastleRoster Roster { get; init; }

    /// <summary>The carriers.</summary>
    public required CarrierBehavior Carriers { get; init; }

    /// <summary>The goods on the flags.</summary>
    public required GoodsTransport Goods { get; init; }

    /// <summary>The removal and the split of a road.</summary>
    public required Removal Removal { get; init; }

    /// <summary>The walk home.</summary>
    public required WalkHomeBehavior WalkHome { get; init; }

    /// <summary>The walk to an errand.</summary>
    public required WalkBehavior Walks { get; init; }

    /// <summary>The states the workers of the huts share.</summary>
    public required HutWork Hut { get; init; }

    /// <summary>The placing of a building with its flag: sites by command, finished buildings by a layout.</summary>
    public required BuildingPlacement Placement { get; init; }

    /// <summary>The check of a road the player draws.</summary>
    public required RoadPlanner RoadPlanner { get; init; }

    /// <summary>The queue of commands waiting for their tick.</summary>
    public required CommandQueue Queue { get; init; }

    /// <summary>One handler for each kind of command.</summary>
    public required IReadOnlyList<ICommandHandler> Handlers { get; init; }

    /// <summary>The digest port.</summary>
    public required IGameDigest Digest { get; init; }
}
