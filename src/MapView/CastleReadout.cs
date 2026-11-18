using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

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
    /// <paramref name="food"/> in the order of <c>Good.Foods</c>, then each of <paramref name="added"/> by its stable name and count.
    /// </summary>
    public static string Text(
        int logs,
        int planks,
        int stones,
        IReadOnlyList<int> food,
        IReadOnlyList<(string Name, int Count)>? added = null
    ) =>
        $"Logs {logs}  Planks {planks}  Stones {stones}"
        + string.Concat(
            PlayFields.Food.Select(
                (field, index) =>
                    $"  {char.ToUpperInvariant(field[0])}{field[1..]} {food[index].ToString(CultureInfo.InvariantCulture)}"
            )
        )
        + string.Concat(
            (added ?? []).Select(good => $"  {good.Name} {good.Count.ToString(CultureInfo.InvariantCulture)}")
        );

    /// <summary>
    /// The goods the castle holds that have no original code, an added good, each with its count, in the catalog's order of stable names.
    /// A good the stock holds none of is left out, so the readout of the original's content is as it was.
    /// </summary>
    public static IReadOnlyList<(string Name, int Count)> AddedGoods(CastleStock stock) =>
        [
            .. stock
                .Catalog.Goods.Where(entry => entry.OriginalCode is null)
                .Select(entry => (entry.Name, Count: stock.Count(new Good { Name = entry.Name })))
                .Where(good => good.Count > 0),
        ];
}
