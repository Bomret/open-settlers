using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>The player's castle and the goods in it: the one home of both.</summary>
/// <param name="catalog">The content catalog of the game: the stock holds a place for each of its goods.</param>
public sealed class CastleStock(ContentCatalog catalog)
{
    /// <summary>The cap of a good's count in the castle.</summary>
    const int Cap = 50000;

    /// <summary>The castle of the one player, or null before it is placed.</summary>
    public Castle? Castle { get; private set; }

    /// <summary>The content catalog the stock is keyed by.</summary>
    public ContentCatalog Catalog { get; } = catalog;

    /// <summary>
    /// The goods in the castle: the count of each entry of the catalog, in the order of <see cref="ContentCatalog.Goods"/> (entry
    /// <c>i</c> of the catalog has the count <c>i</c>). Empty before the castle is placed.
    /// </summary>
    public IReadOnlyList<int> Stock { get; private set; } = [];

    /// <summary>The logs the castle holds.</summary>
    public int Logs => Count(Good.Log);

    /// <summary>The planks the castle holds.</summary>
    public int Planks => Count(Good.Plank);

    /// <summary>The stones the castle holds.</summary>
    public int Stones => Count(Good.Stone);

    /// <summary>The counts of the six food goods (<see cref="Good.Foods"/>) in the castle, in the order of the food readout.</summary>
    public IReadOnlyList<int> FoodCounts => [.. Good.Foods.Select(Count)];

    /// <summary>
    /// The count of a good in the castle; 0 for no good and before the castle is placed. Throws for a good the catalog lacks, naming it.
    /// </summary>
    public int Count(Good good) => PlaceOf(good) is { } place && place < Stock.Count ? Stock[place] : 0;

    /// <summary>
    /// The castle stands, holding <paramref name="stock"/>: a count for each good it names and 0 for every other good of the catalog.
    /// Throws for a good the catalog lacks, naming it.
    /// </summary>
    public void Place(Castle castle, IReadOnlyDictionary<Good, int> stock)
    {
        var counts = new int[Catalog.Goods.Length];
        foreach (var (good, count) in stock)
        {
            counts[PlaceOf(good) ?? throw new ArgumentException("the start stock names no good")] = count;
        }

        Castle = castle;
        Stock = counts;
    }

    /// <summary>The castle takes one good in, up to the 50000 its count holds.</summary>
    public void Add(Good good) => Change(good, 1);

    /// <summary>The castle gives one good out of its stock; it tells whether the count held one.</summary>
    public bool Take(Good good)
    {
        if (Count(good) == 0)
        {
            return false;
        }

        Change(good, -1);

        return true;
    }

    // The place of the good's entry in the catalog's order; null for no good. A good the catalog lacks is an error.
    int? PlaceOf(Good good) => good.IsNone ? null : _places[Catalog.Require(good).Name];

    readonly Dictionary<string, int> _places = catalog
        .Goods.Select((entry, place) => (entry.Name, place))
        .ToDictionary(entry => entry.Name, entry => entry.place);

    void Change(Good good, int by)
    {
        if (PlaceOf(good) is not { } place || place >= Stock.Count)
        {
            return;
        }

        int[] stock = [.. Stock];
        stock[place] = Math.Min(stock[place] + by, Cap);
        Stock = stock;
    }
}
