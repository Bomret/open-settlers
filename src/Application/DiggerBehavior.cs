using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The digger's work (<c>docs/rules/digging.md</c>): the start routine of job 2 at the large site's door (<c>0x43cd9d</c>) and the turns of
/// state 8 (<c>0x43e45e</c>). He makes sixteen passes, from 15 down to 0; a pass visits the site's node and its six neighbours, the east
/// neighbour first, then south-east, south, west, north-west, north, and last the site's node (the first pass leaves the east one out). A
/// node whose height is the height the pass names (the site's height plus 1 to 8 in a pass of an odd number, minus 1 to 8 in an even one:
/// 15 is +8, 14 is -8, 13 is +7, and so on down to 1 for +1 and 0 for -1) is worked: he steps onto it, digs once, which lowers an odd
/// pass's node and raises an even one's by one, and steps back onto the site's node. After the last pass the site's progress word is
/// 1, the worker bits go, and he steps onto the flag and goes home. A node more than eight from the site's height is never named. **Not
/// ported:** the cursor's repaint that marks the changed node (<c>0x45086a</c>) and the branch of a player whose block byte <c>+0x163</c>
/// has bit 6 set (<c>0x42d4e9</c>, <c>SitePass</c>).
/// </summary>
/// <param name="movement">The settlers' ground: the clock, the timer, the nodes, and the tables.</param>
/// <param name="buildings">The building table: the site he works in.</param>
/// <param name="history">The history the behaviour records its changes in (no rule reads it).</param>
/// <param name="stepOut">The step from the building onto its flag (the turn of state 7), which his end runs at once.</param>
public sealed class DiggerBehavior(
    SettlerMovement movement,
    BuildingTable buildings,
    GameHistory history,
    Action<int, Settler> stepOut
)
{
    // The first pass and the first node of a pass; the start routine gives them (0x43cdb2, 0x43ce0d), and phase 1 (the next node).
    private const int FirstPass = 15;
    private const int FirstNode = 6;
    private const int NextNodePhase = 1;

    // The phases of byte 14.
    private const int WaitPhase = 0;
    private const int DigPhase = 1;
    private const int StepPhase = 3;

    // The frame of the digger waiting for a busy node is 0x57 less the node's index (0x43e7b4), and the timer he waits on is 0x7f.
    private const int WaitFrame = 0x57;
    private const int WaitTimer = 0x7f;

    // The frames of the dig, 0x57 where the pass lowers a node and 0x58 where it raises one, and the time of one dig (0x43e7f9).
    private const int LowerFrame = 0x57;
    private const int RaiseFrame = 0x58;
    private const int DigTime = 0x17f;

    // A step between two nodes takes the frame 4 + 9 * direction plus the difference of the heights (0x43ebac, 0x43ebb3).
    private const int StepFrameBase = 4;
    private const int StepFrameSpan = 9;

    // The tallest height a node can have: the mask 0x1f of the height byte.
    private const int HeightMask = 0x1f;

    private const int ByteMask = 0xff;
    private const int DirectionCount = 6;

    /// <summary>Registers the behaviour of the digger's activity.</summary>
    public void Register(ActivityRegistry registry) =>
        registry.Register<Digs>((slot, settler, _) => Turn(slot, settler));

    /// <summary>
    /// The start routine of job 2 (<c>0x43cd9d</c>), run where the digger has stepped through the door of the site's node: he takes state 8
    /// with the first pass, the site's height, the first node, and the phase of the next node. His frame, timer, and stamp stay as the
    /// step through the door left them.
    /// </summary>
    public void Start(int slot, Settler settler, Building site) =>
        movement.Become(settler, new Digs(FirstPass, site.Site?.TargetHeight ?? 0, FirstNode, NextNodePhase));

    // State 8: the time since the last turn comes off the timer; when it has run out the digger works until the timer is positive again.
    private void Turn(int slot, Settler settler)
    {
        if (movement.TimerRan(settler))
        {
            while (Step(slot, settler)) { }
        }

        if (settler.Activity is Digs)
        {
            history.RecordSettler(slot, EventType.Digs, Cause.Settlers);
        }
    }

    // The loop of the work (0x43e496), at the point after the timer ran out: the phase falls by one, and true is returned where the
    // work goes on at once.
    private bool Step(int slot, Settler settler)
    {
        var digs = (Digs)settler.Activity;
        var before = digs.Phase;
        var phase = (before - 1) & ByteMask;
        settler.Activity = digs with { Phase = phase };
        if (before == WaitPhase)
        {
            // The node of the pass was busy: he looks again, from the node he stands on (0x43e4a3).
            return StepOnto(slot, settler, digs.Node);
        }

        if (phase == 0)
        {
            return NextNode(slot, settler, digs);
        }

        return phase == DigPhase ? Dig(slot, settler, digs) : Animate(settler, digs.Pass);
    }

    // 0x43e62d: the next node of the pass; after the site's node the next pass starts at the first node.
    private bool NextNode(int slot, Settler settler, Digs digs) =>
        digs.Node >= 1 ? Visit(slot, settler, digs with { Node = digs.Node - 1 }) : NextPass(slot, settler, digs);

    // 0x43e63a: the next pass; after the last the digger is done.
    private bool NextPass(int slot, Settler settler, Digs digs)
    {
        if (digs.Pass < 1)
        {
            Finish(slot, settler);

            return false;
        }

        return Visit(slot, settler, digs with { Pass = digs.Pass - 1, Node = FirstNode });
    }

    // 0x43eb4a: the height the pass names (the byte of the site's height plus the pass's delta; a sum that is not a height skips the pass),
    // and whether the node stands at it.
    private bool Visit(int slot, Settler settler, Digs digs)
    {
        settler.Activity = digs;
        var goal = (digs.Target + PassDelta(digs.Pass)) & ByteMask;
        if (goal > HeightMask)
        {
            return NextPass(slot, settler, digs);
        }

        var here = movement.Map.Index(settler.X, settler.Y);
        if (digs.Node == 0)
        {
            if (movement.Height(here) != goal)
            {
                return NextNode(slot, settler, digs);
            }

            settler.Activity = digs with { Phase = 2 };

            return Animate(settler, digs.Pass);
        }

        var (_, _, next) = movement.Neighbour(settler, DirectionOf(digs.Node));
        if (movement.Height(next) != goal)
        {
            return NextNode(slot, settler, digs);
        }

        if (movement.Occupant(next) != 0)
        {
            Wait(settler, digs);

            return false;
        }

        return StepOnto(slot, settler, digs.Node);
    }

    // The pass's delta to the site's height (0x43ebba): -1, +1, -2, +2, and so on for the passes 0, 1, 2, 3, ..., 15.
    private static int PassDelta(int pass) => (pass & 1) != 0 ? (pass + 1) / 2 : -((pass / 2) + 1);

    // The direction from the site's node to the node 6 (east, 0), 5 (south-east), 4 (south), 3 (west), 2 (north-west), 1 (north).
    private static int DirectionOf(int node) => FirstNode - node;

    // The digger waits for the busy node: frame 0x57 less the node's index, a timer of 0x7f, and the phase 0 (0x43e7ac).
    private void Wait(Settler settler, Digs digs)
    {
        settler.Activity = digs with { Phase = WaitPhase };
        settler.Frame = (WaitFrame - digs.Node) & ByteMask;
        settler.Timer = WaitTimer;
    }

    // The step onto the node of the pass (0x43eadc): the free node takes him, the frame is the direction's base plus the difference of the
    // heights, the phase is 3, and the time of the step goes onto the timer. False where that time ends his turn.
    private bool StepOnto(int slot, Settler settler, int node)
    {
        var direction = node == 0 ? DirectionCount - 1 : DirectionOf(node);
        var (x, y, next) = movement.Neighbour(settler, direction);
        if (movement.Occupant(next) != 0)
        {
            Wait(settler, (Digs)settler.Activity);

            return false;
        }

        var digs = (Digs)settler.Activity;
        settler.Activity = digs with { Phase = StepPhase };

        return Walk(slot, settler, x, y, next, direction);
    }

    private bool Walk(int slot, Settler settler, int x, int y, int next, int direction)
    {
        var from = movement.Map.Index(settler.X, settler.Y);
        var climb = movement.Height(next) - movement.Height(from);
        movement.Move(slot, settler, x, y, next);
        settler.Frame = (climb + StepFrameBase + (StepFrameSpan * direction)) & ByteMask;

        return AddTime(settler, movement.Tables.StepTime(settler.Frame));
    }

    // The time of a step or of a dig goes onto the timer; the work goes on at once while the sum does not carry out of the word.
    private static bool AddTime(Settler settler, int time)
    {
        var old = settler.Timer;
        settler.Timer = (ushort)(old + time);

        return settler.Timer >= old;
    }

    // 0x43e7da: the dig's figure, a lowering one for an odd pass and a raising one for an even pass.
    private static bool Animate(Settler settler, int pass)
    {
        settler.Frame = (pass & 1) != 0 ? LowerFrame : RaiseFrame;

        return AddTime(settler, DigTime);
    }

    // 0x43e81e: the node he stands on changes by one, and he steps back onto the site's node (a node of the pass other than the site's
    // own). The site's node itself he leaves where he is.
    private bool Dig(int slot, Settler settler, Digs digs)
    {
        ref var node = ref movement.Map.At(settler.X, settler.Y);
        node.Height += (digs.Pass & 1) != 0 ? -1 : 1;
        if (digs.Node == 0)
        {
            return true;
        }

        var back = (DirectionOf(digs.Node) + (DirectionCount / 2)) % DirectionCount;
        var (x, y, next) = movement.Neighbour(settler, back);

        return Walk(slot, settler, x, y, next, back);
    }

    // 0x43e64f: the site starts, the digger's worker bit and slot go from it, and he steps onto the flag to go home (the step of
    // state 7, 0x43e02e): a walk to the storage, as the builder's end leaves it.
    private void Finish(int slot, Settler settler)
    {
        if (movement.BuildingAt(movement.Map.Index(settler.X, settler.Y)) is { } site)
        {
            site.Progress = 1;
            site.WorkerInside = false;
            site.Worker = 0;
            history.RecordBuilding(
                buildings.SlotOf(site),
                EventType.Levelled,
                Cause.Settlers,
                history.SettlerId(slot).ToString()
            );
        }

        settler.Activity = new StepsOntoFlag(new Walks(new ToStorage(), new Heading(0)));
        stepOut(slot, settler);
    }
}
