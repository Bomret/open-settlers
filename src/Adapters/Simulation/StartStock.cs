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

    // What the castle's construction and its 20 first settlers take from the stock, by good: the planks and
    // stones the castle's own site uses (goods 7 and 9) and the tools the settlers carry out. inferred: the
    // vector is the difference between the interpolated rows and the castle stock of the emulator (castle:, 7 start
    // stock levels in castle-placements.txt), not read from the routine's own subtractions; docs/rules/castle.md
    // states its origin. Upgrade path: read the subtractions of 0x442704 and its callees.
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
    /// its value in the lower row towards the higher row of the program's table (<see cref="ProgramTables.StartStock"/>)
    /// by the tenths of the level, rounded as the original rounds.
    /// </summary>
    public static IReadOnlyList<int> At(ProgramTables tables, int level)
    {
        var low =
            level < 10 ? 0
            : level < 20 ? 1
            : level < 30 ? 2
            : level < 40 ? 3
            : 4;
        var high = low == 4 ? 4 : low + 1;
        var weight = (level - 10 * low) * 0x199a & 0xffff;
        var rows = tables.StartStock;
        var stock = new int[Goods];
        for (var good = 0; good < Goods; good++)
        {
            var from = rows[low][good];
            var difference = (ushort)(rows[high][good] - from);
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
        var generator = new RandomGenerator(
            (ushort)(seed.W0 ^ 0x5a5a),
            (ushort)(seed.W1 ^ 0xa5a5),
            (ushort)(seed.W2 ^ 0xc3c3)
        );

        // The draws, in order: 4 rounds of 4 for the four opponents' settings, then the pair for player 1 and the
        // pair for player 0, whose first draw is the human's level.
        for (var draw = 1; draw <= 19; draw++)
        {
            generator = generator.Step();
        }

        return generator.W0 * 0x29 >> 16;
    }
}
