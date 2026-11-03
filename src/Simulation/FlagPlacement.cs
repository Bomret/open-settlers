using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// What putting a flag on a node writes into the map (<c>0x4422c5</c>, <c>docs/rules/flags.md</c>): the flag bit and
/// the flag object on the node, the link to its slot in the flag table, and the resource deposit the link displaces.
/// </summary>
internal static class FlagPlacement
{
    // The object code of a flag.
    private const int FlagCode = 1;

    // The nodes that take the node's deposit, in the order 0x449f58 offers it: east, north-east, north, the node
    // itself (it holds the flag object by then, so it takes nothing), south, and south-east.
    private static readonly (int Dx, int Dy)[] DepositTargets = [(1, 0), (1, -1), (0, -1), (0, 0), (0, 1), (1, 1)];

    /// <summary>
    /// Puts a flag of table slot <paramref name="slot"/> (a byte: the link keeps one) on the node, which the build-site
    /// evaluator judged to allow it.
    /// </summary>
    internal static void Apply(Map map, int x, int y, int slot)
    {
        var flags = map.Bytes(MapLayer.Flags);
        var objects = map.Bytes(MapLayer.Object);
        var links = map.Bytes(MapLayer.Resource);

        var node = map.Index(x, y);
        objects[node] = (byte)((objects[node] & 0x80) | FlagCode);
        flags[node] |= 0x80;
        MoveDeposit(map, x, y);
        links[node] = (byte)slot;
    }

    // The link replaces the byte that holds the node's resource deposit (or lake detail). The deposit moves to the
    // nodes around, which add it to theirs where it has the same type; what no node takes is lost, and the total of
    // resource 1 falls by it.
    private static void MoveDeposit(Map map, int x, int y)
    {
        var objects = map.Bytes(MapLayer.Object);
        var deposits = map.Bytes(MapLayer.Resource);
        var value = deposits[map.Index(x, y)];
        if (value == 0)
        {
            return;
        }

        var type = value & 0xe0;
        var carry = value & 0x1f;
        for (var i = 0; i < DepositTargets.Length && (i == 0 || carry != 0); i++)
        {
            var target = map.Index(x + DepositTargets[i].Dx, y + DepositTargets[i].Dy);
            var code = objects[target] & 0x7f;
            if (code is >= 1 and <= 4)
            {
                continue;
            }

            if (deposits[target] == 0)
            {
                deposits[target] = (byte)(carry | type);
                carry = 0;
            }
            else if ((deposits[target] & 0xe0) == type)
            {
                var sum = (deposits[target] & 0x1f) + carry;
                deposits[target] = (byte)(Math.Min(sum, 0x1f) | type);
                carry = Math.Max(sum - 0x1f, 0);
            }
        }

        if (carry != 0 && type == 0x20)
        {
            map.Resource1Total -= carry;
        }
    }
}
