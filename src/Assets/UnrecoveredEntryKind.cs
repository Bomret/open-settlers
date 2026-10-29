using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of an entry no kind has recovered yet: where it lies and how large it is.</summary>
public sealed record UnrecoveredPreview : Preview
{
    /// <summary>The entry.</summary>
    public required ArchiveEntry Entry { get; init; }
}

/// <summary>
/// The graphics-archive entries that no other <see cref="ArchiveEntryKind"/> claims yet, listed with their
/// location and size. It also reports a missing or corrupt archive once, so it stands for the archive file when no
/// entry can be listed.
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

    internal override bool ReportsFileFailures => true;

    /// <inheritdoc/>
    protected override string Label(string file, ArchiveEntry entry) =>
        $"{base.Label(file, entry)}: offset {entry.Offset}, size {entry.Size}";

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        new UnrecoveredPreview
        {
            Entry = entry,
            Summary =
                $"Entry {entry.Number}: offset {entry.Offset}, size {entry.Size}. No kind has recovered this entry yet.",
        };
}
