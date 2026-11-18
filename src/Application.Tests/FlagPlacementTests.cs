using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The deposit move of the flag placement on a hand-made map (the emulator-backed cases are in the class of the same name in
// src/Adapters.Tests).
[TestClass]
public sealed class FlagPlacementTests
{
    [TestMethod]
    public void ADepositOfZeroAmountWithTypeBitsMovesToTheFirstEmptyNeighbour()
    {
        // Inferred: read from 0x449f58 and 0x44a0aa, no emulator run backs it (open lead in
        // docs/milestones/m3-first-loop.md). The first neighbour is offered the deposit even when its amount is 0, so
        // the type bits move and the amount 0 with them.
        var map = new Map(MapSize.Offered[0]);
        map.At(5, 5).DepositType = 1;

        FlagPlacement.Apply(map, 5, 5, 3);

        Assert.AreEqual(1, map.At(6, 5).DepositType);
        Assert.AreEqual(0, map.At(6, 5).DepositAmount);
    }

    [TestMethod]
    public void ADepositMovesAcrossTheColumnWrap()
    {
        // Inferred: read from 0x449f58, no emulator run backs it (open lead in docs/milestones/m3-first-loop.md). The
        // first neighbour is east, which wraps from the last column to the first.
        var map = new Map(MapSize.Offered[0]);
        var last = map.Size.Width - 1;
        map.At(last, 5).DepositType = 1;
        map.At(last, 5).DepositAmount = 3;

        FlagPlacement.Apply(map, last, 5, 3);

        Assert.AreEqual(1, map.At(0, 5).DepositType);
        Assert.AreEqual(3, map.At(0, 5).DepositAmount);
    }
}
