using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>How far a walking settler is moved from its node, in map pixels, at one step of its walk.</summary>
public readonly record struct StepOffset(int Dx, int Dy);

/// <summary>
/// The dx and dy of every step of every walk frame, from entry 2 of the graphics archive: the table the placement of a
/// walking settler reads under every graphics source (<c>docs/rules/settlers.md</c>, "Drawing the walk"). The entry holds a
/// dword, then 200 big-endian offsets, one for each frame, from the first byte after that dword; each offset names a
/// sequence of (figure byte, dx, dy) triples. The settler's frame picks the sequence and the time it has left, in ticks (8
/// clock units), picks the triple. The figure byte belongs to the History Edition source, which picks the figure by it.
/// </summary>
public sealed class StepOffsets
{
    /// <summary>The number of walk frames the entry holds a sequence offset for.</summary>
    public const int Frames = 200;

    // The end of the table of sequence offsets in the entry: a count word, then a dword for each frame.
    internal const int TableEnd = 4 + 4 * Frames;

    // The entry of the graphics archive that holds the sequences.
    const int Entry = 2;

    readonly byte[] _data;

    /// <summary>The table of the bytes of entry 2.</summary>
    public StepOffsets(byte[] entry)
    {
        _data = entry;
    }

    /// <summary>Reads the table from the install. Throws <see cref="System.IO.InvalidDataException"/> where the entry is absent.</summary>
    public static StepOffsets Load(Install install) =>
        new(
            ObjectGraphics.Read(
                ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile)),
                Entry,
                "the walk sequences",
                data => data.ToArray()
            )
        );

    /// <summary>
    /// The step of <paramref name="frame"/> with <paramref name="timeLeft"/> clock units left of it, or null where the frame
    /// has no sequence or the time reaches past the end of the entry.
    /// </summary>
    public StepOffset? At(int frame, int timeLeft) =>
        TripleAt(_data, frame, timeLeft) is { } at ? new StepOffset((sbyte)_data[at + 1], (sbyte)_data[at + 2]) : null;

    /// <summary>The offset in <paramref name="data"/> at which the sequence of a frame starts.</summary>
    internal static int SequenceStart(byte[] data, int frame)
    {
        var slot = 4 + 4 * frame;

        return 4 + ((data[slot] << 24) | (data[slot + 1] << 16) | (data[slot + 2] << 8) | data[slot + 3]);
    }

    /// <summary>
    /// The offset in <paramref name="data"/> of the triple a frame holds for the time left (the time shifted right 3), or
    /// null where there is none.
    /// </summary>
    internal static int? TripleAt(byte[] data, int frame, int timeLeft)
    {
        if (frame is < 0 or >= Frames || data.Length < TableEnd)
        {
            return null;
        }

        var start = SequenceStart(data, frame);
        var at = start + 3 * (timeLeft >> 3);
        if (start <= TableEnd - 4 || at + 3 > data.Length)
        {
            return null;
        }

        return at;
    }
}
