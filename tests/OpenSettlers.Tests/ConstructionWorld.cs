using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A game on made-up tables for the rules of construction that need no install: the map of the seed 1234,5678,9ABC with its castle at
// 15,5 and its flag at 16,6, no tree anywhere, the flags 18,6, 19,4, and 17,3 (slots 1 to 3) with roads from the castle's flag east
// to 18,6 and from there to 19,4, as HutTests lays them. A building at 17,5 has its flag at 18,6, which a road joins to the castle.
internal static class ConstructionWorld
{
    public static Game Create(RuleTables? tables = null, bool handsOutGoods = false, bool everyPassTurns = false)
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = GameFactory.Create(
            tables ?? ConstructionTables.Tables(),
            map,
            handsOutGoods: handsOutGoods,
            everyPassTurns: everyPassTurns
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            Do(game, CommandKind.PlaceFlag, x, y);
        }

        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);

        return game;
    }

    public static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    public static void Site(Game game, BuildingType type, int x, int y)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, x, y, Building: type));
        game.Tick();
    }

    // The longest run of passes, over the ticks, in which a settler the view draws (Activity.IsDrawn) had no time left of its step:
    // its figure stands still until its next turn. Also the activity types such a settler was seen in.
    public static (int Longest, HashSet<System.Type> Seen) LongestStall(Game game, int ticks)
    {
        var stalls = new Dictionary<int, int>();
        var seen = new HashSet<System.Type>();
        var longest = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            game.Tick();
            for (var slot = 0; slot < game.Settlers.Count; slot++)
            {
                var settler = game.Settlers[slot];
                var drawn = settler.Activity.IsDrawn;
                var ended = (ushort)(game.Clock.Now - settler.Stamp) >= settler.Timer;
                stalls[slot] = drawn && ended ? stalls.GetValueOrDefault(slot) + 1 : 0;
                longest = System.Math.Max(longest, stalls[slot]);
                if (drawn)
                {
                    seen.Add(settler.Activity.GetType());
                }
            }
        }

        return (longest, seen);
    }

    public static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    // The settlers of a job, in the order of their slots.
    public static Settler[] OfJob(Game game, Job job) =>
        [.. game.Settlers.Skip(1).Where(settler => settler.Job == job)];
}
