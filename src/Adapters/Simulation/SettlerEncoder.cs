namespace OpenSettlers.Simulation;

/// <summary>The fields of the original's settler item that the typed settler does not hold as they are (bytes 10 to 15, and the stamp).</summary>
/// <param name="State">Byte 10, the state.</param>
/// <param name="Mode">Byte 11, what the settler walks to, or the good a carrier carries.</param>
/// <param name="Target">The word at 12: the flag the settler walks to, a good's destination, the door's time, or a vector.</param>
/// <param name="Heading">Byte 14: the direction back, a storage's slot, or a walk's count.</param>
/// <param name="Next">Byte 15: the state after a step's wait, a walk's memory, or a count.</param>
/// <param name="Stamp">The word at 8; for the carrier that waits next to a flag its low byte holds the approach.</param>
public readonly record struct SettlerFields(int State, int Mode, int Target, int Heading, int Next, ushort Stamp);

/// <summary>
/// The exporter of the typed settler: it writes the original's state, mode, target, heading, and next (bytes 10 to 15 of the
/// settler's item) and the stamp, field for field, for the digest, the event snapshots, and the readouts (ADR 0013). The
/// original shares those four fields between its states; each activity here writes the ones it uses from its own data, and the
/// others from its <see cref="Residue"/>, which the rules never name: only this class and <c>Become</c> handle it.
/// </summary>
public static class SettlerEncoder
{
    private const int StorageMode = 0xfe;
    private const int BuildingMode = 0xff;
    private const int WayBackMark = 0x80;

    /// <summary>The original's fields for the settler.</summary>
    public static SettlerFields Fields(Settler settler)
    {
        var raw = Of(settler.Activity);
        var stamp = settler.Stamp;
        if (settler.Activity is WaitsOffRoad { Approach: var approach })
        {
            // The carrier that waits next to a flag keeps the heading it stopped with in the low byte of its stamp, from
            // the state 0x42 to the states 0x43 to 0x45 its road's going leads it to.
            stamp = (ushort)((stamp & 0xff00) | (byte)approach.Value);
        }

        return new SettlerFields(raw.State, raw.Mode, raw.Target, raw.Heading, raw.Next, stamp);
    }

    /// <summary>The bytes the activity leaves as they are when the settler takes up another activity.</summary>
    internal static Residue ResidueOf(Activity activity)
    {
        var raw = Of(activity);

        return new Residue(raw.Mode, raw.Target, raw.Heading, raw.Next);
    }

    /// <summary>
    /// The search for a flag the settler takes up where its walk is turned home with only its state changed (<c>0x461b77</c>,
    /// <c>0x462153</c>): the original reads the byte 11 the walk left (not 0: the far end first), and the other bytes stay.
    /// </summary>
    internal static SearchesHome HomeSearchFrom(Activity activity)
    {
        var residue = ResidueOf(activity);

        return new SearchesHome(residue.Mode != 0, residue.Mode) { Residue = residue };
    }

    /// <summary>
    /// The search for a flag home that takes the place of the activity a step's wait leads to (<c>0x462153</c>): as
    /// <see cref="HomeSearchFrom"/>, and the wait's end clears the next byte.
    /// </summary>
    internal static SearchesHome HomeSearchAfterStep(Activity then)
    {
        var search = HomeSearchFrom(then);

        return search with
        {
            Residue = search.Residue with { Next = 0 },
        };
    }

    /// <summary>The bytes of a builder's item when it loses its job (<c>0x4617fd</c>): as it leaves them, with the mode byte cleared.</summary>
    internal static Residue ResidueOfBuilder(Settler settler) => ResidueOf(settler) with { Mode = 0 };

    /// <summary>The bytes the settler's activity leaves as they are when the settler takes up another activity.</summary>
    public static Residue ResidueOf(Settler settler) => ResidueOf(settler.Activity);

    /// <summary>The word the original's settler item holds for a good's destination flag: the flag's slot plus one, 0 for none.</summary>
    internal static int DestinationWord(int? destination) => destination is { } slot ? slot + 1 : 0;

    private readonly record struct Raw(int State, int Mode, int Target, int Heading, int Next);

    private static Raw Of(Activity activity)
    {
        var r = activity.Residue;

        return activity switch
        {
            WaitsInCastle a => new(a.Code, r.Mode, r.Target, a.Storage, r.Next),
            Walks a => new(a.Code, ModeOf(a.Errand), TargetOf(a.Errand), a.Heading.Value, a.Tries),
            WalksRoad a => new(
                a.Code,
                a.Load?.Good.Code ?? 0,
                a.Load is { } load ? DestinationWord(load.Destination) : r.Target,
                a.Heading.Value,
                a.Marked ? 0xff : a.Waits
            ),
            Door a => new(a.Code, a.EnteringStorage ? StorageMode : r.Mode, a.TimeLeft, r.Heading, r.Next),
            WaitsOutStep a => Of(a.Then) with { State = a.Code, Next = a.Then.Code },
            EntersBuilding a => new(a.Code, a.Storage ? StorageMode : r.Mode, r.Target, r.Heading, r.Next),
            StepsOntoFlag a => Of(a.Then) with { State = a.Code, Next = a.Then.Code },
            HandsOver a => new(
                a.Code,
                a.Load?.Good.Code ?? 0,
                a.Load is { } load ? DestinationWord(load.Destination) : r.Target,
                a.Heading.Value,
                a.Marked ? 0xff : 0
            ),
            Leaving a => new(a.Code, ModeOf(a.Errand), TargetOf(a.Errand), r.Heading, r.Next),
            WalksFree a => OfTrip(a.Code, a.Trip),
            KnightWalks a => OfTrip(a.Code, a.Trip),
            Cuts a => new(
                a.Code,
                a.Back.East & 0xff,
                ((a.SecondVariant ? 0xff : 0) << 8) | (a.Back.South & 0xff),
                a.Count,
                a.Memory
            ),
            SearchesHome a => new(a.Code, a.FarEndFirst ? a.FarEndMark : 0, r.Target, r.Heading, r.Next),
            WaitsAtFlag a => new(a.Code, a.Direction.Value, a.Flag, r.Heading, r.Next),
            ClaimsNode a => new(a.Code, r.Mode, r.Target, a.Heading.Value, r.Next),
            _ => new(activity.Code, r.Mode, r.Target, r.Heading, r.Next),
        };
    }

    private static int ModeOf(Errand errand) =>
        errand switch
        {
            ToBuilding => BuildingMode,
            ToRoad road => road.Direction.Value,
            _ => StorageMode,
        };

    private static int TargetOf(Errand errand) => errand.Flag;

    // A free walk: the vector's east steps in the mode and its south steps in the low byte of the target, the way-back mark in
    // the high byte of the target and the way back's south steps (or what the walk is for) in the heading.
    private static Raw OfTrip(int state, Trip trip)
    {
        var (mark, heading) = trip.Purpose switch
        {
            ToTree tree => (tree.Back.East & 0xff, tree.Back.South),
            BackToHut back => (WayBackMark, back.Carrying ? 1 : 0),
            _ => (WayBackMark, -1),
        };

        return new Raw(
            state,
            trip.Remaining.East & 0xff,
            (mark << 8) | (trip.Remaining.South & 0xff),
            heading,
            trip.Memory
        );
    }
}
