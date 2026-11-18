using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The thing readout of a building site placed by the command PlaceBuilding, on the made-up tables of the adapters'
// BuildingSiteTests (no install): the woodcutter, stonecutter, and forester are small and the sawmill is large, with the
// needs 2/0, 2/0, 2/0, and 3/2. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6.
[TestClass]
public sealed class BuildingSiteTests
{
    static readonly BuildingType[] Types =
    [
        BuildingType.Woodcutter,
        BuildingType.Stonecutter,
        BuildingType.Forester,
        BuildingType.Sawmill,
    ];

    static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var sizes = new byte[TestCatalog.BuildingSlots];
        var needs = new byte[2 * TestCatalog.BuildingSlots];
        foreach (var type in Types)
        {
            var code = TestCatalog.CodeOf(type);
            sizes[code] = (byte)(type == BuildingType.Sawmill ? 2 : 1);
            needs[2 * code] = (byte)(type == BuildingType.Sawmill ? 3 : 2);
            needs[2 * code + 1] = (byte)(type == BuildingType.Sawmill ? 2 : 0);
        }

        return new RuleTables(
            classes,
            MadeProgram.MadeStartStock(),
            jobData: MadeProgram.MadeJobData(),
            catalog: TestCatalog.StandIn(
                TestCatalog.Buildings(sizes: sizes, needs: needs, workers: MadeProgram.MadeWorkers())
            )
        );
    }

    static Game World()
    {
        var map = MapGenerator.Generate(
            MadeProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var game = GameFactory.Create(Made(), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();

        return game;
    }

    static void Place(Game game, BuildingType type, int x, int y)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = x,
                Y = y,
                Building = type,
            }
        );
        game.Tick();
    }

    // A node of the castle's land that the evaluator gives the site value and the node state 7 (nothing on or around its
    // south-east node), the first in row-major order.
    static (int X, int Y) Find(Game game, int site)
    {
        for (var y = 0; y < game.Map.Size.Height - 1; y++)
        {
            for (var x = 0; x < game.Map.Size.Width - 1; x++)
            {
                var result = BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, hasCastle: true);
                if (result.Node == 7 && result.Site == site)
                {
                    return (x, y);
                }
            }
        }

        Assert.Fail($"no node of the site value {site}");

        return default;
    }

    [TestMethod]
    [DataRow(2, "woodcutter-hut", "planks=2 stones=0")]
    [DataRow(4, "stonecutter-hut", "planks=2 stones=0")]
    [DataRow(9, "forester-hut", "planks=2 stones=0")]
    [DataRow(0x11, "sawmill", "planks=3 stones=2")]
    public void TheThingReadoutOfASiteNamesItsTypeAndShowsItsNeeds(int code, string name, string needs)
    {
        var game = World();
        var (x, y) = Find(game, 4);
        Place(game, TestCatalog.BuildingOf(code), x, y);

        var line = OpenSettlers.MapView.Thing.Of(game.Buildings[1], game.Tables.Catalog).Line();

        StringAssert.Contains(line, "constructing=true");
        StringAssert.Contains(line, needs);
        Assert.IsTrue(line.EndsWith(name), line);
    }
}
