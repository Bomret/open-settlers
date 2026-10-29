using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Assets;

/// <summary>The asset check's outcome: the process exit code and the report, one line each.</summary>
public sealed record CheckReport
{
    /// <summary>0 when every entry was read, 1 when an entry cannot be read or a listed file is missing, 2 when there is no usable install or two kinds share a name.</summary>
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
    /// Runs the check of <c>kinds</c> over the install the launch arguments name. Exit code 2 (no install given,
    /// folder missing, no program in it, a launch problem, or two kinds of one name) wins over 1. A kind or an asset that throws fails only
    /// itself; the check goes on with the rest. A file that fails as a whole is reported once, however many kinds
    /// list it. Never throws.
    /// </summary>
    public static CheckReport Run(LaunchArgs args, IEnumerable<AssetKind> kinds)
    {
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            return new CheckReport { ExitCode = 2, Lines = [$"ERROR {problem}"] };
        }

        kinds = kinds.ToList();
        if (AssetKind.NameClash(kinds) is { } clash)
        {
            return new CheckReport { ExitCode = 2, Lines = [$"ERROR {clash}"] };
        }

        var files = new Dictionary<string, FileTally>();
        var failures = new List<string>();
        // The files that failed as a whole, such as a missing archive that every archive kind lists.
        var failedFiles = new HashSet<string>();
        foreach (var kind in kinds)
        {
            List<Asset> assets;
            try
            {
                assets = kind.Enumerate(install).ToList();
            }
            catch (Exception e)
            {
                failures.Add($"FAIL {kind.Name}: cannot list the assets: {e.Message}");

                continue;
            }

            foreach (var asset in assets)
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
                catch (Exception e)
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
