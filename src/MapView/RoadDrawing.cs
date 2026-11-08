using System.Collections.Generic;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The stand-in for the original's road drawing at the pointer (<c>docs/rules/roads.md</c>). The original draws the
/// road into the map step by step as the player clicks the next node, takes a step back where the player clicks the
/// previous node, builds the road where a click reaches a flag, and takes the drawn bits back where the player
/// cancels. This keeps the road being drawn outside the game: it asks <see cref="RoadPlanner.Check"/> after each click,
/// so an open road or a cancelled one leaves no state behind, and it hands back a <see cref="CommandKind.BuildRoad"/>
/// command when the road reaches a flag.
/// </summary>
public sealed class RoadDrawing(Game game)
{
    private readonly List<int> _directions = [];
    private (int X, int Y)? _start;

    /// <summary>True from the click on the start flag until the road is built or cancelled.</summary>
    public bool Active => _start is not null;

    /// <summary>
    /// The nodes of the road being drawn, from its start flag on, as unwrapped column and row, so a road over the
    /// map's edge continues past it. Empty when no road is being drawn.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> Nodes
    {
        get
        {
            if (_start is not { } start)
            {
                return [];
            }

            var nodes = new List<(int X, int Y)> { start };
            var (x, y) = start;
            foreach (var direction in _directions)
            {
                x += RoadBuilding.Steps[direction].Dx;
                y += RoadBuilding.Steps[direction].Dy;
                nodes.Add((x, y));
            }

            return nodes;
        }
    }

    /// <summary>
    /// A click on a node. With no road being drawn, a flag of the player's starts one. With one, the click on a node
    /// next to the end of the road is a step: refused steps are ignored (the original sounds an error and changes
    /// nothing), a click on the node before the end takes the last step back, and a step that reaches a flag ends the
    /// drawing and returns the command that builds the road. A click elsewhere changes nothing.
    /// </summary>
    public Command? Click(int column, int row)
    {
        var size = game.Map.Size;
        if (_start is not { } start)
        {
            if (game.RoadPlanner.Check(column, row, []) == RoadStatus.Open)
            {
                _start = (column, row);
            }

            return null;
        }

        var nodes = Nodes;
        var (endX, endY) = nodes[^1];
        for (var direction = 0; direction < 6; direction++)
        {
            var (dx, dy) = RoadBuilding.Steps[direction];
            if (((endX + dx - column) & (size.Width - 1)) != 0 || ((endY + dy - row) & (size.Height - 1)) != 0)
            {
                continue;
            }

            if (_directions.Count > 0 && direction == (_directions[^1] + 3) % 6)
            {
                _directions.RemoveAt(_directions.Count - 1);

                return null;
            }

            var next = new List<int>(_directions) { direction };
            switch (game.RoadPlanner.Check(start.X, start.Y, next))
            {
                case RoadStatus.Open:
                    _directions.Add(direction);
                    break;
                case RoadStatus.Complete:
                    Cancel();

                    return new Command(game.TickCount, 0, CommandKind.BuildRoad, start.X, start.Y, next);
            }

            return null;
        }

        return null;
    }

    /// <summary>Drops the road being drawn. Nothing was built, so nothing is left behind.</summary>
    public void Cancel()
    {
        _start = null;
        _directions.Clear();
    }
}
