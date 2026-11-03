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
    private const int StatesWalking = 2;
    private const int StatesWaiting = 5;

    private readonly short[] _bases;
    private readonly short[] _offsets;
    private readonly byte[] _data;

    /// <param name="bases">Eight words for each job: the figure base, the second figure base (negative for none), and more.</param>
    /// <param name="offsets">Two words for each figure byte: what it adds to the figure and to the second figure.</param>
    /// <param name="sequences">The bytes of entry 2 of the graphics archive.</param>
    public SettlerFigures(short[] bases, short[] offsets, byte[] sequences)
    {
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
            if (settler.State is not (StatesWalking or StatesWaiting) || Triple(settler, clock) is not { } triple)
            {
                continue;
            }

            var (figure, dx, dy) = triple;

            var (x, y) = Camera.NodePosition(settler.X, settler.Y);
            var height = map.Layer(MapLayer.Height)[map.Index(settler.X, settler.Y)] & Map.HeightMask;
            x += ObjectDraws.OffsetX + dx;
            y += ObjectDraws.OffsetY - height * TerrainTriangles.HeightStep + dy;
            var baseIndex = settler.Job * 8;
            var first = _bases[baseIndex] + _offsets[2 * figure];
            draws.Add(new SettlerDraw(settler.Y, FigureBase + first, -1, x, y));
            draws.Add(
                new SettlerDraw(
                    settler.Y,
                    FigureBase + first + OverlayDistance,
                    ColourBase + ColourStep * settler.Player,
                    x,
                    y
                )
            );
            if (_bases[baseIndex + 1] >= 0)
            {
                draws.Add(
                    new SettlerDraw(settler.Y, SecondBase + _bases[baseIndex + 1] + _offsets[2 * figure + 1], -1, x, y)
                );
            }
        }

        draws.Sort((a, b) => a.Row.CompareTo(b.Row));

        return draws;
    }

    /// <summary>
    /// Every sprite the sequences of a job can draw: the entry and the colour of its fill (-1 where none). A sequence
    /// runs from its offset to the next sequence's, or to the end of the entry.
    /// </summary>
    public IEnumerable<(int Entry, int Colour)> Sprites(int job, int colour)
    {
        var end = 4 + 4 * Sequences;
        var starts = new SortedSet<int>();
        for (var frame = 0; frame < Sequences && _data.Length >= end; frame++)
        {
            var slot = 4 + 4 * frame;
            var start = 4 + ((_data[slot] << 24) | (_data[slot + 1] << 16) | (_data[slot + 2] << 8) | _data[slot + 3]);
            if (start > end - 4)
            {
                starts.Add(start);
            }
        }

        var list = starts.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var stop = i + 1 < list.Count ? list[i + 1] : _data.Length;
            for (var at = list[i]; at + 3 <= stop; at += 3)
            {
                var first = _bases[job * 8] + _offsets[2 * _data[at]];
                yield return (FigureBase + first, -1);
                yield return (FigureBase + first + OverlayDistance, colour);
                if (_bases[job * 8 + 1] >= 0)
                {
                    yield return (SecondBase + _bases[job * 8 + 1] + _offsets[2 * _data[at] + 1], -1);
                }
            }
        }
    }

    // The triple of the settler's sequence for the time it has left, or null where there is none.
    private (int Figure, int Dx, int Dy)? Triple(Settler settler, ushort clock)
    {
        if (settler.Frame is < 0 or >= Sequences || _data.Length < 4 + 4 * Sequences)
        {
            return null;
        }

        var slot = 4 + 4 * settler.Frame;
        var start = 4 + ((_data[slot] << 24) | (_data[slot + 1] << 16) | (_data[slot + 2] << 8) | _data[slot + 3]);
        var elapsed = (ushort)(clock - settler.Stamp);
        var left = settler.Timer >= elapsed ? settler.Timer - elapsed : 0;
        var at = start + 3 * (left >> 3);
        if (start <= 4 + 4 * Sequences - 4 || at + 3 > _data.Length)
        {
            return null;
        }

        return (_data[at], (sbyte)_data[at + 1], (sbyte)_data[at + 2]);
    }
}
