using System;

namespace OpenSettlers.Testing;

/// <summary>The bytes of the entries of the graphics archives the tests build in code: no file, no library type.</summary>
public static class GraphicEntries
{
    /// <summary>A graphic entry: the 10-byte header, then the body.</summary>
    public static byte[] Entry(sbyte first, sbyte second, ushort width, ushort height, short x, short y, byte[] body)
    {
        var bytes = new byte[10 + body.Length];
        bytes[0] = (byte)first;
        bytes[1] = (byte)second;
        BitConverter.TryWriteBytes(bytes.AsSpan(2), width);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), height);
        BitConverter.TryWriteBytes(bytes.AsSpan(6), x);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), y);
        body.CopyTo(bytes, 10);

        return bytes;
    }

    /// <summary>A mask entry: the header, then each row as (skip, cover) pairs and its end pair (any skip, cover 0).</summary>
    public static byte[] MaskEntry(ushort width, ushort height, params byte[] body) =>
        Entry(2, 0, width, height, -1, -2, body);

    /// <summary>A 3 x 1 sprite: pixel 0 is skipped, pixel 1 is drawn with palette index 0, pixel 2 with index 7.</summary>
    public static byte[] Sprite(short offsetX = -1, short offsetY = -2) =>
        Entry(1, 0, 3, 1, offsetX, offsetY, [1, 2, 0, 7, 0, 0]);

    /// <summary>A 2 x 2 sprite with the given offsets.</summary>
    public static byte[] Square(short offsetX, short offsetY) =>
        Entry(1, 0, 2, 2, offsetX, offsetY, [0, 2, 3, 3, 0, 0, 0, 1, 4, 0, 0]);

    /// <summary>The red components of index 7 and the shadow color 0x80 are 28 and 36. Index 0 reads as black.</summary>
    public static byte[] Palette()
    {
        var palette = new byte[768];
        palette[3 * 7] = 28;
        palette[3 * 0x80] = 36;

        return palette;
    }

    /// <summary>A 2 x 1 shadow mask.</summary>
    public static byte[] ShadowMask() => MaskEntry(2, 1, 1, 1, 0, 0);

    /// <summary>
    /// Entry 2: a dword, then 200 big-endian offsets from the first byte after that dword. The sequence of frame 7 holds
    /// three triples (figure byte, dx, dy): (3, 1, 2), (4, -1, 0), (5, 0, 0); the sequence of frame 8 holds (0x81, 0, 0), a
    /// figure byte from 0x80 as the cut's frames hold.
    /// </summary>
    public static byte[] Sequences()
    {
        var data = new byte[4 + 200 * 4 + 12];
        var at = 4 + 200 * 4;
        for (var i = 0; i < 12; i++)
        {
            data[at + i] = new byte[] { 3, 1, 2, 4, 0xff, 0, 5, 0, 0, 0x81, 0, 0 }[i];
        }

        var offset = at - 4;
        data[4 + 7 * 4 + 2] = (byte)(offset >> 8);
        data[4 + 7 * 4 + 3] = (byte)offset;
        data[4 + 8 * 4 + 2] = (byte)((offset + 9) >> 8);
        data[4 + 8 * 4 + 3] = (byte)(offset + 9);

        return data;
    }
}
