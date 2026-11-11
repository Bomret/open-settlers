using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Where and in what order the walking settlers are drawn, under every graphics source (<c>docs/rules/settlers.md</c>,
/// "Drawing the walk"). A settler's frame (the shape of its step, which holds the direction) and the time it has left of the
/// step pick a step of the <see cref="StepOffsets"/>; the settler is drawn at its node, lifted by 4 times its height and 2
/// more, plus the step's offsets (<c>0x43f537</c>). The record joins the row of the settler plus the byte of its frame's row
/// (<c>0x43f402</c>), and the rows are drawn from the top; the records of one row come in the order of the nodes, row by row
/// and from the west. A settler is drawn as three terms: its figure, then its overlay, then its second figure (a source
/// that has no second figure for a job draws nothing for the third). A carrier that waits off its node is drawn by the tile
/// pass (<c>0x44d77e</c>), after the settlers of its row, at its node lifted by 4 times its height.
/// <b>Confirmed</b> by the draw runs of <c>tests/OpenSettlers.Tests/settler-draws.txt</c> for the carrier (with each good), the
/// woodcutter, and the settler of the type 1 hut. A carrier that waits next to a flag (state 0x42), a carrier told to go home
/// (0x44, 0x45), a settler that claims its node (0x1c), and a settler at work (0x12, 0x1f) have no record.
/// <b>Inferred:</b> every state the recordings hold no settler of.
/// </summary>
/// <param name="steps">The dx and dy of every step of every walk frame.</param>
/// <param name="frameRows">For each frame what the record builder adds to the row of the settler's record (<c>0x43f402</c>); none for no frame.</param>
public sealed class SettlerFigures(StepOffsets steps, sbyte[] frameRows)
{
    // The record builder lifts a settler 2 pixels above the node's height (0x43f537: the height times 4, then 2 more).
    private const int SettlerLift = 2;

    private static readonly SettlerPart[] Parts = [SettlerPart.Figure, SettlerPart.Overlay, SettlerPart.SecondFigure];

    // The settlers that are drawn walk the roads (state 2), wait out a step (5), walk along a road as a carrier does (3), hand
    // a good over (0xe), step through a door (4), walk free (0x10), cut (0x11), or search for a flag home (0x19). **Confirmed**
    // (draw run, settler-draws.txt): a carrier that waits next to a flag (state 0x42) is not drawn at all: the carrier's figure
    // handler (0x43f8ae) returns at once for it. **Confirmed** the same way: a carrier whose road was taken down or joined and
    // that waits for its turn to go home (states 0x44 and 0x45, docs/rules/removal.md) is not drawn either, nor is a settler
    // that claims its node (0x1c) on its way home, nor a settler at work (the woodcutter's 0x12, the state 0x1f of the settler
    // of the type 1 hut). A carrier hands the good it carries over to the castle in state 0xe (docs/rules/goods.md);
    // **Confirmed** (draw run): it is drawn as one on its road is, with the good in its hands. A woodcutter that steps through
    // its door, walks free along its vector, or cuts is drawn with the frame and the timer it holds as a walking settler is
    // (docs/rules/woodcutting.md). A settler that searches for the nearest flag on its way home is drawn with the frame 0x52
    // the claim sets, as a walking settler is. **Confirmed** (draw run, the woodcutter and the dismissed carrier).
    private static bool IsDrawn(Activity activity) =>
        activity
            is Walks
                or WaitsOutStep
                or WalksRoad
                or HandsOver
                or Door
                or WalksFree
                or Cuts
                or CutsStone
                or Plants
                or SearchesHome
                or Builds
                or LeavesSite;

    /// <summary>
    /// The terms of the settlers that walk, in the order of their rows, each settler's figure, overlay, and second figure in
    /// that order; the clock is the game's (16 bits). The waiting carriers that the tile pass draws follow the settlers of
    /// their row.
    /// </summary>
    public List<SettlerDraw> Draws(Map map, IReadOnlyList<Settler> settlers, ushort clock)
    {
        var records = new List<(int Order, int Y, int X, int Row, List<SettlerDraw> Terms)>();
        foreach (var settler in settlers)
        {
            if (
                !IsDrawn(settler.Activity)
                || (
                    settler.Job != Job.Carrier
                    && settler.Job != Job.Woodcutter
                    && settler.Job != Job.Stonecutter
                    && settler.Job != Job.TypeOneHut
                    && settler.Job != Job.Builder
                    && settler.Job != Job.Forester
                )
            )
            {
                continue;
            }

            // The time left runs down with the clock between the settler's turns (the view pass gives a settler in view a
            // turn every pass, which runs its timer down by the clock units since its stamp).
            var elapsed = (ushort)(clock - settler.Stamp);
            var left = settler.Timer >= elapsed ? settler.Timer - elapsed : 0;
            if (steps.At(settler.Frame, left) is not { } step)
            {
                continue;
            }

            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            var height = map.At(settler.X, settler.Y).Height;
            x += step.Dx;
            y += step.Dy - height * TerrainTriangles.HeightStep - SettlerLift;
            // The record joins the list of its row, which the frame moves (0x43f537); a list holds its records in the order
            // the original scans the nodes, row by row and from the west, so the order of the records is the settlers'
            // row, then their node's row and column.
            var row = settler.Y + (settler.Frame < frameRows.Length ? frameRows[settler.Frame] : 0);
            var carried = WalkingActivity.LoadOf(settler.Activity)?.Good ?? Good.None;
            records.Add(
                (
                    0,
                    settler.Y,
                    settler.X,
                    row,
                    [
                        .. Parts.Select(part => new SettlerDraw(
                            row,
                            x,
                            y,
                            part,
                            settler.Job,
                            settler.Activity,
                            WalkingActivity.HeadingOf(settler.Activity),
                            settler.Frame,
                            left,
                            settler.Player,
                            carried
                        )),
                    ]
                )
            );
        }

        // 0x44d3ae draws each row's settler records (0x44ffe7), then the records the tile pass made for that row's nodes
        // (0x44fff3 on the list at 0x6749a3), which are the waiting carriers'.
        records.AddRange(WaitingCarriers(map, settlers, clock));

        return
        [
            .. records
                .OrderBy(record => record.Row)
                .ThenBy(record => record.Order)
                .ThenBy(record => record.Y)
                .ThenBy(record => record.X)
                .SelectMany(record => record.Terms),
        ];
    }

    // The records the tile pass makes for the nodes marked by a waiting carrier (0x44d77e). The position is the node lifted by
    // 4 times its height (no lift of 2, unlike 0x43f537); the source picks the figure by the node and the clock, and moves it
    // by the offsets of the node's road directions.
    private static IEnumerable<(int Order, int Y, int X, int Row, List<SettlerDraw> Terms)> WaitingCarriers(
        Map map,
        IReadOnlyList<Settler> settlers,
        ushort clock
    )
    {
        foreach (var settler in settlers)
        {
            if (settler.Job != Job.Carrier || settler.Activity is not WaitsOffRoad)
            {
                continue;
            }

            var node = map.At(settler.X, settler.Y);
            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            y -= node.Height * TerrainTriangles.HeightStep;
            var waiting = new WaitingNode(settler.X, node.LakeMark, node.Roads);

            yield return (
                1,
                settler.Y,
                settler.X,
                settler.Y,
                [
                    .. Parts.Select(part => new SettlerDraw(
                        settler.Y,
                        x,
                        y,
                        part,
                        settler.Job,
                        settler.Activity,
                        null,
                        settler.Frame,
                        clock,
                        settler.Player,
                        Good.None,
                        waiting
                    )),
                ]
            );
        }
    }
}
