using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Tests;

// The castle under construction grows by its settler's turns (state 10, 0x43f17a): each adds the elapsed units times 128 to the
// progress word. The original's view pass (0x42f4f1) gives the settler on the castle's node a turn every pass, so the castle grows
// 1024 a pass, 1/64 of its height, and not by the slice turns alone, 16 or more passes apart (docs/rules/construction.md).
[TestClass]
public sealed class CastleGrowthTests
{
    static int LargestStep(bool everyPassTurns)
    {
        var game = ConstructionWorld.Create(everyPassTurns: everyPassTurns);
        var castle = game.Buildings[0];
        Assert.IsTrue(castle.Constructing, "the castle is under construction after its placement");
        var largest = 0;
        var last = castle.Progress;
        for (var tick = 0; castle.Constructing; tick++)
        {
            Assert.IsTrue(tick < 2000, "the castle stands within 2000 passes");
            game.Tick();
            if (castle.Constructing)
            {
                largest = System.Math.Max(largest, castle.Progress - last);
                last = castle.Progress;
            }
        }

        return largest;
    }

    [TestMethod]
    public void TheViewTurnsGrowTheCastleEveryPass()
    {
        Assert.IsTrue(
            LargestStep(everyPassTurns: false) > 1024,
            "the slice turns alone grow the castle in large steps"
        );
        Assert.AreEqual(1024, LargestStep(everyPassTurns: true));
    }
}
