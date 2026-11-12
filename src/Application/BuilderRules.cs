using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The builder's work (<c>docs/rules/construction.md</c>): the start routine of job 3 at the building site's door (<c>0x43ce20</c>), the
/// turns of state 9 (<c>0x43ec82</c>) in which he takes the site's materials in the order of the type's order word and makes eight blows
/// for each, every blow adding a step to the site's progress word, and the end of the building (<c>0x43ef20</c>), after which state 0x4a
/// (<c>0x43e008</c>) lets him step onto the flag and go home. A blow needs the material on site: he waits for it, a quarter of a step
/// at a time. **Not ported:** the player's counts of buildings and the call of <c>0x45f402</c> for the types 0xb, 0x15, and 0x16, the
/// buildings of a mission's start. It is the rules module of the builder job (<see cref="JobRules{TJob}"/>) and takes the movement, the
/// building table (the site he works in), the flag table (the finished building's flag stops asking for materials), the history, the random
/// generator (each blow draws one value that picks the figure), and the step out (which the end of the building runs at once) from the
/// <see cref="JobParts"/>.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class BuilderRules(JobParts parts) : JobRules<Builder>(parts)
{
    private readonly SettlerMovement movement = parts.Movement;
    private readonly BuildingTable buildings = parts.Buildings;
    private readonly FlagTable flags = parts.Flags;
    private readonly GameHistory history = parts.History;
    private readonly IRandomDraws draws = parts.Draws;
    private readonly HutWork hut = parts.Hut;

    // The types whose builder takes the second figure: the mask 0x4a0400 tests the type's bit (0x43ceb4).
    private const int SecondFigureTypes = 0x4a0400;

    // The frame of the builder looking for a material (0x62, and 0x64 for the second figure), the frame of a blow (0x66 to 0x69 by the
    // draw's low two bits, four more for the second figure), and the timer the start routine gives.
    private const int FirstFigureFrame = 0x62;
    private const int SecondFigureFrame = 0x64;
    private const int BlowFrame = 0x66;
    private const int StartTimer = 0x7f;

    // The blows of one material.
    private const int BlowsPerMaterial = 8;

    // The fill counters hold the goods on site in their high nibble.
    private const int OnSite = 0xf0;
    private const int OneOnSite = 0x10;

    // The wait for a material: a quarter of a step, which sets the timer to 0xff where it would not carry (0x43ee01).
    private const int WaitStep = 0x100;
    private const int WaitTimer = 0xff;

    // The settler's node and the site's flag lie one step apart, south-east (direction 1).
    private const int ToFlag = 1;

    /// <summary>The builder is the worker a site asks for when it starts (<c>0x42d5fc</c>).</summary>
    public override WorkerRequest? SiteWorker(SiteStage stage, RuleTables tables) =>
        stage == SiteStage.Building ? tables.Get<WorkerRequest>(new Builder()) : null;

    /// <summary>Registers the behaviour of the builder's activities.</summary>
    public override void Register(ActivityRegistry registry)
    {
        registry.Register<Builds>((slot, settler, _) => Turn(slot, settler));
        registry.Register<LeavesSite>((slot, settler, _) => Leave(slot, settler));
    }

    /// <summary>
    /// The start routine of job 3 (<c>0x43ce20</c>), run where the builder has stepped through the door of the site's node: he takes state 9
    /// with the frame of his figure, a timer of 0x7f, the site's slot, and no material taken yet. He stays on the node.
    /// </summary>
    public override void Start(int slot, Settler settler, Building site)
    {
        var secondFigure = (SecondFigureTypes >> site.Type.Code & 1) != 0;
        movement.Become(settler, new Builds(buildings.SlotOf(site), false, 0, secondFigure, 0));
        settler.Frame = secondFigure ? SecondFigureFrame : FirstFigureFrame;
        settler.Timer = StartTimer;
    }

    // State 9 (0x43ec82): the time since the last turn comes off the timer; when it has run out the builder works until the timer is
    // positive again.
    private void Turn(int slot, Settler settler)
    {
        var site = ((Builds)settler.Activity).Site;
        if (movement.TimerRan(settler))
        {
            Work(slot, settler);
        }

        if (settler.Activity is Builds)
        {
            history.RecordSettler(slot, EventType.Builds, Cause.Settlers, history.BuildingId(site).ToString());
            history.RecordBuilding(site, EventType.Builds, Cause.Settlers, history.SettlerId(slot).ToString());
        }
    }

    // The builder's loop (0x43ecba): a blow where he is making them, else the next material; after the blow or the material a figure
    // is picked and the time of its step goes onto the timer, and he goes on while that does not run the timer over.
    private void Work(int slot, Settler settler)
    {
        while (true)
        {
            var builds = (Builds)settler.Activity;
            var site = buildings[builds.Site];
            if (builds.Blowing)
            {
                if (!Blow(slot, settler, site, builds))
                {
                    return;
                }

                builds = (Builds)settler.Activity;
                if (builds.Blows == 0 && !TakeMaterial(settler, site, builds))
                {
                    return;
                }
            }
            else if (!TakeMaterial(settler, site, builds))
            {
                return;
            }

            if (!Animate(settler))
            {
                return;
            }
        }
    }

    // One blow (0x43ee1b): the step of the type's table, before or after the word reaches 0x8000, is added to the site's progress word. A
    // carry out of the word finishes the building; false then.
    private bool Blow(int slot, Settler settler, Building site, Builds builds)
    {
        var sum = site.Progress + movement.Tables.BlowStep(site.Type, site.Progress >= 0x8000);
        site.Progress = (ushort)sum;
        if (sum > ushort.MaxValue)
        {
            Finish(slot, settler, site);

            return false;
        }

        settler.Activity = builds with { Blows = builds.Blows - 1 };

        return true;
    }

    // The next material (0x43ecca, 0x43ecf6): the order word says plank or stone; one on site (the high nibble of the fill byte) goes out
    // of the fill and out of the count the site still needs, and eight blows follow. With none on site the timer gains a quarter of a
    // step (0xff where that would not carry) and the builder waits; false then.
    private bool TakeMaterial(Settler settler, Building site, Builds builds)
    {
        settler.Frame = builds.SecondFigure ? SecondFigureFrame : FirstFigureFrame;
        var needs = site.Site!;
        if (movement.Tables.StoneAt(site.Type, builds.Materials))
        {
            if ((site.FillHigh & OnSite) == 0)
            {
                return Wait(settler, builds with { Blowing = false });
            }

            site.SetFill(FillCounter.High, site.FillHigh - OneOnSite);
            site.Site = needs with { Stones = needs.Stones - 1 & 0xff };
        }
        else
        {
            if ((site.FillLow & OnSite) == 0)
            {
                return Wait(settler, builds with { Blowing = false });
            }

            site.SetFill(FillCounter.Low, site.FillLow - OneOnSite);
            site.Site = needs with { Planks = needs.Planks - 1 & 0xff };
        }

        settler.Activity = builds with { Blowing = true, Materials = builds.Materials + 1, Blows = BlowsPerMaterial };

        return true;
    }

    private bool Wait(Settler settler, Builds builds)
    {
        settler.Activity = builds;
        var sum = settler.Timer + WaitStep;
        settler.Timer = sum > ushort.MaxValue ? (ushort)sum : (ushort)WaitTimer;

        return false;
    }

    // The figure of the blow (0x43eebd): the generator's next value picks one of four frames, and the time of that frame's step goes onto
    // the timer. False where the sum carries out of the timer: the builder's turn ends there.
    private bool Animate(Settler settler)
    {
        var builds = (Builds)settler.Activity;
        settler.Frame = BlowFrame + (draws.Draw() & 3) + (builds.SecondFigure ? 4 : 0);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + movement.Tables.StepTime(settler.Frame));

        return settler.Timer >= old;
    }

    // The end of the building (0x43ef20): the site's progress word, the worker bit, the worker's slot, and the construction bit go, the
    // site's flag asks for nothing, and the builder takes state 0x4a with no timer and goes on at once.
    private void Finish(int slot, Settler settler, Building site)
    {
        var siteSlot = buildings.SlotOf(site);
        site.Progress = 0;
        site.WorkerInside = false;
        site.Worker = 0;
        site.Constructing = false;
        site.Site = null;
        var flag = flags[site.Flag];
        flag.SetRequest(RequestWord.First, flag.RequestOf(RequestWord.First) with { Asks = null });
        flag.SetRequest(RequestWord.Second, flag.RequestOf(RequestWord.Second) with { Asks = null });

        movement.Become(settler, new LeavesSite());
        settler.Timer = 0;
        history.RecordBuilding(
            siteSlot,
            EventType.BuildingFinished,
            Cause.Settlers,
            history.SettlerId(slot).ToString()
        );
        history.RecordFlag(
            site.Flag,
            EventType.BuildingFinished,
            Cause.Settlers,
            history.BuildingId(siteSlot).ToString()
        );
        Leave(slot, settler);
    }

    // State 0x4a (0x43e008): when the node of the flag holds no settler the builder takes the step onto it (the turn of state 7, which the
    // original's code falls into) and goes home along the roads (state 2, mode 0xfe, to the castle).
    private void Leave(int slot, Settler settler)
    {
        var (_, _, next) = movement.Neighbour(settler, ToFlag);
        if (movement.Occupant(next) == 0)
        {
            settler.Activity = new StepsOntoFlag(new Walks(new ToStorage(), new Heading(0)));
            hut.StepOut(slot, settler);

            return;
        }

        history.RecordSettler(slot, EventType.LeavesSite, Cause.Settlers);
    }
}
