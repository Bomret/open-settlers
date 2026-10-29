using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The entries that no routine of the program is shown to reach: every routine that finds an entry in the archive
/// is a drawing routine, the palette loader, or the entry finder <c>0x4073d0</c>, and none followed is handed
/// these numbers (<c>docs/rules/graphics-archives.md</c>, "Entries no routine reads"). The status is inferred: the
/// search found no reader, and a list the search did not follow could still hold one.
/// </summary>
public sealed class UnreadEntryKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "unread";

    // The numbers the search covers; an absent slot is listed by no kind.
    static readonly (int First, int Last)[] Ranges =
    [
        (494, 494), // sprite-shaped; the one caller that adds 174 plus 320 draws only an object byte of 1 or more (0x44e653)
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
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        new LocatedEntryPreview
        {
            Entry = entry,
            Summary = $"Entry {entry.Number}: offset {entry.Offset}, size {entry.Size}; not read by the program.",
        };
}
