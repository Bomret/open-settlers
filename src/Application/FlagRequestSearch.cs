using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The search of the flag whose request ranks highest, shared by the good that looks for a request (<c>0x4644ab</c> to <c>0x464a3f</c>)
/// and the storage's hand-out (<c>0x429a05</c> to <c>0x42a0ad</c>), <c>docs/rules/construction-materials.md</c>: from a flag, level by
/// level over the roads that hold a carrier (the directions from north to east), for the flag whose request word asks for the class
/// with a rank above a floor and above the best so far. After each level the best rank grows by a quarter and one (<c>0x464906</c>),
/// and the search ends where that carries out of the byte or no flag is left, so a farther request must rank above what the nearer has
/// grown to.
/// </summary>
internal static class FlagRequestSearch
{
    /// <summary>Finds the flag; -1 where no flag asks for the class with a rank above <paramref name="floor"/>.</summary>
    public static int Find(FlagTable flags, RoadNetwork roads, int start, GoodClass goodClass, int floor)
    {
        var marked = new bool[flags.Count];
        marked[start] = true;
        var level = new List<int> { start };
        var best = 0;
        var found = -1;
        while (best != 0xff)
        {
            var next = new List<int>();
            foreach (var current in level)
            {
                for (var direction = 5; direction >= 0; direction--)
                {
                    if (
                        !flags[current].Side(direction).HoldsCarrier
                        || roads.FlagNeighbour(current, direction) is not (>= 0 and var other)
                        || marked[other]
                    )
                    {
                        continue;
                    }

                    marked[other] = true;
                    next.Add(other);
                    var request = flags[other].RequestOf(goodClass.Word);
                    if (request.Asks == goodClass && request.Rank > floor && best < request.Rank)
                    {
                        best = request.Rank;
                        found = other;
                    }
                }
            }

            if (next.Count == 0)
            {
                break;
            }

            level = next;
            if (best != 0)
            {
                best = System.Math.Min(best + (best >> 2) + 1, 0xff);
            }
        }

        return found;
    }
}
