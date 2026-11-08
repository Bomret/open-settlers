using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>The original's spiral table of offsets around a node, which the generator, the placements, and the settlers' searches read.</summary>
public static class Spiral
{
    /// <summary>The column and row offsets around a node in rings: the node itself, then rings of 6, 12, and on (entries 0 to 255).</summary>
    public static IReadOnlyList<(int Dx, int Dy)> Offsets { get; } = Build(256);

    /// <summary>
    /// The same table to its entry 258, which the search for the nearest flag reads (0x4353ef walks entries 1 to 258 from
    /// either end); entries 256 to 258 are the last rotations of the group of six that entry 253 starts.
    /// </summary>
    public static IReadOnlyList<(int Dx, int Dy)> SearchOffsets { get; } = Build(259);

    // The original builds the table at start-up (0x41e726) from a seed offset for each group of six: the six
    // rotations (x, y) -> (x - y, x) of the seed. Ring r holds the seeds (r, j) for j from 1 to r - 1, nearest
    // to the middle of the ring first (ties: the lower j), then (r, 0). The passes index at most 255.
    private static List<(int Dx, int Dy)> Build(int count)
    {
        var table = new List<(int Dx, int Dy)> { (0, 0) };
        for (var ring = 1; table.Count < count; ring++)
        {
            var seeds = Enumerable.Range(1, ring - 1).OrderBy(j => Math.Abs(2 * j - ring)).ThenBy(j => j).Append(0);
            foreach (var j in seeds)
            {
                var (x, y) = (ring, j);
                for (var turn = 0; turn < 6; turn++)
                {
                    table.Add((x, y));
                    (x, y) = (x - y, x);
                }
            }
        }

        table.RemoveRange(count, table.Count - count);

        return table;
    }
}
