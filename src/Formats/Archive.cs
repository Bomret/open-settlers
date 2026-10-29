using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>How the program sees one archive slot (<c>docs/rules/graphics-archives.md</c>).</summary>
public enum EntryState
{
    /// <summary>The slot holds no entry and the program leaves it empty.</summary>
    Absent,

    /// <summary>The file's table gives the slot an entry of its own.</summary>
    Present,

    /// <summary>The file leaves the slot empty and the program fills it with a copy of another entry's location.</summary>
    Alias,
}

/// <summary>
/// One slot of an archive, numbered as the program numbers it: the slot is the table row, and row 0 is the header,
/// so entries start at 1. <see cref="Offset"/> and <see cref="Size"/> are the entry's location in the file; for an
/// <see cref="EntryState.Alias"/> they are those of <see cref="AliasOf"/>; for an absent slot both are 0.
/// </summary>
public sealed record ArchiveEntry
{
    /// <summary>The slot number, as the program numbers it.</summary>
    public required int Number { get; init; }

    /// <summary>Whether the slot is present, an alias, or absent.</summary>
    public required EntryState State { get; init; }

    /// <summary>The entry's offset from the start of the file.</summary>
    public required uint Offset { get; init; }

    /// <summary>The entry's size in bytes.</summary>
    public required uint Size { get; init; }

    /// <summary>The number of the entry an alias repeats; null for a present or absent slot.</summary>
    public int? AliasOf { get; init; }
}

/// <summary>A graphics archive: its entries and the bytes they point into.</summary>
public sealed class Archive
{
    readonly byte[] bytes;
    readonly ArchiveEntry[] rows;

    internal Archive(byte[] bytes, ArchiveEntry[] rows)
    {
        this.bytes = bytes;
        this.rows = rows;
    }

    /// <summary>The number of table rows the header declares, the header's own row included.</summary>
    public int RowCount => rows.Length;

    /// <summary>Every slot from entry 1 to the last, in number order, absent slots included.</summary>
    public IReadOnlyList<ArchiveEntry> Entries =>
        new ArraySegment<ArchiveEntry>(rows, Math.Min(1, rows.Length), Math.Max(rows.Length - 1, 0));

    /// <summary>The slot with the given program number (1 to <see cref="RowCount"/> - 1).</summary>
    public ArchiveEntry this[int number] => rows[number];

    /// <summary>
    /// The bytes of a present or aliased entry. Throws <see cref="InvalidDataException"/> naming the entry, its
    /// offset, and its size when the entry runs past the end of the file, and
    /// <see cref="InvalidOperationException"/> for an absent slot.
    /// </summary>
    public ReadOnlyMemory<byte> Data(ArchiveEntry entry)
    {
        if (entry.State == EntryState.Absent)
        {
            throw new InvalidOperationException($"entry {entry.Number} is absent and has no data");
        }

        if ((long)entry.Offset + entry.Size > bytes.Length)
        {
            throw new InvalidDataException(
                $"entry {entry.Number} runs past the end of the file: offset {entry.Offset}, size {entry.Size}, file length {bytes.Length}"
            );
        }

        return new ReadOnlyMemory<byte>(bytes, (int)entry.Offset, (int)entry.Size);
    }
}

/// <summary>
/// The graphics archive reader (<c>docs/rules/graphics-archives.md</c>). An archive is a header (file size, row
/// count, flag) followed by 8-byte table rows (size, offset); the header takes the place of row 0.
/// </summary>
public static class ArchiveFile
{
    const int RowSize = 8;

    // The slots the program fills with a copy of another slot after loading (0x407290): the source and the
    // first and last slot filled.
    static readonly (int Source, int First, int Last)[] Copies = BuildCopies();

    /// <summary>
    /// Reads the header and the table. Throws <see cref="InvalidDataException"/>, naming what is wrong and the
    /// offset, for a file shorter than its header, a file shorter than its table, and a header size that differs
    /// from the file length. An entry that runs past the end of the file is not found here: it fails on its own
    /// at <see cref="Archive.Data"/>.
    /// </summary>
    public static Archive Read(byte[] bytes)
    {
        if (bytes.Length < RowSize)
        {
            throw new InvalidDataException(
                $"the archive is shorter than its header: the header ends at offset {RowSize}, the file at offset {bytes.Length}"
            );
        }

        var size = BitConverter.ToUInt32(bytes, 0);
        var count = BitConverter.ToUInt16(bytes, 4);
        if (bytes.Length < count * RowSize)
        {
            throw new InvalidDataException(
                $"the archive is shorter than its table: {count} rows end at offset {count * RowSize}, the file at offset {bytes.Length}"
            );
        }

        if (size != bytes.Length)
        {
            throw new InvalidDataException(
                $"the header gives the file size {size}, the file is {bytes.Length} bytes long"
            );
        }

        var rows = new ArchiveEntry[count];
        for (var number = 0; number < count; number++)
        {
            rows[number] = Row(bytes, number);
        }

        foreach (var (source, first, last) in Copies)
        {
            for (var number = first; number <= last && number < count && source < count; number++)
            {
                var original = rows[source];
                rows[number] =
                    original.State == EntryState.Absent
                        ? original with
                        {
                            Number = number,
                        }
                        : original with
                        {
                            Number = number,
                            State = EntryState.Alias,
                            AliasOf = original.Number,
                        };
            }
        }

        return new Archive(bytes, rows);
    }

    static ArchiveEntry Row(byte[] bytes, int number)
    {
        var size = BitConverter.ToUInt32(bytes, number * RowSize);
        var offset = BitConverter.ToUInt32(bytes, number * RowSize + 4);

        // The program tests the offset alone for presence.
        return offset == 0
            ? new ArchiveEntry
            {
                Number = number,
                State = EntryState.Absent,
                Offset = 0,
                Size = 0,
            }
            : new ArchiveEntry
            {
                Number = number,
                State = EntryState.Present,
                Offset = offset,
                Size = size,
            };
    }

    static (int, int, int)[] BuildCopies()
    {
        var copies = new List<(int, int, int)>
        {
            (1352, 1363, 1368),
            (1602, 1613, 1618),
            (3762, 3765, 3765),
            (3763, 3766, 3766),
            (3764, 3767, 3767),
        };
        for (var group = 0; group < 48; group++)
        {
            var source = 3450 + 6 * group;
            copies.Add((source, source + 1, source + 5));
        }

        return [.. copies];
    }
}
