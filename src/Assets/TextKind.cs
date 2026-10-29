using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>One key of the text table and its value in each language, in <see cref="TextPreview.Languages"/> order; null where the language has no such key.</summary>
public sealed record TextRow(string Key, IReadOnlyList<string?> Values);

/// <summary>The preview of the texts: one table of every key, with one value column per language.</summary>
public sealed record TextPreview : Preview
{
    /// <summary>The languages, one column each, in the program's order.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>The keys in order of first appearance across the languages.</summary>
    public required IReadOnlyList<TextRow> Rows { get; init; }
}

/// <summary>
/// The text files <c>loca/eng.txt</c>, <c>loca/ger.txt</c>, <c>loca/fre.txt</c> (<c>docs/rules/text-files.md</c>).
/// The check reports per file, so each file is one asset. Every asset previews the same table across the three
/// files; its <see cref="Asset.Load"/> fails only for a line of its own file the program does not accept, so a bad
/// file stops neither the check of the others nor their table.
/// </summary>
public sealed class TextKind : AssetKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "text";

    // The languages and their files in the order the program's language table holds them.
    static readonly (string Language, string File)[] Languages =
    [
        ("eng", "loca/eng.txt"),
        ("ger", "loca/ger.txt"),
        ("fre", "loca/fre.txt"),
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override IEnumerable<Asset> Enumerate(Install install) =>
        Languages.Select(
            (l, index) =>
                new Asset
                {
                    Kind = KindName,
                    File = l.File,
                    Name = Path.GetFileName(l.File),
                    Load = () => Table(install, index),
                }
        );

    // Each file is read once. A file that cannot be read leaves its column empty; its own asset fails with the reason.
    static Preview Table(Install install, int own)
    {
        var reads = Languages.Select(l => Read(install, l.File)).ToList();
        if (reads[own].Error is { } error)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        var rejected = reads[own].File!.Rejected;
        if (rejected.Count > 0)
        {
            throw new InvalidDataException(string.Join("; ", rejected.Select(r => $"line {r.Line}: {r.Reason}")));
        }

        var files = reads.Select(r => r.File).ToList();
        var keys = files.SelectMany(f => f?.Entries.Select(e => e.Key) ?? []).Distinct();
        var rows = keys.Select(k => new TextRow(k, files.Select(f => f?.Get(k)).ToList())).ToList();

        return new TextPreview
        {
            Languages = Languages.Select(l => l.Language).ToList(),
            Rows = rows,
            Summary = $"{rows.Count} keys in {Languages.Length} languages",
        };
    }

    static (TextFile? File, Exception? Error) Read(Install install, string file)
    {
        try
        {
            return (TextFile.Read(install.ReadAll(file)), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, e);
        }
    }
}
