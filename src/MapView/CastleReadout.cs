using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenSettlers.MapView;

/// <summary>
/// The stand-in readout of the castle's log, plank, stone, and food counts in the play view. The original shows stock in its own window,
/// a later slice's; this text only lets a player see a delivery arrive and a site's materials leave.
/// </summary>
public static class CastleReadout
{
    /// <summary>
    /// The text for a castle holding <paramref name="logs"/> logs, <paramref name="planks"/> planks, and <paramref name="stones"/> stones,
    /// then the count of each food good, written with the capitalized word of its <see cref="PlayFields.Food"/> field, the six of
    /// <paramref name="food"/> in the order of <c>Good.Foods</c>.
    /// </summary>
    public static string Text(int logs, int planks, int stones, IReadOnlyList<int> food) =>
        $"Logs {logs}  Planks {planks}  Stones {stones}"
        + string.Concat(
            PlayFields.Food.Select(
                (field, index) =>
                    $"  {char.ToUpperInvariant(field[0])}{field[1..]} {food[index].ToString(CultureInfo.InvariantCulture)}"
            )
        );
}
