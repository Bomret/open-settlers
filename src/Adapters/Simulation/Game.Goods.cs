using System;

namespace OpenSettlers.Simulation;

// The goods on the flags are the goods behaviour's (GoodsTransport, docs/rules/goods.md); the game keeps the castle's stock the
// carriers' deliveries count in.
public sealed partial class Game
{
    /// <summary>The logs the castle holds: the count of its storage item for the good 7.</summary>
    public int CastleLogs => Stock.Count > GoodLog ? Stock[GoodLog] : 0;

    /// <summary>The goods on the flags: their destinations, directions, and the exchange with the carriers.</summary>
    internal GoodsTransport Goods => _goods;

    private void AddToStock(int good)
    {
        int[] stock = [.. Stock];
        stock[good] = Math.Min(stock[good] + 1, 50000);
        Stock = stock;
    }

    private void MixGoods(ref ulong hash)
    {
        foreach (var word in FlagEncoder.Words(_flags))
        {
            hash = Mix(hash, word);
        }
    }
}
