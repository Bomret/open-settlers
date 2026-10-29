using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Assets;

/// <summary>The asset check's outcome: the process exit code and the report, one line each.</summary>
public sealed record CheckReport
{
    /// <summary>0 when every entry was read, 1 when an entry cannot be read or a listed file is missing, 2 when there is no usable install.</summary>
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
    /// folder missing, no program in it, or a launch problem) wins over 1. A kind or an asset that throws fails only
    /// itself; the check goes on with the rest. Never throws.
    /// </summary>
    public static CheckReport Run(LaunchArgs args, IEnumerable<AssetKind> kinds)
    {
        if (!Install.TryOpen(args, out var install, out var problem)) {
            return new CheckReport { ExitCode = 2, Lines = [$"ERROR {problem}"] };
        }

        var files = new Dictionary<string, FileTally>();
        var failures = new List<string>();
        foreach (var kind in kinds) {
            List<Asset> assets;
            try {
                assets = kind.Enumerate(install).ToList();
            } catch (Exception e) {
                failures.Add($"FAIL {kind.Name}: cannot list the assets: {e.Message}");

                continue;
            }

            foreach (var asset in assets) {
                try {
                    asset.Load();
                    if (!files.TryGetValue(asset.File, out var tally)) {
                        files[asset.File] = tally = new FileTally();
                    }

                    tally.Entries++;
                    if (!tally.Kinds.Contains(asset.Kind)) {
                        tally.Kinds.Add(asset.Kind);
                    }
                } catch (Exception e) {
                    var entry = asset.Entry == null ? "" : $" entry {asset.Entry}";
                    failures.Add($"FAIL {asset.File}{entry}: {e.Message}");
                }
            }
        }

        var lines = files
            .Select(f =>
                $"{f.Key}: {f.Value.Entries} {(f.Value.Entries == 1 ? "entry" : "entries")} ({string.Join(", ", f.Value.Kinds)})"
            )
            .Concat(failures)
            .ToList();
        lines.Add($"{files.Count} files read, {files.Values.Sum(t => t.Entries)} entries read, {failures.Count} failed");

        return new CheckReport { ExitCode = failures.Count == 0 ? 0 : 1, Lines = lines };
    }

    sealed class FileTally
    {
        public int Entries;
        public readonly List<string> Kinds = [];
    }
}
