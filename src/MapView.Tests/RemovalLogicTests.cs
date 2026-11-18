using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The view of the game's removal logic (the logic itself is in the adapters' RemovalLogicTests), on made-up tables (no
// install): every object class is 0 except that of the flag, code 1, which is 3, and that of the hut, code 2, which is 4. The
// map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6 (slot 0); the flags at 18,6 (slot 1), 19,4 (slot 2),
// and 17,3 (slot 3) stand on it, with the roads 16,6 to 18,6, 18,6 to 19,4, and 17,3 to 19,4, and a hut stands north-west of the
// flag at 17,3 (16,2).
[TestClass]
public sealed class RemovalLogicTests
{
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
        for (var index = 0; index < map.Count; index++)
        {
            // No tree stands anywhere, so the worker waits in its hut.
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = GameFactory.Create(MadeProgram.RemovalTables(), map);
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
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.PlaceFlag,
                    X = x,
                    Y = y,
                }
            );
            game.Tick();
        }

        Build(game, 16, 6, 0, 0);
        Build(game, 18, 6, 5, 5, 0);
        Build(game, 17, 3, 0, 1);
        Huts.PlaceAndTick(game, 16, 2);

        return game;
    }

    static void Build(Game game, int x, int y, params int[] directions)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.BuildRoad,
                X = x,
                Y = y,
                Directions = directions,
            }
        );
        game.Tick();
    }

    static bool Applied(Game game, int kind, int x, int y)
    {
        var digest = game.Digest();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
            }
        );
        game.ApplyCommands();

        return digest != game.Digest();
    }

    // The view: the goods of the flags that were taken down are not drawn (their slots hold the bytes they had), and a hut that
    // burns is drawn until the burn frees its slot.
    [TestMethod]
    public void TheViewDrawsNoFreeFlagsGoodsAndAHutThatBurnsUntilItsSlotIsFree()
    {
        var game = World();
        foreach (var (x, y) in new[] { (13, 4), (13, 7), (14, 8) })
        {
            Assert.IsTrue(Applied(game, CommandKind.PlaceFlag, x, y));
            game.LayGood(game.Flags.Count - 1, 0, Good.Log);
        }

        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 4));
        Assert.IsTrue(Applied(game, CommandKind.RemoveFlag, 13, 7));

        var goods = OpenSettlers.MapView.ObjectDraws.FlagGoodsOf(game);
        CollectionAssert.AreEqual(new[] { (14, 8) }, goods.Keys.ToArray());

        int Huts() =>
            OpenSettlers
                .MapView.ObjectDraws.Of(game.Map, game.CastleStock.Castle, game.Buildings, clock: game.Clock.Now)
                .OfType<OpenSettlers.Import.BuildingDraw>()
                .Count(draw => draw.Type == BuildingType.Woodcutter);
        Assert.AreEqual(1, Huts());
        Assert.IsTrue(Applied(game, CommandKind.RemoveBuilding, 16, 2));
        Assert.AreEqual(1, Huts(), "the hut burns");
        for (var tick = 0; tick < 2000 && game.Buildings.Any(building => building.IsDemolishing); tick++)
        {
            game.Tick();
        }

        Assert.IsFalse(game.Buildings.Any(building => building.IsDemolishing), "the burn freed the slot");
        Assert.AreEqual(0, Huts());
    }
}
