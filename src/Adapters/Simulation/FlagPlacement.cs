using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// What putting a flag on a node writes into the map (<c>0x4422c5</c>, <c>docs/rules/flags.md</c>): the flag mark and
/// the flag object on the node, the link to its slot in the flag table, and the resource deposit the link displaces.
/// </summary>
internal static class FlagPlacement
{
    // The nodes that take the node's deposit, in the order 0x449f58 offers it: east, north-east, north, the node
    // itself (it holds the flag object by then, so it takes nothing), south, and south-east.
    private static readonly (int Dx, int Dy)[] DepositTargets = [(1, 0), (1, -1), (0, -1), (0, 0), (0, 1), (1, 1)];

    // The deposit a node holds at most.
    private const int MostAmount = 0x1f;

    /// <summary>
    /// Puts a flag of table slot <paramref name="slot"/> (a byte: the link keeps one) on the node, which the build-site
    /// evaluator judged to allow it.
    /// </summary>
    internal static void Apply(Map map, int x, int y, int slot)
    {
        ref var node = ref map.At(x, y);
        node.Object = MapObject.Flag;
        node.FlagMark = true;
        MoveDeposit(map, x, y);
        node.LinkTo(slot);
    }

    // **Inferred:** a deposit of amount 0 that keeps type bits still moves to the first empty neighbour, and the move
    // crosses the column wrap; both are read from 0x449f58 and no emulator run backs them.
    // The link replaces the deposit (or lake detail) of the node. The deposit moves to the nodes around, which add it
    // to theirs where it has the same type; what no node takes is lost, and the total of resource 1 falls by it.
    private static void MoveDeposit(Map map, int x, int y)
    {
        var source = map.At(x, y);
        if (source.DepositType == 0 && source.DepositAmount == 0)
        {
            return;
        }

        var type = source.DepositType;
        var carry = source.DepositAmount;
        for (var i = 0; i < DepositTargets.Length && (i == 0 || carry != 0); i++)
        {
            ref var target = ref map.At(x + DepositTargets[i].Dx, y + DepositTargets[i].Dy);
            if (target.Object.IsStructure)
            {
                continue;
            }

            if (target.DepositType == 0 && target.DepositAmount == 0)
            {
                target.DepositType = type;
                target.DepositAmount = carry;
                carry = 0;
            }
            else if (target.DepositType == type)
            {
                var sum = target.DepositAmount + carry;
                target.DepositAmount = Math.Min(sum, MostAmount);
                carry = Math.Max(sum - MostAmount, 0);
            }
        }

        if (carry != 0 && type == 1)
        {
            map.Resource1Total -= carry;
        }
    }
}
