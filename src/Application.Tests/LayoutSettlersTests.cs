using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The wording of the walk behavior's refusal for a worker whose hut is not standing, and the refusal of a worker whose hut's door
// or flag holds a settler. The layout's settler tests are in the class of the same name in src/Adapters.Tests.
[TestClass]
public sealed class LayoutSettlersTests
{
    // A layout's finished woodcutter's hut on a node with a settler on its door or its flag: the bench's game on the made-up tables of
    // LayoutGames, its flat map with the castle at 10,10 (its flag at 11,11), a flag at 14,11, and the road between them; the hut at
    // 13,10 ties to the flag at 14,11. The setup stands the hut and its worker as the layout's finished-building entry does.
    [TestMethod]
    [DataRow(13, 10, DisplayName = "the door")]
    [DataRow(14, 11, DisplayName = "the flag")]
    public void AWoodcutterEntryForAHutWithASettlerOnItsDoorOrFlagFailsTheLoad(int x, int y)
    {
        var game = GameBench.Create(MadeProgram.MadeTables(), GameBench.Flat());
        GameBench.Do(game, CommandKind.PlaceCastle, 10, 10);
        GameBench.Do(game, CommandKind.PlaceFlag, 14, 11);
        GameBench.Do(game, CommandKind.BuildRoad, 11, 11, [0, 0, 0]);
        game.Movement.Occupy(game.Map.Index(x, y), 1);

        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Woodcutter, 13, 10));
        var problem = game.StandWorkerInFinishedHut(BuildingType.Woodcutter, 13, 10);

        Assert.AreEqual("a settler stands on the hut's door or on its flag", problem);
    }

    sealed class StandingRules<TJob>(bool stands) : JobRules<TJob>(null!)
        where TJob : Job
    {
        public override bool CanStandInHut => stands;
    }

    [TestMethod]
    public void TheRefusalReadsRightForAnyNumberOfJobsThatStandInTheirHut()
    {
        var woodcutter = new StandingRules<Woodcutter>(true);
        var forester = new StandingRules<Forester>(true);
        var stonecutter = new StandingRules<Stonecutter>(true);
        var carrier = new StandingRules<Carrier>(false);

        CollectionAssert.AreEqual(
            new[]
            {
                "no finished building stands on the node",
                "no finished woodcutter's building stands on the node",
                "no finished woodcutter's or forester's building stands on the node",
                "no finished woodcutter's, stonecutter's, or forester's building stands on the node",
            },
            new[]
            {
                WalkBehavior.NoHutRefusal([carrier]),
                WalkBehavior.NoHutRefusal([carrier, woodcutter]),
                WalkBehavior.NoHutRefusal([forester, carrier, woodcutter]),
                WalkBehavior.NoHutRefusal([forester, stonecutter, woodcutter]),
            }
        );
    }
}
