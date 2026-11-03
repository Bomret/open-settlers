using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// One sprite of a settler: the sprite entry, the palette index a colour overlay is filled with (-1 for a sprite drawn
/// as it is), and the position in map pixels.
/// </summary>
public readonly record struct SettlerDraw(int Row, int Entry, int Colour, int X, int Y);

/// <summary>
/// Which sprites a walking settler is drawn with (<c>docs/rules/settlers.md</c>, "Drawing the walk"). The settler's frame
/// (the shape of its step, which holds the direction) picks a sequence of (figure byte, dx, dy) triples from entry 2 of the
/// graphics archive; the time left of the step, in ticks (8 clock units), picks the triple. The tables of the program turn
/// the job and the figure byte into the entries: the figure, its overlay in the player's colour, and a second figure.
/// <b>Inferred:</b> that the sequence offsets count from the byte after the first dword of the entry (as
/// <c>0x41e8e0</c> keeps its table), that the triple index is the time left shifted right 3, that the time left falls
/// with the clock between the settler's turns (the original draws a settler in view as it acts every pass), the draw
/// position (the node plus 16 and 8, lifted by its height, as an object), and that the second figure takes no offset.
/// </summary>
public sealed class SettlerFigures
{
    // The sprite entry of the figures, of the second figures, and the distance of a colour overlay (0x406610).
    private const int FigureBase = 1850;
    private const int SecondBase = 3150;
    private const int OverlayDistance = 2500;

    // The palette index of the first player's colour, and the step between players (0x406610).
    private const int ColourBase = 0x40;
    private const int ColourStep = 4;

    private const int Sequences = 200;

    // The end of the table of sequence offsets in the sequences' entry: a count word, then a dword for each sequence.
    private const int TableEnd = 4 + 4 * Sequences;

    /// <summary>The palette index of a player's colour.</summary>
    public static int ColourOf(int player) => ColourBase + ColourStep * player;
    private const int StatesWalking = 2;
    private const int StatesWaiting = 5;

    // A carrier on its road (state 3) walks as a settler does. A carrier that waits next to a flag (state 0x42) is drawn
    // with the timer it holds: the original does not run it down. **Inferred:** the idle frames 0x6e to 0x73 name the
    // sequences of the waiting carrier, which no draw run confirms.
    private const int StatesOnRoad = 3;
    private const int StatesIdle = 0x42;

    // A carrier whose road was taken down or joined (states 0x44 and 0x45, docs/rules/removal.md) still stands next to the flag
    // and is drawn as the carrier that waits. **Inferred:** no draw run confirms it.
    private const int StatesDismissed = 0x44;
    private const int StatesRejoined = 0x45;

    // A woodcutter that steps through its door (state 4), walks free along its vector (0x10), or cuts (0x11) is drawn with
    // the frame and the timer it holds as a walking settler is (docs/rules/woodcutting.md). **Inferred:** the cut's
    // frames 0x74 to 0x78 name the cutting sequences, which no draw run confirms.
    private const int StatesDoor = 4;
    private const int StatesFreeWalk = 0x10;
    private const int StatesCutting = 0x11;

    // A settler that claims its node (0x1c) or searches for the nearest flag (0x19) on its way home stands with the frame
    // 0x52 the claim sets. **Inferred:** it is drawn as a waiting settler is; no draw run confirms it.
    private const int StatesClaiming = 0x1c;
    private const int StatesSearching = 0x19;

    private readonly short[] _bases;
    private readonly short[] _offsets;
    private readonly byte[] _data;
    private readonly short[] _carried;

    /// <param name="bases">Eight words for each job: the figure base, the second figure base (negative for none), and more.</param>
    /// <param name="offsets">Two words for each figure byte: what it adds to the figure and to the second figure.</param>
    /// <param name="sequences">The bytes of entry 2 of the graphics archive.</param>
    /// <param name="carried">For each good the word a carrier carrying it adds to its figure byte.</param>
    public SettlerFigures(short[] bases, short[] offsets, byte[] sequences, short[]? carried = null)
    {
        _carried = carried ?? [];
        _bases = bases;
        _offsets = offsets;
        _data = sequences;
    }

    /// <summary>The sprites of the settlers that walk, in the order of their rows; the clock is the game's (16 bits).</summary>
    public List<SettlerDraw> Draws(Map map, IReadOnlyList<Settler> settlers, ushort clock)
    {
        var draws = new List<SettlerDraw>();
        foreach (var settler in settlers)
        {
            if (
                settler.State
                    is not (
                        StatesWalking
                        or StatesWaiting
                        or StatesOnRoad
                        or StatesIdle
                        or StatesDismissed
                        or StatesRejoined
                        or StatesDoor
                        or StatesFreeWalk
                        or StatesCutting
                        or StatesClaiming
                        or StatesSearching
                    )
                || Triple(settler, clock) is not { } triple
            )
            {
                continue;
            }

            var (walkFigure, dx, dy) = triple;
            var figure = walkFigure + CarriedFigure(settler);
            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            var height = map.Layer(MapLayer.Height)[map.Index(settler.X, settler.Y)] & Map.HeightMask;
            x += ObjectDraws.OffsetX + dx;
            y += ObjectDraws.OffsetY - height * TerrainTriangles.HeightStep + dy;
            foreach (var (entry, colour) in Entries(settler.Job, figure, ColourOf(settler.Player)))
            {
                draws.Add(new SettlerDraw(settler.Y, entry, colour, x, y));
            }
        }

        draws.Sort((a, b) => a.Row.CompareTo(b.Row));

        return draws;
    }

    /// <summary>
    /// Every sprite the sequences of a job can draw: the entry and the colour of its fill (-1 where none). A sequence
    /// runs from its offset to the next sequence's, or to the end of the entry.
    /// </summary>
    public IEnumerable<(int Entry, int Colour)> Sprites(int job, int colour, int carriedGood = 0)
    {
        var add = job == Settler.CarrierJob && carriedGood > 0 && carriedGood < _carried.Length ? _carried[carriedGood] : 0;
        var starts = new HashSet<int>();
        for (var frame = 0; frame < Sequences && _data.Length >= TableEnd; frame++)
        {
            var start = SequenceStart(frame);
            if (start > TableEnd - 4)
            {
                starts.Add(start);
            }
        }

        var list = starts.Order().ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var stop = i + 1 < list.Count ? list[i + 1] : _data.Length;
            for (var at = list[i]; at + 3 <= stop; at += 3)
            {
                foreach (var figure in add == 0 ? new int[] { _data[at] } : new int[] { _data[at], _data[at] + add })
                {
                    foreach (var sprite in Entries(job, figure, colour))
                    {
                        yield return sprite;
                    }
                }
            }
        }
    }

    // The offset in the sequences' entry at which the sequence of a frame starts.
    private int SequenceStart(int frame)
    {
        var slot = 4 + 4 * frame;

        return 4 + ((_data[slot] << 24) | (_data[slot + 1] << 16) | (_data[slot + 2] << 8) | _data[slot + 3]);
    }

    // The sprites of a figure byte of a job: the figure, its colour overlay filled with the colour, and the second figure
    // where the job has one (-1 where a sprite has no fill). None where the figure byte lies outside the offsets table.
    private IEnumerable<(int Entry, int Colour)> Entries(int job, int figure, int colour)
    {
        if (figure >= _offsets.Length / 2)
        {
            yield break;
        }

        var first = _bases[job * 8] + _offsets[2 * figure];
        yield return (FigureBase + first, -1);
        yield return (FigureBase + first + OverlayDistance, colour);
        if (_bases[job * 8 + 1] >= 0)
        {
            yield return (SecondBase + _bases[job * 8 + 1] + _offsets[2 * figure + 1], -1);
        }
    }

    // The word a carrier on its road adds to its figure byte for the good it carries (byte 11, at 0x43f8ae; the same routine
    // serves state 0xe, which is not drawn). **Inferred:** see ProgramTables.CarriedFigureAddress. In state 0x42 byte 11 is
    // the road direction, no good.
    private int CarriedFigure(Settler settler) =>
        settler is { Job: Settler.CarrierJob, State: StatesOnRoad, Mode: > 0 } && settler.Mode < _carried.Length
            ? _carried[settler.Mode]
            : 0;

    // The triple of the settler's sequence for the time it has left, or null where there is none.
    private (int Figure, int Dx, int Dy)? Triple(Settler settler, ushort clock)
    {
        if (settler.Frame is < 0 or >= Sequences || _data.Length < TableEnd)
        {
            return null;
        }

        var start = SequenceStart(settler.Frame);
        var elapsed = settler.State == StatesIdle ? (ushort)0 : (ushort)(clock - settler.Stamp);
        var left = settler.Timer >= elapsed ? settler.Timer - elapsed : 0;
        var at = start + 3 * (left >> 3);
        if (start <= TableEnd - 4 || at + 3 > _data.Length)
        {
            return null;
        }

        return (_data[at], (sbyte)_data[at + 1], (sbyte)_data[at + 2]);
    }
}
