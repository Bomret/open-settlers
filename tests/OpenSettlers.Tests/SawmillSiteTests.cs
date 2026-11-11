using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A sawmill's site from its placement to its building, on made-up tables with the castle's hand-out on (docs/rules/construction-materials.md,
// docs/rules/digging.md, docs/rules/construction.md): the site at 14,8 stands on uneven ground, asks for a digger, asks for nothing before its
// ground is level, then asks for its builder, and the castle hands it 3 planks and 2 stones over the road to its flag at 15,9. The program's
// needs of the sawmill (3 planks, 2 stones) and the order of its materials (the third and fourth are stones, 0x000c) are made-up values here.
[TestClass]
public sealed class SawmillSiteTests
{
    private const int SiteX = 14;
    private const int SiteY = 8;
    private const int Planks = 3;
    private const int Stones = 2;

    // A plank on site or on the way counts against the planks the castle handed out; the third plank's rank is below the castle's floor
    // while no builder is inside (the start rank shifted right by 2 more: 0xff >> 2 >> 2 with bit 0 cleared is 0x0e, a rank below 15 at 2 held).
    private const int PlanksWithNoBuilder = 2;

    private static (Game Game, Building Site) World()
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * BuildingType.Sawmill.Code] = Planks;
        needs[2 * BuildingType.Sawmill.Code + 1] = Stones;
        var order = new ushort[RuleTables.BuildingTypeCount];
        order[BuildingType.Sawmill.Code] = 0x000c;
        var game = ConstructionWorld.Create(
            ConstructionTables.Tables(needs: needs, materialOrder: order),
            handsOutGoods: true
        );
        game.CastleStock.Add(ConstructionTables.DiggerTool);
        for (var count = 0; count < 5; count++)
        {
            game.CastleStock.Add(Good.Plank);
            game.CastleStock.Add(Good.Stone);
        }

        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        ConstructionWorld.Site(game, BuildingType.Sawmill, SiteX, SiteY);
        var site = game.Buildings[1];
        Assert.IsTrue(site.Constructing, "the sawmill's site stands");
        // The ground is uneven: the node lies two below the site's height, so the digger has work.
        game.Map.At(SiteX, SiteY).Height = site.Site!.TargetHeight - 2;

        return (game, site);
    }

    // The planks the site holds on site, has on the way, or has used.
    private static int PlanksHeld(Building site) =>
        (site.FillLow >> 4) + (site.FillLow & 0xf) + (Planks - site.Site!.Planks);

    [TestMethod]
    public void ASawmillSiteLevelledByTheDiggerAndWithItsBuilderInsideReceivesThreePlanksAndTwoStonesFromTheCastle()
    {
        var (game, site) = World();
        var planks = game.CastleStock.Planks;
        var stones = game.CastleStock.Stones;
        var builderInside = false;

        for (var tick = 0; tick < 60000 && site.Constructing; tick++)
        {
            game.Tick();
            if (!site.Constructing)
            {
                break;
            }

            if (site.Progress == 0)
            {
                Assert.AreEqual(planks, game.CastleStock.Planks, "a site not yet level asks for no plank");
                Assert.AreEqual(stones, game.CastleStock.Stones, "a site not yet level asks for no stone");
            }

            builderInside |= site.WorkerInside;
            if (!site.WorkerInside)
            {
                Assert.IsLessThanOrEqualTo(
                    PlanksWithNoBuilder,
                    PlanksHeld(site),
                    "with no builder inside the third plank's rank stays below what the castle hands out"
                );
            }
        }

        Assert.IsFalse(site.Constructing, "the sawmill is built");
        Assert.IsTrue(builderInside, "its builder was inside");
        Assert.AreEqual(planks - Planks, game.CastleStock.Planks, "the castle lost the 3 planks that went out");
        Assert.AreEqual(stones - Stones, game.CastleStock.Stones, "the castle lost the 2 stones that went out");
        Assert.IsNull(site.Site);
        Assert.IsNull(
            game.Flags[site.Flag].RequestOf(RequestWord.First).Asks,
            "the built sawmill's flag asks for no plank"
        );
        Assert.IsNull(game.Flags[site.Flag].RequestOf(RequestWord.Second).Asks, "nor for a stone");
    }
}
