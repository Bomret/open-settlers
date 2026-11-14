using System;
using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The buttons of the map view that decide what a click does. Several may be pressed at once; <see cref="ClickDecision"/>
/// picks by precedence.
/// </summary>
/// <param name="Road">The road button is pressed.</param>
/// <param name="Remove">The remove button is pressed.</param>
/// <param name="Flag">The flag button is pressed.</param>
/// <param name="Buildings">The building types whose buttons are pressed, in button order.</param>
public readonly record struct ClickMode(
    bool Road = false,
    bool Remove = false,
    bool Flag = false,
    IReadOnlyList<BuildingType>? Buildings = null
)
{
    /// <summary>No button pressed: a click places the castle.</summary>
    public static ClickMode None => default;
}

/// <summary>
/// The map view's click-to-command decision, kept out of the Godot control so the tests run it. The control reads its
/// buttons, asks here, and enqueues the command.
/// </summary>
public static class ClickDecision
{
    /// <summary>
    /// The command a click on node <paramref name="x"/>, <paramref name="y"/> issues, or null. With no game or no map,
    /// nothing. In road mode with a road drawing, the drawing takes the node and a build command comes only where the
    /// road reaches a flag. In remove mode, the removal <paramref name="removal"/> gives for the node, or nothing; it
    /// never falls through to placing. Else the first pressed building type is placed, else a flag with the flag
    /// button, otherwise the castle. Road mode with no drawing falls through to those choices. Every command carries
    /// the game's current tick and player 0.
    /// </summary>
    public static Command? Decide(
        ClickMode mode,
        Game? game,
        Map? map,
        RoadDrawing? drawing,
        Func<int, int, int?> removal,
        int x,
        int y
    )
    {
        if (game is null || map is null)
        {
            return null;
        }

        if (mode.Road && drawing is not null)
        {
            return drawing.Click(x, y);
        }

        if (mode.Remove)
        {
            return removal(x, y) is { } kind
                ? new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = kind,
                    X = x,
                    Y = y,
                }
                : null;
        }

        if (mode.Buildings is { Count: > 0 } buildings)
        {
            return new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = x,
                Y = y,
                Building = buildings[0],
            };
        }

        return new Command
        {
            Tick = game.TickCount,
            Player = 0,
            Kind = mode.Flag ? CommandKind.PlaceFlag : CommandKind.PlaceCastle,
            X = x,
            Y = y,
        };
    }
}
