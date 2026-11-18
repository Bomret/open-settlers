using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain.Testing;

/// <summary>
/// The settlers the figure tests of the map view and of the History Edition source draw: a woodcutter in the activity of a
/// state of the original, a carrier with a good, the fisher, and the activities that go with them. Both test projects read the
/// same settlers, so the term check and the pick check of one scenario see the same data.
/// </summary>
public static class SettlerCases
{
    /// <summary>The activity the original's state code stands for, with the data the drawing reads: no good carried, heading 0.</summary>
    public static Activity ActivityOf(int state) =>
        state switch
        {
            1 => new WaitsInCastle(),
            2 => new Walks
            {
                Errand = new ToStorage(),
                Heading = new Heading { Value = 0 },
            },
            3 => new WalksRoad
            {
                Load = null,
                Heading = new Heading { Value = 0 },
            },
            4 => new Door { TimeLeft = 0 },
            5 => new WaitsOutStep
            {
                Then = new Walks
                {
                    Errand = new ToStorage(),
                    Heading = new Heading { Value = 0 },
                },
            },
            0xe => new HandsOver
            {
                Load = null,
                Heading = new Heading { Value = 0 },
            },
            0x10 => new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = new Vector(0, 0),
                    Purpose = new BackToHut { Carrying = false },
                },
            },
            0x11 => new Cuts
            {
                Back = new Vector(0, 0),
                SecondVariant = false,
                Count = 0,
                Memory = 0,
            },
            0x12 => new LooksForTree(),
            0x19 => new SearchesHome(),
            0x1c => new ClaimsHomeNode(),
            0x42 => new WaitsAtFlag
            {
                Flag = 0,
                Direction = 0,
                Approach = new Heading { Value = 0 }.Retried,
            },
            0x43 => new ClaimsNode
            {
                Heading = new Heading { Value = 0 },
                Approach = new Heading { Value = 0 }.Retried,
            },
            0x44 => new WaitsToGoHome { Approach = new Heading { Value = 0 }.Retried },
            0x45 => new WaitsToRejoin { Approach = new Heading { Value = 0 }.Retried },
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    /// <summary>A carrier in the activity of a state at the node (4, 3); on its road or handing a good over it carries the good <paramref name="mode"/>, none for 0.</summary>
    public static Settler Carrier(int mode, int state = 3, int timer = 0)
    {
        var carried = mode == 0 ? null : new Load?(new Load { Good = TestCatalog.GoodOf(mode), Destination = null });
        var activity = state switch
        {
            3 => new WalksRoad
            {
                Load = carried,
                Heading = new Heading { Value = 0 },
            },
            0xe => new HandsOver
            {
                Load = carried,
                Heading = new Heading { Value = 0 },
            },
            _ => ActivityOf(state),
        };

        return new(new Carrier(), 0, 4, 3, 0, activity) { Frame = 7, Timer = (ushort)timer };
    }

    /// <summary>A woodcutter at the node (4, 3) in the activity of a state, in a frame with the time left.</summary>
    public static Settler Walker(int frame, int timer, int state = 2) =>
        new(new Woodcutter(), 0, 4, 3, 0, ActivityOf(state))
        {
            Frame = frame,
            Timer = (ushort)timer,
            Stamp = 0,
        };

    /// <summary>A fisher at the node (4, 3) in a frame, walking unless given another activity.</summary>
    public static Settler Hut(int frame, Activity? activity = null) =>
        new(new Fisher(), 0, 4, 3, 0, activity ?? ActivityOf(2)) { Frame = frame };

    /// <summary>The free walk back to the hut's flag, with the log in the woodcutter's hands or without it.</summary>
    public static Activity WayBack(bool carrying) =>
        new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(0, 0),
                Purpose = new BackToHut { Carrying = carrying },
            },
        };

    /// <summary>The fisher's cast, with a fish caught or not.</summary>
    public static Activity Cast(bool caught) =>
        new Fishes
        {
            Back = new Vector(0, 0),
            Turns = 1,
            Caught = caught,
            Casts = 1,
        };

    /// <summary>
    /// The jobs the History Edition's figure tables hold a handler for (<c>0x43f744</c> plus 8 for each job): the jobs a
    /// graphics source draws a figure for. The History Edition tests pin the list to the tables and the content set's tests to
    /// the content set.
    /// </summary>
    public static IReadOnlyList<Job> JobsWithAFigureHandler { get; } =
    [
        new Carrier(),
        new Woodcutter(),
        new Stonecutter(),
        new Digger(),
        new Builder(),
        new Forester(),
        new Sawmiller(),
        new Miller(),
        new Fisher(),
        new Farmer(),
        new Baker(),
        new Butcher(),
        new PigFarmer(),
    ];
}
