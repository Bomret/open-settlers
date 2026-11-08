using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// One sprite of a settler: the sprite entry, the palette index a colour overlay is filled with (-1 for a sprite drawn
/// as it is), and the position in map pixels.
/// </summary>
public readonly record struct SettlerDraw(int Row, int Entry, int Colour, int X, int Y);

/// <summary>
/// The tables the tile pass draws a waiting carrier with (<c>0x44d77e</c>): 16 phase words (<c>0x44d87d</c>), 128 figure words
/// (<c>0x44d89d</c>), and the x and y offsets, as signed bytes, for each value of a node's six road direction bits
/// (<c>0x674923</c>).
/// </summary>
public sealed record IdleCarrierTables(short[] Phases, short[] Figures, sbyte[] Offsets);

/// <summary>
/// Which sprites a walking settler is drawn with (<c>docs/rules/settlers.md</c>, "Drawing the walk"). The settler's frame
/// (the shape of its step, which holds the direction) picks a sequence of (figure byte, dx, dy) triples from entry 2 of the
/// graphics archive; the time left of the step, in ticks (8 clock units), picks the triple. The job's handler
/// (<c>0x43f744</c> plus 8 for each job) adds a constant to the figure byte to make the figure word; the high byte of the
/// word picks a pair of words of the figure bases and the low byte a pair of the figure offsets, which give the entries:
/// the figure, its overlay in the player's colour, and a second figure (<c>0x44fff3</c>, <c>docs/rules/settlers.md</c>).
/// <b>Confirmed</b> by the draw runs of <c>tests/OpenSettlers.Tests/settler-draws.txt</c> (the emulator verb <c>draw</c> runs
/// <c>0x42f4f1</c>, <c>0x43f537</c>, <c>0x44ffe7</c>, and <c>0x406610</c>), which the engine matches pass for pass for the carrier
/// (with each good), the woodcutter, and the settler of the type 1 hut: the sequence offsets count from the byte after the first dword of the entry (as <c>0x41e8e0</c> keeps its
/// table); the triple index is the time left shifted right 3; the time left falls with the clock between the settler's turns
/// (the view pass gives a settler in view a turn every pass, which runs its timer down by the clock units since its stamp); the
/// position is the node, lifted by 4 times its height and 2 more, plus the triple's offsets; the figure 1850 + i takes its
/// overlay from 2500 + i, filled with the player's colour; the second figure moves by the header bytes 0 and 1 of the figure,
/// read as signed bytes; a settler draws its figure, then the overlay, then the second figure; the record joins the row of the
/// settler plus the byte of its frame's row (<c>0x43f402</c>), and the rows are drawn from the top; a carrier that waits next to
/// a flag (state 0x42), a carrier told to go home (0x44, 0x45), a settler that claims its node (0x1c), and a settler at work
/// (0x12, 0x1f) have no record; the records of one row come in the order of the nodes, row by row and from the west.
/// <b>Inferred:</b> every state the recordings hold no settler of.
/// </summary>
public sealed class SettlerFigures
{
    // The sprite entry of the figures, of their colour overlays, and of the second figures (0x406610): the figure 1850 + i
    // has the overlay 2500 + i.
    public const int FigureBase = 1850;

    /// <summary>The sprite entry of the first colour overlay; the figures lie below it.</summary>
    public const int OverlayBase = 2500;
    private const int SecondBase = 3150;

    // The palette index of the first player's colour, and the step between players (0x406610).
    private const int ColourBase = 0x40;
    private const int ColourStep = 4;

    private const int Sequences = 200;

    // The record builder lifts a settler 2 pixels above the node's height (0x43f537: the height times 4, then 2 more).
    private const int SettlerLift = 2;

    // The end of the table of sequence offsets in the sequences' entry: a count word, then a dword for each sequence.
    private const int TableEnd = 4 + 4 * Sequences;

    /// <summary>The palette index of a player's colour.</summary>
    public static int ColourOf(int player) => ColourBase + ColourStep * player;

    // The settlers that are drawn walk the roads (state 2), wait out a step (5), walk along a road as a carrier does (3), hand
    // a good over (0xe), step through a door (4), walk free (0x10), cut (0x11), or search for a flag home (0x19). **Confirmed**
    // (draw run, settler-draws.txt): a carrier that waits next to a flag (state 0x42) is not drawn at all: the carrier's figure
    // handler (0x43f8ae) returns at once for it. **Confirmed** the same way: a carrier whose road was taken down or joined and
    // that waits for its turn to go home (states 0x44 and 0x45, docs/rules/removal.md) is not drawn either, nor is a settler
    // that claims its node (0x1c) on its way home, nor a settler at work (the woodcutter's 0x12, the state 0x1f of the settler
    // of the type 1 hut). A carrier hands the good it carries over to the castle in state 0xe (docs/rules/goods.md);
    // **Confirmed** (draw run): it is drawn as one on its road is, with the good in its hands. A woodcutter that steps through
    // its door, walks free along its vector, or cuts is drawn with the frame and the timer it holds as a walking settler is
    // (docs/rules/woodcutting.md). **Confirmed** (draw run): the cut's frames 0x74 to 0x78 are drawn through the same rule, the
    // figure bytes from 0x80 in the row of the felling and the other bytes in the row of the walk. A settler that searches for
    // the nearest flag on its way home is drawn with the frame 0x52 the claim sets, as a walking settler is. **Confirmed**
    // (draw run, the woodcutter and the dismissed carrier).
    private static bool IsDrawn(Activity activity) =>
        activity is Walks or WaitsOutStep or WalksRoad or HandsOver or Door or WalksFree or Cuts or SearchesHome;

    private readonly short[] _bases;
    private readonly short[] _offsets;
    private readonly byte[] _data;
    private readonly short[] _carried;
    private readonly FigureAdds _woodcutter;
    private readonly sbyte[] _frameRows;
    private readonly HutFigureAdds _typeOneHut;
    private readonly IdleCarrierTables? _idle;

    // A carrier that waits off its node (the activities of the states 0x42 to 0x45): it takes state 0x42 at the end of its road
    // (0x43bc52), state 0x43 when it has a good to fetch and its node is taken, and 0x44 or 0x45 when its road goes
    // (docs/rules/removal.md). From 0x42 on its node holds a mark (byte 1 of record B: bit 7 and the player) until it takes the
    // node again (0x42ff81, 0x43007b).

    // The node's lake mark picks the fixed figure word, and its road directions the offsets (0x44d77e).
    private const int FixedFigureWord = 0x203;

    // The woodcutter's handler (0x43fb6b) tests the figure byte for bit 7, and the settler's activity for the way back with the
    // log.
    private const int HighFigure = 0x80;

    /// <param name="bases">Two words for each high byte of the figure word: the figure base and the second figure base (negative for none).</param>
    /// <param name="offsets">Two words for each low byte of the figure word: what it adds to the figure and to the second figure.</param>
    /// <param name="sequences">The bytes of entry 2 of the graphics archive.</param>
    /// <param name="carried">For each good the word a carrier carrying it adds to its figure byte.</param>
    /// <param name="woodcutter">What the woodcutter's handler adds to the figure byte; all 0 draws him in the carrier's row.</param>
    /// <param name="frameRows">For each frame what the record builder adds to the row of the settler's record (<c>0x43f402</c>); none for no frame.</param>
    /// <param name="typeOneHut">What the handler of the settler of the type 1 hut adds to the figure byte (<c>0x43fed8</c>).</param>
    public SettlerFigures(
        short[] bases,
        short[] offsets,
        byte[] sequences,
        short[]? carried = null,
        FigureAdds woodcutter = default,
        sbyte[]? frameRows = null,
        HutFigureAdds typeOneHut = default,
        IdleCarrierTables? idle = null
    )
    {
        _idle = idle;
        _typeOneHut = typeOneHut;
        _frameRows = frameRows ?? [];
        _woodcutter = woodcutter;
        _carried = carried ?? [];
        _bases = bases;
        _offsets = offsets;
        _data = sequences;
    }

    /// <summary>
    /// The sprites of the settlers that walk, in the order of their rows, each settler's figure, overlay, and second figure in
    /// that order; the clock is the game's (16 bits). <paramref name="shifts"/> holds the header bytes 0 and 1 of a figure
    /// entry, the shift of its second figure; a figure it leaves out shifts nothing. The waiting carriers that the tile pass
    /// draws (<c>0x44d77e</c>) follow the settlers of their row.
    /// </summary>
    public List<SettlerDraw> Draws(
        Map map,
        IReadOnlyList<Settler> settlers,
        ushort clock,
        IReadOnlyDictionary<int, (int X, int Y)>? shifts = null
    )
    {
        var records = new List<(int Y, int X, List<SettlerDraw> Sprites)>();
        foreach (var settler in settlers)
        {
            if (
                !IsDrawn(settler.Activity)
                || (settler.Job != Job.Carrier && settler.Job != Job.Woodcutter && settler.Job != Job.TypeOneHut)
                || Triple(settler, clock) is not { } triple
            )
            {
                continue;
            }

            var (walkFigure, dx, dy) = triple;
            var word = FigureWord(settler, walkFigure);
            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            var height = map.At(settler.X, settler.Y).Height;
            x += dx;
            y += dy - height * TerrainTriangles.HeightStep - SettlerLift;
            // The record joins the list of its row, which the frame moves (0x43f537); a list holds its records in the order
            // the original scans the nodes, row by row and from the west, so the order of the records is the settlers'
            // row, then their node's row and column.
            var row = settler.Y + (settler.Frame < _frameRows.Length ? _frameRows[settler.Frame] : 0);
            var sprites = new List<SettlerDraw>();
            // The figure comes first; the overlay and the second figure follow it, and only the second figure is shifted.
            foreach (var (entry, colour, shiftedBy) in Entries(word, ColourOf(settler.Player)))
            {
                var shift = shiftedBy >= 0 && shifts is not null ? shifts.GetValueOrDefault(shiftedBy) : default;
                sprites.Add(new SettlerDraw(row, entry, colour, x + shift.X, y + shift.Y));
            }

            // A word outside the tables draws nothing, so the record has no row to order by.
            if (sprites.Count > 0)
            {
                records.Add((settler.Y, settler.X, sprites));
            }
        }

        // 0x44d3ae draws each row's settler records (0x44ffe7), then the records the tile pass made for that row's nodes
        // (0x44fff3 on the list at 0x6749a3), which are the waiting carriers' (0x44d77e).
        var idle = IdleRecords(map, settlers, clock, shifts);

        return
        [
            .. records
                .Select(record => (Order: 0, record.Y, record.X, record.Sprites))
                .Concat(idle.Select(record => (Order: 1, record.Y, record.X, record.Sprites)))
                .OrderBy(record => record.Sprites[0].Row)
                .ThenBy(record => record.Order)
                .ThenBy(record => record.Y)
                .ThenBy(record => record.X)
                .SelectMany(record => record.Sprites),
        ];
    }

    // The records the tile pass makes for the nodes marked by a waiting carrier (0x44d77e). The figure word is 0x203 where bit
    // 6 of the node's flag byte is set; else the word of the table at 0x44d89d at the phase of the node plus the clock, shifted
    // right 3. The position is the node lifted by 4 times its height (no lift of 2, unlike 0x43f537), plus, for the second
    // kind, the offsets of the node's road directions.
    private List<(int Y, int X, List<SettlerDraw> Sprites)> IdleRecords(
        Map map,
        IReadOnlyList<Settler> settlers,
        ushort clock,
        IReadOnlyDictionary<int, (int X, int Y)>? shifts
    )
    {
        var records = new List<(int Y, int X, List<SettlerDraw> Sprites)>();
        if (_idle is null)
        {
            return records;
        }

        foreach (var settler in settlers)
        {
            if (settler.Job != Job.Carrier || settler.Activity is not WaitsOffRoad)
            {
                continue;
            }

            var node = map.At(settler.X, settler.Y);
            var height = node.Height;
            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            y -= height * TerrainTriangles.HeightStep;
            int word;
            if (node.LakeMark)
            {
                word = FixedFigureWord;
            }
            else
            {
                // **Stand-in:** the original picks the phase by bits 2 to 5 of the address of the node's record, so by the
                // node's column only where the map buffer starts on a multiple of 64; the engine takes the column.
                var phase = _idle.Phases[settler.X & (_idle.Phases.Length - 1)];
                word = (ushort)_idle.Figures[((ushort)(phase + clock) >> 3) & (_idle.Figures.Length - 1)];
                // The offsets table has an entry for each set of road directions (its six bits as a number).
                var direction = node.Roads.Bits;
                x += _idle.Offsets[2 * direction];
                y += _idle.Offsets[2 * direction + 1];
            }

            // The record is drawn as a settler's is (0x44fff3): the figure, the overlay, and the shifted second figure.
            var sprites = new List<SettlerDraw>();
            foreach (var (entry, colour, shiftedBy) in Entries(word, ColourOf(settler.Player)))
            {
                var shift = shiftedBy >= 0 && shifts is not null ? shifts.GetValueOrDefault(shiftedBy) : default;
                sprites.Add(new SettlerDraw(settler.Y, entry, colour, x + shift.X, y + shift.Y));
            }

            if (sprites.Count > 0)
            {
                records.Add((settler.Y, settler.X, sprites));
            }
        }

        return records;
    }

    /// <summary>
    /// Every sprite the sequences of a job can draw: the entry and the colour of its fill (-1 where none). A sequence
    /// runs from its offset to the next sequence's, or to the end of the entry.
    /// </summary>
    public IEnumerable<(int Entry, int Colour)> Sprites(int job, int colour, int carriedGood = 0)
    {
        var add = job == Job.CarrierCode ? Carried(new Good(carriedGood)) : 0;
        var starts = new HashSet<int>();
        for (var frame = 0; frame < Sequences && _data.Length >= TableEnd; frame++)
        {
            var start = SequenceStart(frame);
            if (start > TableEnd - 4)
            {
                starts.Add(start);
            }
        }

        // A waiting carrier is drawn with the figure words of the tile pass (0x44d77e).
        if (job == Job.CarrierCode && _idle is not null)
        {
            foreach (var word in _idle.Figures.Select(word => (int)(ushort)word).Append(FixedFigureWord).Distinct())
            {
                foreach (var (entry, fill, _) in Entries(word, colour))
                {
                    yield return (entry, fill);
                }
            }
        }

        var list = starts.Order().ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var stop = i + 1 < list.Count ? list[i + 1] : _data.Length;
            for (var at = list[i]; at + 3 <= stop; at += 3)
            {
                foreach (var word in Words(job, _data[at], add))
                {
                    foreach (var (entry, fill, _) in Entries(word, colour))
                    {
                        yield return (entry, fill);
                    }
                }
            }
        }
    }

    // The figure words a job's handler can make of a figure byte of its sequences; a carrier carrying a good (add) makes
    // both the plain word and the carried one. A job other than the carrier and the woodcutter has none.
    private IEnumerable<int> Words(int job, int figure, int add)
    {
        switch (job)
        {
            case Job.CarrierCode:
                yield return figure;
                if (add != 0)
                {
                    yield return figure + add;
                }

                break;
            case Job.WoodcutterCode:
                yield return figure + WoodcutterAdd(figure, false);
                if (figure < HighFigure)
                {
                    yield return figure + WoodcutterAdd(figure, true);
                }

                break;
            case RuleTables.TypeOneHutJob when figure >= HighFigure:
                yield return figure + _typeOneHut.High;
                yield return figure + _typeOneHut.HighFacing;
                break;
            case RuleTables.TypeOneHutJob:
                yield return figure + _typeOneHut.Walk;
                yield return figure + _typeOneHut.Carrying;
                break;
        }
    }

    // The figure word of a settler's figure byte (0x43f8ae for the carrier, 0x43fb6b for the woodcutter): the carrier adds
    // the word of the good it carries; the woodcutter adds the constant of the handler, the one of the way back with the
    // log (byte 13 0x80, byte 14 1, state 0x10) for a figure byte below 0x80 on that way.
    private int FigureWord(Settler settler, int figure)
    {
        if (settler.Job == Job.Carrier)
        {
            return figure + CarriedFigure(settler);
        }

        if (settler.Job == Job.TypeOneHut)
        {
            // The handler of the type 1 hut's settler (0x43fed8) adds by the heading for a figure byte from 0x80 and by the
            // way back with a load, as the woodcutter's does, below it.
            if (figure >= HighFigure)
            {
                return figure + (FacesBack(settler) ? _typeOneHut.HighFacing : _typeOneHut.High);
            }

            return figure + (WayBackWithLoad(settler) ? _typeOneHut.Carrying : _typeOneHut.Walk);
        }

        return figure + WoodcutterAdd(figure, WayBackWithLoad(settler));
    }

    // What the woodcutter's handler (0x43fb6b) adds to a figure byte: the felling's constant from 0x80, below it the way
    // back's with a load or the walk's.
    private int WoodcutterAdd(int figure, bool carrying) =>
        figure >= HighFigure ? _woodcutter.Cutting
        : carrying ? _woodcutter.Carrying
        : _woodcutter.Walk;

    // The way back with a load: the free walk back to the hut's flag with the log (0x43fc0b, 0x43ff40).
    private static bool WayBackWithLoad(Settler settler) =>
        settler.Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: true } };

    // The settler's heading is 1 (byte 14, which the handler of the type 1 hut's settler reads at 0x43fed8): the way back with
    // a load, the first cut, or a step back from the node to the south-west.
    private static bool FacesBack(Settler settler) =>
        settler.Activity switch
        {
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } } => true,
            Cuts cut => cut.Count == 1,
            Walks walks => walks.Heading == new Heading(1),
            _ => false,
        };

    // The offset in the sequences' entry at which the sequence of a frame starts.
    private int SequenceStart(int frame)
    {
        var slot = 4 + 4 * frame;

        return 4 + ((_data[slot] << 24) | (_data[slot + 1] << 16) | (_data[slot + 2] << 8) | _data[slot + 3]);
    }

    // The sprites of a figure word: the figure, its colour overlay filled with the colour, and the second figure where
    // the base has one (-1 where a sprite has no fill), each with the figure entry whose header shifts it (-1 for none:
    // only the second figure is shifted). The high byte of the word picks the pair of bases, the low byte the pair of
    // offsets (0x44fff3). None where either lies outside the tables.
    private IEnumerable<(int Entry, int Colour, int ShiftedBy)> Entries(int word, int colour)
    {
        var row = (word >> 8) & 0xff;
        var figure = word & 0xff;
        if (2 * row + 1 >= _bases.Length || 2 * figure + 1 >= _offsets.Length)
        {
            yield break;
        }

        var first = _bases[2 * row] + _offsets[2 * figure];
        yield return (FigureBase + first, -1, -1);
        yield return (OverlayBase + first, colour, -1);
        if (_bases[2 * row + 1] >= 0)
        {
            yield return (SecondBase + _bases[2 * row + 1] + _offsets[2 * figure + 1], -1, FigureBase + first);
        }
    }

    // The word a carrier on its road or handing a good over adds to its figure byte for the good it carries (byte 11, at
    // 0x43f8ae, for the states 3 and 0xe). A carrier in state 0x42 has no record (byte 11 is the road direction there, no
    // good). Sprites applies the same rule through Carried.
    private int CarriedFigure(Settler settler) =>
        settler.Job == Job.Carrier && settler.Activity is WalksRoad or HandsOver
            ? Carried(
                settler.Activity switch
                {
                    WalksRoad road => road.Load?.Good ?? Good.None,
                    HandsOver hands => hands.Load?.Good ?? Good.None,
                    _ => Good.None,
                }
            )
            : 0;

    // The word of the table at 0x43f4b7 for a good (2 * good bytes on): the row of the figure bases in its high byte. A
    // carrier with no good (0) adds nothing; so does a good the table does not cover.
    private int Carried(Good good) => good.Code > 0 && good.Code < _carried.Length ? _carried[good.Code] : 0;

    // The triple of the settler's sequence for the time it has left (the timer less the clock units since the stamp, 0x43f537
    // shifts the timer the view pass leaves right 3), or null where there is none.
    private (int Figure, int Dx, int Dy)? Triple(Settler settler, ushort clock)
    {
        if (settler.Frame is < 0 or >= Sequences || _data.Length < TableEnd)
        {
            return null;
        }

        var start = SequenceStart(settler.Frame);
        var elapsed = (ushort)(clock - settler.Stamp);
        var left = settler.Timer >= elapsed ? settler.Timer - elapsed : 0;
        var at = start + 3 * (left >> 3);
        if (start <= TableEnd - 4 || at + 3 > _data.Length)
        {
            return null;
        }

        return (_data[at], (sbyte)_data[at + 1], (sbyte)_data[at + 2]);
    }
}
