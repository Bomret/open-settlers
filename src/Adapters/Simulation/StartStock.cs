using System;
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
    // What the castle's construction and its 20 first settlers take from the stock, by the good's stable name: the planks and
    // stones the castle's own site uses (the plank and the stone) and the tools the settlers carry out. Confirmed for the planks
    // and stones: 0x442704 subtracts 7 and 2 itself. inferred for the tools: they are the difference between the interpolated rows
    // and the castle stock of the emulator (castle:, 7 start stock levels in castle-placements.txt), not read from the
    // subtractions of 0x442704's callees; docs/rules/castle.md states their origin. Upgrade path: read those callees'
    // subtractions.
    static readonly (string Good, int Count)[] Taken =
    [
        ("plank", 7),
        ("stone", 2),
        ("shovel", 1),
        ("hammer", 4),
        ("fishing-rod", 1),
        ("hatchet", 1),
        ("saw", 2),
        ("pickaxe", 3),
        ("sword", 3),
        ("shield", 3),
    ];

    /// <summary>
    /// The stock of a castle at a start stock level (0 to 255; the free game draws 0 to 40), a count for each good that the rows of
    /// the program's table name. Throws an <see cref="InvalidOperationException"/> that names the good for a row good the catalog lacks, and
    /// for a deduction of the castle whose good no row holds. Each count steps from its value in the lower row towards the higher row
    /// (<see cref="RuleTables.StartStock"/>) by the tenths of the level, rounded as the original rounds.
    /// </summary>
    public static IReadOnlyDictionary<Good, int> At(RuleTables tables, int level)
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
        var stock = new Dictionary<Good, int>();
        foreach (var (good, from) in rows[low])
        {
            tables.Catalog.Require(good);

            var difference = (ushort)(rows[high].GetValueOrDefault(good) - from);
            var product = (uint)difference * (uint)weight;
            var rounded = (product & 0xffff) > 0x7fff ? 1 : 0;
            stock[good] = (ushort)(from + rounded + (int)(product >> 16));
        }

        foreach (var (name, count) in Taken)
        {
            var good = new Good { Name = name };
            if (!stock.TryGetValue(good, out var held))
            {
                throw new InvalidOperationException(
                    $"The castle's start stock takes {count} of the good '{name}' away, and the start stock rows hold no such good."
                );
            }

            stock[good] = (ushort)(held - count);
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
        var generator = new RandomGenerator
        {
            W0 = (ushort)(seed.W0 ^ 0x5a5a),
            W1 = (ushort)(seed.W1 ^ 0xa5a5),
            W2 = (ushort)(seed.W2 ^ 0xc3c3),
        };

        // The draws, in order: 4 rounds of 4 for the four opponents' settings, then the pair for player 1 and the
        // pair for player 0, whose first draw is the human's level.
        for (var draw = 1; draw <= 19; draw++)
        {
            generator = generator.Step();
        }

        return generator.W0 * 0x29 >> 16;
    }
}
