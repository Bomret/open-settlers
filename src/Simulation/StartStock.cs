using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The goods a castle holds when it is placed (<c>docs/rules/castle.md</c>): 26 counts, interpolated by the
/// player's start stock level between rows of the original's table, less what building the castle and equipping
/// its first settlers take.
/// </summary>
public static class StartStock
{
    /// <summary>The number of goods the castle counts.</summary>
    public const int Goods = 26;

    // The five rows of the original's table (0x442e8a): the stock at the levels 0, 10, 20, 30, and 40.
    private static readonly int[][] Rows =
    [
        [0, 0, 0, 0, 0, 0, 0, 7, 0, 2, 0, 0, 0, 0, 0, 1, 6, 1, 0, 0, 1, 2, 3, 0, 10, 10],
        [2, 1, 1, 3, 2, 1, 0, 25, 1, 8, 4, 3, 8, 2, 1, 3, 12, 2, 1, 1, 2, 3, 4, 1, 30, 30],
        [3, 2, 2, 10, 3, 1, 0, 40, 2, 20, 12, 8, 20, 4, 2, 5, 20, 3, 1, 2, 3, 4, 6, 2, 60, 60],
        [8, 4, 6, 20, 7, 5, 3, 80, 5, 40, 20, 40, 50, 8, 4, 10, 30, 5, 2, 4, 6, 6, 12, 4, 100, 100],
        [30, 10, 30, 50, 10, 30, 10, 200, 10, 100, 30, 150, 100, 10, 5, 20, 50, 10, 5, 10, 20, 20, 50, 10, 200, 200],
    ];

    // What the castle's construction and its 20 first settlers take from the stock, by good: the planks and
    // stones the castle's own site uses (goods 7 and 9) and the tools the settlers carry out.
    private static readonly (int Good, int Count)[] Taken =
    [
        (7, 7),
        (9, 2),
        (15, 1),
        (16, 4),
        (17, 1),
        (20, 1),
        (21, 2),
        (22, 3),
        (24, 3),
        (25, 3),
    ];

    /// <summary>
    /// The stock of a castle at a start stock level (0 to 255; the free game draws 0 to 40). Each count steps from
    /// its value in the lower row towards the higher row by the tenths of the level, rounded as the original rounds.
    /// </summary>
    public static IReadOnlyList<int> At(int level)
    {
        var low = level < 10 ? 0 : level < 20 ? 1 : level < 30 ? 2 : level < 40 ? 3 : 4;
        var high = low == 4 ? 4 : low + 1;
        var weight = (level - 10 * low) * 0x199a & 0xffff;
        var stock = new int[Goods];
        for (var good = 0; good < Goods; good++)
        {
            var from = Rows[low][good];
            var difference = (ushort)(Rows[high][good] - from);
            var product = (uint)difference * (uint)weight;
            var rounded = (product & 0xffff) > 0x7fff ? 1 : 0;
            stock[good] = (ushort)(from + rounded + (int)(product >> 16));
        }

        foreach (var (good, count) in Taken)
        {
            stock[good] = (ushort)(stock[good] - count);
        }

        return stock;
    }

    /// <summary>
    /// The start stock level of the human player of a free game: the 19th draw of <c>FreeGame_RandomizeOpponents</c>
    /// (<c>0x4692d4</c>) from the free-game seed, scaled to 0 to 40. The original runs it whenever the free-game
    /// screen opens.
    /// </summary>
    public static int LevelOf(Seed seed)
    {
        var generator = new RandomGenerator((ushort)(seed.W0 ^ 0x5a5a), (ushort)(seed.W1 ^ 0xa5a5), (ushort)(seed.W2 ^ 0xc3c3));

        // The draws, in order: 4 rounds of 4 for the four opponents' settings, then the pair for player 1 and the
        // pair for player 0, whose first draw is the human's level.
        for (var draw = 1; draw <= 19; draw++)
        {
            generator = generator.Step();
        }

        return generator.W0 * 0x29 >> 16;
    }
}
