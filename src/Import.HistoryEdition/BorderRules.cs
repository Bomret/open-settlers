using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// How the original picks the image of a border post (<c>docs/rules/roads.md</c>, "How a border is drawn"): the routines
/// <c>0x428389</c>, <c>0x42846d</c>, and <c>0x428528</c>, which share the bands. The History Edition source applies it to
/// a <see cref="BorderPost"/>. The bands are the border's own, not those of <see cref="RoadRules"/>.
/// </summary>
static class BorderRules
{
    // The entry of a post whose slope sum is 2 or more; the next two take a sum from -8 to 1 and one below -8.
    const int SteepEntry = 610;
    const int WaterEntry = 619;
    const int SteepFrom = 2;
    const int MiddleFrom = -8;

    // The ground codes from which the entry is raised by 3, and by 6; a ground below 4 is water.
    const int RaiseBy3From = 11;
    const int RaiseBy6From = 15;

    /// <summary>The entries of the border posts, 610 to 619.</summary>
    public static IEnumerable<int> Entries => Enumerable.Range(SteepEntry, WaterEntry - SteepEntry + 1);

    /// <summary>The archive entry of a post: by its slope sum, raised by its ground, or the water post.</summary>
    public static int Entry(BorderPost post)
    {
        if (post.Ground.IsWater)
        {
            return WaterEntry;
        }

        var entry =
            post.Slope >= SteepFrom ? SteepEntry
            : post.Slope >= MiddleFrom ? SteepEntry + 1
            : SteepEntry + 2;

        return post.Ground.Code >= RaiseBy6From ? entry + 6
            : post.Ground.Code >= RaiseBy3From ? entry + 3
            : entry;
    }
}
