using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A tint mark of the work-area overlay (<c>CONTEXT.md</c>, "Work-area overlay") on one node: a plain shape the view draws,
/// not a graphics-source term, since the original has no work-area image.
/// </summary>
public sealed record WorkAreaDraw
{
    /// <summary>The column of the node.</summary>
    public required int Column { get; init; }

    /// <summary>The row of the node.</summary>
    public required int Row { get; init; }

    /// <summary>The horizontal position of the node in map pixels.</summary>
    public required int X { get; init; }

    /// <summary>The vertical position of the node in map pixels, raised by the node's height.</summary>
    public required int Y { get; init; }
}

/// <summary>
/// The tint marks of the work-area overlay of a game: one on every node in the work area of at least one of the player's gatherer
/// buildings or building sites. The work area is the building's job search (<see cref="Game.SearchOf"/>) around its node, so the
/// view holds no range of its own (INV-0017). Reading the marks changes nothing in the game (INV-0016).
/// </summary>
public static class WorkAreaDraws
{
    /// <summary>
    /// The marks of <paramref name="game"/> for <paramref name="player"/>: one for each node in the work area of a building or site on
    /// the player's territory whose type has a job search, in the order the areas first reach the nodes, each at its node raised by the
    /// node's height times <see cref="TerrainTriangles.HeightStep"/>. A building taken down and burning, the castle, and a type without
    /// a job search add none.
    /// </summary>
    public static List<WorkAreaDraw> Of(Game game, int player)
    {
        var map = game.Map;
        var owner = Owner.Player(player);
        var marked = new HashSet<(int X, int Y)>();
        var draws = new List<WorkAreaDraw>();
        foreach (var building in game.Buildings)
        {
            if (
                building.IsFree
                || building.IsDemolishing
                || map.At(building.X, building.Y).Owner != owner
                || game.SearchOf(building.Type) is not { } search
            )
            {
                continue;
            }

            foreach (var (column, row) in search.Area(map, building.X, building.Y))
            {
                if (!marked.Add((column, row)))
                {
                    continue;
                }

                var (x, y) = Camera.NodePosition(column, row);
                draws.Add(
                    new WorkAreaDraw
                    {
                        Column = column,
                        Row = row,
                        X = x,
                        Y = y - map.At(column, row).Height * TerrainTriangles.HeightStep,
                    }
                );
            }
        }

        return draws;
    }
}
