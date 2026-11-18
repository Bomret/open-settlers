using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Import;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A reach marker of the work-area overlay (<c>CONTEXT.md</c>, "Reach marker") on one node where the pressed gatherer building fits:
/// a plain shape the view draws, a filled disc where the worker would find a target, a ring where he would find none.
/// </summary>
public sealed record ReachMarkerDraw
{
    /// <summary>The column of the node.</summary>
    public required int Column { get; init; }

    /// <summary>The row of the node.</summary>
    public required int Row { get; init; }

    /// <summary>The horizontal position of the node in map pixels.</summary>
    public required int X { get; init; }

    /// <summary>The vertical position of the node in map pixels, raised by the node's height.</summary>
    public required int Y { get; init; }

    /// <summary>Whether the target test accepts at least one node of the work area the building would have here: a disc, else a ring.</summary>
    public required bool InReach { get; init; }
}

/// <summary>
/// The reach markers of the work-area overlay while a gatherer's build button is pressed: one on every node where that building fits
/// for the player, as Build help's marker shows the fit (<see cref="BuildMarkers"/>). Each tells whether the job search's target test
/// accepts a node of the work area the building would have there, on the ground as it stands, before the hut, its flag, and its road
/// exist. The view holds no range or test of its own (INV-0017), and reading the markers changes nothing in the game (INV-0016).
/// </summary>
public static class ReachMarkerDraws
{
    /// <summary>
    /// The markers of <paramref name="game"/> for <paramref name="player"/> and the pressed building type <paramref name="pressed"/>, in
    /// row-major order, each at its node raised by the node's height times <see cref="TerrainTriangles.HeightStep"/>. None for a type
    /// without a job search and none before the player's castle stands, since no such building fits then. A large building fits where
    /// Build help shows the large-building marker; any other where it shows the small- or the large-building marker.
    /// </summary>
    public static List<ReachMarkerDraw> Of(Game game, int player, BuildingType pressed)
    {
        if (game.SearchOf(pressed) is not { } search || game.CastleStock.Castle is null)
        {
            return [];
        }

        var map = game.Map;
        var large = game.Tables.SizeOf(pressed) == BuildingSize.Large;
        var draws = new List<ReachMarkerDraw>();
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < map.Size.Width; column++)
            {
                var marker = BuildMarkers.Of(
                    BuildSite.Evaluate(game.Tables, map, column, row, player, true, game.Buildings)
                );
                var fits = large ? marker == BuildMarker.Large : marker is BuildMarker.Small or BuildMarker.Large;
                if (!fits)
                {
                    continue;
                }

                var inReach = search.Area(map, column, row).Any(node => search.Accepts(map, node.X, node.Y));
                var (px, py) = Camera.NodePosition(column, row);
                draws.Add(
                    new ReachMarkerDraw
                    {
                        Column = column,
                        Row = row,
                        X = px,
                        Y = py - map.At(column, row).Height * TerrainTriangles.HeightStep,
                        InReach = inReach,
                    }
                );
            }
        }

        return draws;
    }
}
