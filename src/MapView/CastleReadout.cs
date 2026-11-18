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
    /// The goods the castle holds that the content set's start stock does not list, an added good, each with its count, in the
    /// catalog's order of stable names. A good that any row of <paramref name="startStock"/> names is listed
    /// (<c>RuleTables.StartStock</c>, the rows each content set fills with every good it has, zeros included). The rule reads that
    /// content data and no good's name or original code, so the bar of either content set at game start is the nine fields and
    /// nothing more. An added good a set gave a start count would list there and stay out of the bar. A good the stock holds none
    /// of is left out.
    /// </summary>
    public static IReadOnlyList<(string Name, int Count)> AddedGoods(
        CastleStock stock,
        IEnumerable<IReadOnlyDictionary<Good, byte>> startStock
    )
    {
        var listed = startStock.SelectMany(row => row.Keys).Select(good => good.Name).ToHashSet();

        return
        [
            .. stock
                .Catalog.Goods.Where(entry => !listed.Contains(entry.Name))
                .Select(entry => (entry.Name, Count: stock.Count(new Good { Name = entry.Name })))
                .Where(good => good.Count > 0),
        ];
    }
}
