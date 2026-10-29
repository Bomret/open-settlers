using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// A kind of graphics-archive entry: the base of every kind that reads entries of <c>loca/SPAD.PA</c>,
/// <c>loca/SPAE.PA</c>, and <c>loca/SPAF.PA</c>. This is the entry-kind map. A slice that recovers a kind adds one
/// file with one subclass that declares which entries it claims (<see cref="Claims"/>) and how it reads one
/// (<see cref="Read"/>); <see cref="AssetKind.Discover"/> finds it, so no shared table is edited. The base class
/// lists the archives, the entries, the aliases, and the failures.
/// </summary>
/// <remarks>
/// Entries are claimed by their program number, the same in all three archives (<c>docs/rules/graphics-archives.md</c>).
/// An alias is claimed by whichever kind claims the entry it repeats, so a kind never names an alias: a kind that
/// claims an alias's number makes that alias fail with a reason naming both entries. An entry
/// claimed by no kind fails, listed by <see cref="UnrecoveredEntryKind"/>. An entry claimed by two kinds fails in both,
/// with a reason that names both; the test over the discovered kinds fails too.
/// </remarks>
public abstract class ArchiveEntryKind : AssetKind
{
    /// <summary>The graphics archives of the install, in the order the browser lists them.</summary>
    public static readonly IReadOnlyList<string> Files = ["loca/SPAD.PA", "loca/SPAE.PA", "loca/SPAF.PA"];

    static readonly Lazy<IReadOnlyList<ArchiveEntryKind>> Discovered = new(() =>
        Discover().OfType<ArchiveEntryKind>().ToList()
    );

    /// <summary>
    /// Whether this kind claims the entry with this program number (1 and up). Pure and independent of the file.
    /// Claim present entries only: an alias follows its source, and a claim on an alias's own number fails it.
    /// </summary>
    public abstract bool Claims(int entry);

    /// <summary>
    /// Reads one claimed entry. <c>data</c> holds its bytes; <c>entry</c> is the slot being read, an alias
    /// included, whose <see cref="ArchiveEntry.Offset"/> and <see cref="ArchiveEntry.Size"/> are the data's
    /// location. <c>archive</c> is the archive it lies in, for a kind that needs another entry of the same archive,
    /// such as the palette (<see cref="PaletteOf"/>); a kind that needs none ignores it. Throws
    /// <see cref="InvalidDataException"/> for corrupt data; the message is the reason the check reports.
    /// </summary>
    protected abstract Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data);

    /// <summary>
    /// A palette of an archive, by its entry number (<c>docs/rules/graphics-images.md</c>).
    /// </summary>
    /// <exception cref="InvalidDataException">The archive has no such entry or the entry is not a palette.</exception>
    protected static Palette PaletteOf(Archive archive, int number)
    {
        try
        {
            var entry = archive[number];
            if (entry.State == EntryState.Absent)
            {
                throw new InvalidDataException($"the archive has no entry {number}");
            }

            return PaletteFile.Read(archive.Data(entry).Span);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"the palette (entry {number}) is unreadable: {e.Message}", e);
        }
    }

    /// <summary>The label of an entry in the browser's list.</summary>
    protected virtual string Label(string file, ArchiveEntry entry) =>
        entry.AliasOf is { } source
            ? $"{Path.GetFileName(file)} entry {entry.Number} (alias of {source})"
            : $"{Path.GetFileName(file)} entry {entry.Number}";

    /// <summary>Whether this kind lists the entry among the claimants <c>all</c>; <see cref="Claims"/> unless overridden.</summary>
    internal virtual bool Takes(int entry, IReadOnlyList<ArchiveEntryKind> all) => Claims(entry);

    /// <summary>
    /// Whether this kind reports a missing or corrupt archive. One kind does, so that a failing file is reported
    /// once and not once per kind.
    /// </summary>
    internal virtual bool ReportsFileFailures => false;

    /// <summary>The names of the kinds of <c>all</c> that list the entry.</summary>
    public static IReadOnlyList<string> ClaimedBy(int entry, IReadOnlyList<ArchiveEntryKind> all) =>
        all.Where(k => k.Takes(entry, all)).Select(k => k.Name).ToList();

    /// <inheritdoc/>
    public sealed override IEnumerable<Asset> Enumerate(Install install) => Enumerate(install, Discovered.Value);

    /// <summary>
    /// Lists this kind's assets with <c>all</c> as the full set of entry kinds, in archive order and then entry
    /// order. Reads each archive's header and table, never an entry's data.
    /// </summary>
    public IEnumerable<Asset> Enumerate(Install install, IReadOnlyList<ArchiveEntryKind> all)
    {
        foreach (var file in Files)
        {
            Archive? archive = null;
            Exception? problem = null;
            try
            {
                archive = Open(install, file);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                problem = e;
            }

            if (archive == null)
            {
                if (ReportsFileFailures)
                {
                    yield return new Asset
                    {
                        Kind = Name,
                        File = file,
                        Name = Path.GetFileName(file),
                        Load = () => throw problem!,
                    };
                }

                continue;
            }

            foreach (var entry in archive.Entries.Where(e => e.State != EntryState.Absent))
            {
                var claimants = ClaimedBy(entry.AliasOf ?? entry.Number, all);
                if (!claimants.Contains(Name))
                {
                    continue;
                }

                var strays =
                    entry.AliasOf == null ? [] : all.Where(k => k.Claims(entry.Number)).Select(k => k.Name).ToList();
                yield return new Asset
                {
                    Kind = Name,
                    File = file,
                    Entry = entry.Number.ToString(),
                    Name = Label(file, entry),
                    IsAlias = entry.State == EntryState.Alias,
                    Load =
                        claimants.Count > 1
                            ? () =>
                                throw new InvalidDataException(
                                    $"entry {entry.Number} is claimed by more than one kind: {string.Join(", ", claimants)}"
                                )
                        : strays.Any()
                            ? () =>
                                throw new InvalidDataException(
                                    $"entry {entry.Number} is a copy of entry {entry.AliasOf} and follows it, yet {string.Join(", ", strays)} claims the copy"
                                )
                        : () => Read(archive, entry, archive.Data(entry)),
                };
            }
        }
    }

    static Archive Open(Install install, string file) =>
        install.Exists(file)
            ? ArchiveFile.Read(install.ReadAll(file))
            : throw new FileNotFoundException($"the archive {file} is missing: the install must hold it");
}
