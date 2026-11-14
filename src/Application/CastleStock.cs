using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>The player's castle and the goods in it: the one home of both.</summary>
public sealed class CastleStock
{
    /// <summary>The castle of the one player, or null before it is placed.</summary>
    public Castle? Castle { get; private set; }

    /// <summary>
    /// The goods in the castle: the 26 counts of the storage item from its word at 6 (<see cref="StartStock.Goods"/> of them), so
    /// the count of the good <c>g</c> is the entry <c>g - 1</c> (the word for a log, the good 7, lies at <c>0x12</c> of the
    /// item). Empty before the castle is placed.
    /// </summary>
    public IReadOnlyList<int> Stock { get; private set; } = [];

    /// <summary>The logs the castle holds.</summary>
    public int Logs => Count(Good.Log);

    /// <summary>The planks the castle holds.</summary>
    public int Planks => Count(Good.Plank);

    /// <summary>The stones the castle holds.</summary>
    public int Stones => Count(Good.Stone);

    /// <summary>The count of a good in the castle; 0 for a number that is no good of the stock, and before the castle is placed.</summary>
    public int Count(Good good) => Holds(good) ? Stock[good.Code - 1] : 0;

    /// <summary>The castle stands, holding <paramref name="stock"/>.</summary>
    public void Place(Castle castle, IReadOnlyList<int> stock)
    {
        Castle = castle;
        Stock = stock;
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

    bool Holds(Good good) => good.Code >= 1 && good.Code <= Stock.Count;

    void Change(Good good, int by)
    {
        if (!Holds(good))
        {
            return;
        }

        int[] stock = [.. Stock];
        stock[good.Code - 1] = Math.Min(stock[good.Code - 1] + by, 50000);
        Stock = stock;
    }
}
