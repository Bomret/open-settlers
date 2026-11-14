namespace OpenSettlers.Simulation;

/// <summary>The fields of the original's settler item that the typed settler does not hold as they are (bytes 10 to 15, and the stamp).</summary>
public readonly record struct SettlerFields
{
    /// <summary>Byte 10, the state.</summary>
    public required int State { get; init; }

    /// <summary>Byte 11, what the settler walks to, or the good a carrier carries.</summary>
    public required int Mode { get; init; }

    /// <summary>The word at 12: the flag the settler walks to, a good's destination, the door's time, or a vector.</summary>
    public required int Target { get; init; }

    /// <summary>Byte 14: the direction back, a storage's slot, or a walk's count.</summary>
    public required int Heading { get; init; }

    /// <summary>Byte 15: the state after a step's wait, a walk's memory, or a count.</summary>
    public required int Next { get; init; }

    /// <summary>The word at 8; for the carrier that waits next to a flag its low byte holds the approach.</summary>
    public required ushort Stamp { get; init; }
}

/// <summary>
/// The exporter of the typed settler: it writes the original's state, mode, target, heading, and next (bytes 10 to 15 of the
/// settler's item) and the stamp, field for field, for the digest, the event snapshots, and the readouts (ADR 0013). The
/// original shares those four fields between its states; each activity here writes the ones it uses from its own data, and the
/// others from its <see cref="Residue"/>, which the rules never name: only this class and <c>Become</c> handle it.
/// </summary>
public static class SettlerEncoder
{
    const int StorageMode = 0xfe;
    const int BuildingMode = 0xff;
    const int WayBackMark = 0x80;
    const int BlowingMode = 0xff;
    const int SearchingMode = 1;
    const int SecondFigureBit = 0x80;
    const int WalksState = 2;
    const int PutsDownState = 0xd;

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

        return new SettlerFields
        {
            State = raw.State,
            Mode = raw.Mode,
            Target = raw.Target,
            Heading = raw.Heading,
            Next = raw.Next,
            Stamp = stamp,
        };
    }

    /// <summary>The bytes the activity leaves as they are when the settler takes up another activity.</summary>
    internal static Residue ResidueOf(Activity activity)
    {
        var raw = Of(activity);

        return new Residue
        {
            Mode = raw.Mode,
            Target = raw.Target,
            Heading = raw.Heading,
            Next = raw.Next,
        };
    }

    /// <summary>The bytes the settler's activity leaves as they are when the settler takes up another activity.</summary>
    public static Residue ResidueOf(Settler settler) => ResidueOf(settler.Activity);

    /// <summary>The word the original's settler item holds for a good's destination flag: the flag's slot plus one, 0 for none.</summary>
    internal static int DestinationWord(int? destination) => destination is { } slot ? slot + 1 : 0;

    readonly record struct Raw
    {
        public required int State { get; init; }

        public required int Mode { get; init; }

        public required int Target { get; init; }

        public required int Heading { get; init; }

        public required int Next { get; init; }
    }

    static Raw Of(Activity activity)
    {
        var r = activity.Residue;

        return activity switch
        {
            WaitsInCastle a => new()
            {
                State = a.Code,
                Mode = r.Mode,
                Target = r.Target,
                Heading = a.Storage,
                Next = r.Next,
            },
            Walks a => new()
            {
                State = a.Code,
                Mode = ModeOf(a.Errand),
                Target = TargetOf(a.Errand),
                Heading = a.Heading.Value,
                Next = a.Tries,
            },
            WalksRoad a => new()
            {
                State = a.Code,
                Mode = a.Load?.Good.Code ?? 0,
                Target = a.Load is { } load ? DestinationWord(load.Destination) : r.Target,
                Heading = a.Heading.Value,
                Next = a.Marked ? 0xff : a.Waits,
            },
            Door a => new()
            {
                State = a.Code,
                Mode = a.EnteringStorage ? StorageMode : r.Mode,
                Target = a.TimeLeft,
                Heading = r.Heading,
                Next = r.Next,
            },
            WaitsOutStep a => Of(a.Then) with { State = a.Code, Next = a.Then.Code },
            EntersBuilding a => new()
            {
                State = a.Code,
                Mode = a.Storage ? StorageMode : r.Mode,
                Target = r.Target,
                Heading = r.Heading,
                Next = r.Next,
            },
            StepsOntoFlag a => Of(a.Then) with { State = a.Code, Next = a.Then.Code },
            Builds a => new()
            {
                State = a.Code,
                Mode = a.Blowing ? BlowingMode : SearchingMode,
                Target = a.Site,
                Heading = a.Materials | (a.SecondFigure ? SecondFigureBit : 0),
                Next = a.Blows,
            },
            LeavesSite a => new()
            {
                State = a.Code,
                Mode = StorageMode,
                Target = 0,
                Heading = 0,
                Next = WalksState,
            },
            // The digger's work: the pass in byte 11, the site's height and the node of the pass in the word at 12, the phase in byte 14.
            Digs a => new()
            {
                State = a.Code,
                Mode = a.Pass,
                Target = a.Target | (a.Node << 8),
                Heading = a.Phase,
                Next = r.Next,
            },
            // The sawmiller's work: byte 11 is 0xff while he saws and 0 while he waits for a log; with the plank in his hands it holds the
            // good, the word at 12 is 0, and byte 15 is the state he lays it down in.
            Saws a => new()
            {
                State = a.Code,
                Mode = a.Sawing ? BlowingMode : 0,
                Target = r.Target,
                Heading = r.Heading,
                Next = r.Next,
            },
            StepsOutWithGood a => new()
            {
                State = a.Code,
                Mode = a.Good.Code,
                Target = 0,
                Heading = r.Heading,
                Next = PutsDownState,
            },
            PutsDownGood a => new()
            {
                State = a.Code,
                Mode = a.Good.Code,
                Target = 0,
                Heading = r.Heading,
                Next = r.Next,
            },
            HandsOver a => new()
            {
                State = a.Code,
                Mode = a.Load?.Good.Code ?? 0,
                Target = a.Load is { } load ? DestinationWord(load.Destination) : r.Target,
                Heading = a.Heading.Value,
                Next = a.Marked ? 0xff : 0,
            },
            Leaving a => new()
            {
                State = a.Code,
                Mode = ModeOf(a.Errand),
                Target = TargetOf(a.Errand),
                Heading = r.Heading,
                Next = r.Next,
            },
            WalksFree a => OfTrip(a.Code, a.Trip),
            KnightWalks a => OfTrip(a.Code, a.Trip),
            Cuts a => new()
            {
                State = a.Code,
                Mode = a.Back.East & 0xff,
                Target = ((a.SecondVariant ? 0xff : 0) << 8) | (a.Back.South & 0xff),
                Heading = a.Count,
                Next = a.Memory,
            },
            // The stonecutter's cut: the way back in bytes 11 and 12, the part of the cut in byte 13, the quarter in the word at 14.
            CutsStone a => new()
            {
                State = a.Code,
                Mode = a.Back.East & 0xff,
                Target = (a.Phase << 8) | (a.Back.South & 0xff),
                Heading = a.Quarter & 0xff,
                Next = a.Quarter >> 8,
            },
            // The forester's wait: the way back in bytes 11 and 12 (and again, as the arrival left it, in byte 13 and the next word's
            // low byte), byte 14 not 0 once the sapling is down, and the walk's memory in byte 15.
            Plants a => new()
            {
                State = a.Code,
                Mode = a.Back.East & 0xff,
                Target = ((a.Back.East & 0xff) << 8) | (a.Back.South & 0xff),
                Heading = a.Planted ? 0xff : 0,
                Next = a.Memory,
            },
            SearchesHome a => new()
            {
                State = a.Code,
                Mode = a.FarEndFirst ? a.FarEndMark : 0,
                Target = r.Target,
                Heading = r.Heading,
                Next = r.Next,
            },
            WaitsAtFlag a => new()
            {
                State = a.Code,
                Mode = a.Direction.Value,
                Target = a.Flag,
                Heading = r.Heading,
                Next = r.Next,
            },
            ClaimsNode a => new()
            {
                State = a.Code,
                Mode = r.Mode,
                Target = r.Target,
                Heading = a.Heading.Value,
                Next = r.Next,
            },
            _ => new()
            {
                State = activity.Code,
                Mode = r.Mode,
                Target = r.Target,
                Heading = r.Heading,
                Next = r.Next,
            },
        };
    }

    static int ModeOf(Errand errand) =>
        errand switch
        {
            ToBuilding => BuildingMode,
            ToRoad road => road.Direction.Value,
            _ => StorageMode,
        };

    static int TargetOf(Errand errand) => errand.Flag;

    // A free walk: the vector's east steps in the mode and its south steps in the low byte of the target, the way-back mark in
    // the high byte of the target and the way back's south steps (or what the walk is for) in the heading.
    static Raw OfTrip(int state, Trip trip)
    {
        var (mark, heading) = trip.Purpose switch
        {
            ToTree tree => (tree.Back.East & 0xff, tree.Back.South),
            ToHeap heap => (heap.Back.East & 0xff, heap.Back.South),
            ToSpot spot => (spot.Back.East & 0xff, spot.Back.South),
            BackToHut back => (WayBackMark, back.Carrying ? 1 : 0),
            _ => (WayBackMark, -1),
        };

        return new Raw
        {
            State = state,
            Mode = trip.Remaining.East & 0xff,
            Target = (mark << 8) | (trip.Remaining.South & 0xff),
            Heading = heading,
            Next = trip.Memory,
        };
    }
}
