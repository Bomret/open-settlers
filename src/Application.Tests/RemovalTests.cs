using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The class the original's check 0x44b6e6 gives a click of the remove tool (docs/rules/roads.md, Removal.ClickClass), which the
// removal's command query folds: the classes 0 and 1 both ask for no command, so the query alone does not tell a flag the click refuses
// from a node with nothing to remove. The bench's game on its flat map and the removal's made-up tables: the castle at 15,5 with its flag
// at 16,6, a flag at 20,6 that a road east from the castle's flag reaches, a flag at 17,9 with no road, and a woodcutter's hut at 13,8
// with its flag at 14,9. The emulator's classes of every node of the install's games are in the class of the same name in
// src/Adapters.Tests, through the command query.
[TestClass]
public sealed class RemovalTests
{
    [TestMethod]
    public void TheClickClassTellsAFlagTheClickRefusesFromANodeWithNothingToRemove()
    {
        var game = GameBench.Create(MadeProgram.RemovalTables(), GameBench.Flat());
        GameBench.Do(game, CommandKind.PlaceCastle, 15, 5);
        GameBench.Do(game, CommandKind.PlaceFlag, 20, 6);
        GameBench.Do(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        GameBench.Do(game, CommandKind.PlaceFlag, 17, 9);
        Assert.IsTrue(Huts.PlaceAndTick(game, 13, 8), "the hut stands");
        Assert.AreEqual(Owner.Player(0), game.Map.At(12, 4).Owner, "the empty node is the player's");

        Assert.AreEqual(0, game.Removal.ClickClass(12, 4), "a node of the player's that holds nothing");
        Assert.AreEqual(1, game.Removal.ClickClass(16, 6), "the castle's flag, with the castle at its north-west");
        Assert.AreEqual(1, game.Removal.ClickClass(20, 6), "a flag with one road");
        Assert.AreEqual(2, game.Removal.ClickClass(17, 9), "a flag with no road");
        Assert.AreEqual(3, game.Removal.ClickClass(13, 8), "a hut");
        Assert.AreEqual(4, game.Removal.ClickClass(18, 6), "a node of the road");
    }
}
