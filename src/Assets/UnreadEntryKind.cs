using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of an entry no routine reads: where it lies. It has no byte preview.</summary>
public sealed record UnreadEntryPreview : Preview
{
    /// <summary>The entry.</summary>
    public required ArchiveEntry Entry { get; init; }
}

/// <summary>
/// The entries that no routine of the program reaches: every routine that finds an entry in the archive is a
/// drawing routine, the palette loader, or the entry finder <c>0x4073d0</c>, and none is handed these numbers
/// (<c>docs/rules/graphics-archives.md</c>, "Entries no routine reads"). The status is inferred: the search found no
/// reader, and a table the search did not follow could still hold one.
/// </summary>
public sealed class UnreadEntryKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "unread";

    // The numbers the search covers; an absent slot is listed by no kind.
    static readonly (int First, int Last)[] Ranges =
    [
        (448, 521), // sprite-shaped, including 490, which has the shape of a raw image
        (3900, 3994), // the entries from 3900; 3990 to 3994 hold XMIDI music containers
        (3999, 3999), // decodes as a 16 x 16 sprite; the program only clamps a value to 3999 (0x47664e, 0x478294)
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => Array.Exists(Ranges, r => entry >= r.First && entry <= r.Last);

    /// <inheritdoc/>
    protected override string Label(string file, ArchiveEntry entry) =>
        $"{base.Label(file, entry)}: offset {entry.Offset}, size {entry.Size}, not read by the program";

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        new UnreadEntryPreview
        {
            Entry = entry,
            Summary = $"Entry {entry.Number}: offset {entry.Offset}, size {entry.Size}; not read by the program.",
        };
}
