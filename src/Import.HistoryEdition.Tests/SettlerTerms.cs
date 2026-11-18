using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Tests;

/// <summary>
/// The shift the header of a figure gives the second figure drawn from it: the header bytes 0 and 1 of the figure the pick
/// names (<c>0x4066d2</c>, <c>0x4066e4</c>), by the entry of the figure; none for a pick that is no second figure, for a figure
/// without a header, and where no shifts are given.
/// </summary>
static class SettlerFigureShift
{
    /// <summary>The shift of <paramref name="pick"/> by the <paramref name="shifts"/> of the figure entries.</summary>
    public static (int X, int Y) Of(SettlerPick pick, IReadOnlyDictionary<int, (int X, int Y)>? shifts) =>
        pick.ShiftedBy >= 0 && shifts is not null ? shifts.GetValueOrDefault(pick.ShiftedBy) : default;
}

/// <summary>
/// The terms of the settlers as the figure tables read them, with no position: the part of the map view's placement
/// (<c>SettlerFigures</c>) that the picks depend on. A term holds the settler's job, activity, heading, frame, the time left of
/// its step, player, and carried good; a carrier that waits off its node holds the node's facts and the game's clock instead.
/// The History Edition source cannot reach the map view (INV-0012), so its tests build the terms from the domain's settler here;
/// the map view's own tests check the real placement, and the launch tests check both together through public types.
/// </summary>
static class SettlerTerms
{
    /// <summary>The terms of <paramref name="settlers"/> at the game's <paramref name="clock"/>, the waiting carriers' last.</summary>
    public static List<SettlerDraw> Of(IWalkSteps steps, Map map, IEnumerable<Settler> settlers, ushort clock)
    {
        var terms = new List<SettlerDraw>();
        var waiting = new List<SettlerDraw>();
        foreach (var settler in settlers)
        {
            if (settler.Job is Carrier && settler.Activity is WaitsOffRoad)
            {
                var node = map.At(settler.X, settler.Y);
                waiting.AddRange(
                    Parts(
                        settler,
                        step: clock,
                        heading: null,
                        carried: Good.None,
                        new WaitingNode
                        {
                            Column = settler.X,
                            LakeMark = node.LakeMark,
                            Roads = node.Roads,
                        }
                    )
                );
                continue;
            }

            if (!settler.Activity.IsDrawn)
            {
                continue;
            }

            // The time left runs down with the clock since the settler's stamp, to 0 at the least.
            var elapsed = (ushort)(clock - settler.Stamp);
            var left = settler.Timer >= elapsed ? settler.Timer - elapsed : 0;
            if (steps.At(settler.Frame, left) is null)
            {
                continue;
            }

            terms.AddRange(
                Parts(
                    settler,
                    left,
                    WalkingActivity.HeadingOf(settler.Activity),
                    WalkingActivity.LoadOf(settler.Activity)?.Good ?? Good.None,
                    node: null
                )
            );
        }

        terms.AddRange(waiting);

        return terms;
    }

    /// <summary>The picks the tables make for <paramref name="terms"/>, in order; a term with no pick has none.</summary>
    public static List<SettlerPick> Picks(SettlerFigureTables tables, IEnumerable<SettlerDraw> terms) =>
        [.. terms.Select(term => tables.Pick(term)).OfType<SettlerPick>()];

    /// <summary>The entries the tables pick for <paramref name="terms"/>, in order.</summary>
    public static int[] Entries(SettlerFigureTables tables, IEnumerable<SettlerDraw> terms) =>
        [.. Picks(tables, terms).Select(pick => pick.Entry)];

    static IEnumerable<SettlerDraw> Parts(
        Settler settler,
        int step,
        Heading? heading,
        Good carried,
        WaitingNode? node
    ) =>
        new[] { SettlerPart.Figure, SettlerPart.Overlay, SettlerPart.SecondFigure }.Select(part => new SettlerDraw
        {
            Row = 0,
            X = 0,
            Y = 0,
            Part = part,
            Job = settler.Job,
            Activity = settler.Activity,
            Heading = heading,
            Frame = settler.Frame,
            Step = step,
            Player = settler.Player,
            Carried = carried,
            Node = node,
        });
}
