using System.Collections.Generic;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The rule that picks the build marker for the build-site evaluator's judgment of a node, as the original's
/// <c>0x450d85</c> picks it (<c>docs/rules/build-sites.md</c>, "The build-site overlay").
/// </summary>
public static class BuildMarkers
{
    /// <summary>
    /// The marker for <paramref name="result"/>, or null where the original draws none: a node state of 3 or lower, a
    /// site of 0, and the evaluator's default result. A node state of 4 with any other site shows a flag. A node state of 5
    /// or more shows the marker of its site: a flag for 1, a mine for 2, a small building for 3, and a large building for 4
    /// and for the castle site 5. A node state of 5 with site 1 shows a flag although the flag click refuses that node
    /// (<see cref="BuildSiteResult.AllowsFlag"/>).
    /// </summary>
    public static BuildMarker? Of(BuildSiteResult result) =>
        result.Node <= 3 || result.Site == 0 ? null
        : result.Node == 4 ? BuildMarker.Flag
        : result.Site switch
        {
            1 => BuildMarker.Flag,
            2 => BuildMarker.Mine,
            3 => BuildMarker.Small,
            _ => BuildMarker.Large,
        };
}

/// <summary>
/// The build-site overlay of a game (<c>docs/rules/build-sites.md</c>, "The build-site overlay"): the marker of every node
/// of the map for the view's player, judged by <see cref="BuildSite.Evaluate"/> once per node. Before the player's castle
/// stands it judges for a player without a castle, so the castle sites show the large-building marker; after, only the
/// player's territory shows markers. Reading the markers changes nothing in the game.
/// </summary>
public static class BuildMarkerDraws
{
    /// <summary>
    /// The markers of every node of <paramref name="game"/>'s map for <paramref name="player"/>, in row-major order, each
    /// at its node raised by the node's height times <see cref="TerrainTriangles.HeightStep"/>; a node the original marks
    /// with nothing has no draw.
    /// </summary>
    public static List<BuildMarkerDraw> Of(Game game, int player)
    {
        var map = game.Map;
        var hasCastle = game.CastleStock.Castle is not null;
        var draws = new List<BuildMarkerDraw>();
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < map.Size.Width; column++)
            {
                var judged = BuildSite.Evaluate(game.Tables, map, column, row, player, hasCastle, game.Buildings);
                if (BuildMarkers.Of(judged) is not { } marker)
                {
                    continue;
                }

                var (x, y) = Camera.NodePosition(column, row);
                draws.Add(
                    new BuildMarkerDraw
                    {
                        Row = row,
                        X = x,
                        Y = y - map.At(column, row).Height * TerrainTriangles.HeightStep,
                        Column = column,
                        Marker = marker,
                    }
                );
            }
        }

        return draws;
    }
}
