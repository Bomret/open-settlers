using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The graphics-archive entries that no other <see cref="ArchiveEntryKind"/> claims: each is a gap in the
/// entry-kind map, so its asset fails with the entry and the reason, and the check fails.
/// </summary>
public sealed class UnrecoveredEntryKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "unrecovered";

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => false;

    internal override bool Takes(int entry, IReadOnlyList<ArchiveEntryKind> all) =>
        !all.Any(k => !ReferenceEquals(k, this) && k.Claims(entry));

    /// <inheritdoc/>
    protected override string Label(string file, ArchiveEntry entry) =>
        $"{base.Label(file, entry)}: offset {entry.Offset}, size {entry.Size}";

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        throw new InvalidDataException(
            $"no kind claims entry {entry.Number} (offset {entry.Offset}, size {entry.Size}): the entry-kind map has a gap"
        );
}
