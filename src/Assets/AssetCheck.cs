using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Assets;

/// <summary>The asset check's outcome: the process exit code and the report, one line each.</summary>
public sealed record CheckReport
{
    /// <summary>0 when every entry was read, 1 when an entry cannot be read or a listed file is missing, 2 when there is no usable install, 3 when two kinds share a name, 4 when a launch argument is bad.</summary>
    public required int ExitCode { get; init; }

    /// <summary>The report lines, in the order the check wrote them.</summary>
    public required IReadOnlyList<string> Lines { get; init; }
}

/// <summary>
/// The asset check: reads every asset of every kind and reports per file its entry count and kinds, and per failure
/// the file, the entry, and the reason. It reports nothing about install files that no kind lists, so a file the
/// program does not read never appears. Godot-free; the view prints the lines and sets the exit code, because a
/// Godot callback exception does not.
/// </summary>
public static class AssetCheck
{
    /// <summary>
    /// The problem with <c>kinds</c> that stops the check and the browser: two kinds of one name. Null when every
    /// name is unique.
    /// </summary>
    internal static string? NameClash(IEnumerable<IAssetKind> kinds) =>
        kinds.GroupBy(k => k.Name).FirstOrDefault(g => g.Count() > 1) is { } twice
            ? $"two asset kinds are named \"{twice.Key}\"; a kind's name must be unique"
            : null;

    /// <summary>
    /// Runs the check over the kinds of the assets port <c>assets</c>. Exit code 4 (a bad launch argument) wins over all;
    /// exit code 2 (the port has no usable data, with the reason its <c>IOException</c> gives) wins over 1; exit code 3
    /// (two kinds of one name) stops the check before it reads. A kind or an asset that throws <c>IOException</c>,
    /// <c>InvalidDataException</c> or <c>UnauthorizedAccessException</c> fails only itself; the check goes on with the
    /// rest. Any other exception propagates to the caller. A file that fails as a whole is reported once, however many
    /// kinds list it.
    /// </summary>
    public static CheckReport Run(LaunchArgs args, IAssetsSource assets)
    {
        if (args.Problems.Count > 0)
        {
            return new CheckReport
            {
                ExitCode = LaunchArgs.BadArgumentExitCode,
                Lines = [$"ERROR {string.Join("; ", args.Problems)}"],
            };
        }

        IReadOnlyList<IAssetKind> kinds;
        try
        {
            kinds = assets.Kinds();
        }
        catch (IOException e)
        {
            return new CheckReport { ExitCode = 2, Lines = [$"ERROR {e.Message}"] };
        }

        if (NameClash(kinds) is { } clash)
        {
            return new CheckReport { ExitCode = 3, Lines = [$"ERROR {clash}"] };
        }

        var files = new Dictionary<string, FileTally>();
        var failures = new List<string>();
        // The files that failed as a whole, such as a missing archive that every archive kind lists.
        var failedFiles = new HashSet<string>();
        foreach (var kind in kinds)
        {
            List<Asset> listed;
            try
            {
                listed = [.. kind.Enumerate()];
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                failures.Add($"FAIL {kind.Name}: cannot list the assets: {e.Message}");

                continue;
            }

            foreach (var asset in listed)
            {
                try
                {
                    asset.Load();
                    if (!files.TryGetValue(asset.File, out var tally))
                    {
                        files[asset.File] = tally = new FileTally();
                    }

                    if (asset.IsAlias)
                    {
                        tally.Aliases++;
                    }
                    else
                    {
                        tally.Entries++;
                    }

                    if (!tally.Kinds.Contains(asset.Kind))
                    {
                        tally.Kinds.Add(asset.Kind);
                    }
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    if (asset.Entry == null && !failedFiles.Add(asset.File))
                    {
                        continue;
                    }

                    var entry = asset.Entry == null ? "" : $" entry {asset.Entry}";
                    failures.Add($"FAIL {asset.File}{entry}: {e.Message}");
                }
            }
        }

        var lines = files
            .Select(f =>
                $"{f.Key}: {f.Value.Entries} {(f.Value.Entries == 1 ? "entry" : "entries")}{Aliases(f.Value.Aliases)} ({string.Join(", ", f.Value.Kinds)})"
            )
            .Concat(failures)
            .ToList();
        lines.Add(
            $"{files.Count} files read, {files.Values.Sum(t => t.Entries)} entries read{Aliases(files.Values.Sum(t => t.Aliases))}, {failures.Count} failed"
        );

        return new CheckReport { ExitCode = failures.Count == 0 ? 0 : 1, Lines = lines };
    }

    // The aliases of a file or of the run, counted apart from its entries; nothing when there are none.
    static string Aliases(int count) => count == 0 ? "" : $", {count} {(count == 1 ? "alias" : "aliases")}";

    sealed class FileTally
    {
        public int Entries;
        public int Aliases;
        public readonly List<string> Kinds = [];
    }
}
