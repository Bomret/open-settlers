using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>The player's castle and the goods in it: the one home of both.</summary>
public sealed class CastleStock
{
    /// <summary>The castle of the one player, or null before it is placed.</summary>
    public Castle? Castle { get; private set; }

    /// <summary>The goods in the castle, <see cref="StartStock.Goods"/> counts; empty before the castle is placed.</summary>
    public IReadOnlyList<int> Stock { get; private set; } = [];

    /// <summary>The logs the castle holds: the count of its storage item for the good 7.</summary>
    public int Logs => Stock.Count > WoodcutterBehavior.GoodLog ? Stock[WoodcutterBehavior.GoodLog] : 0;

    /// <summary>The castle stands, holding <paramref name="stock"/>.</summary>
    public void Place(Castle castle, IReadOnlyList<int> stock)
    {
        Castle = castle;
        Stock = stock;
    }

    /// <summary>The castle takes one good in, up to the 50000 its count holds.</summary>
    public void Add(Good good)
    {
        int[] stock = [.. Stock];
        stock[good.Code] = Math.Min(stock[good.Code] + 1, 50000);
        Stock = stock;
    }
}
