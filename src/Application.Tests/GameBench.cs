using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A game wired from its parts by hand, as GameFactory in the adapters wires one, for the tests that read the game's internal parts.
// This pair reaches no adapter, so the original's encodings are stand-ins: a settler's snapshot holds its state code alone, the residue
// a settler keeps is the one its activity carries, the castle holds the start stock's row 0 as the table gives it, and the game has no
// digest. The maps are flat: no map generator runs here.
static class GameBench
{
    // The settler's state code (its activity's code), the one field of a snapshot the history reads.
    sealed class StateSnapshots(SettlerTable settlers) : IThingSnapshots
    {
        public ThingState Building(int slot) => new() { Fields = [] };

        public ThingState Flag(int slot) => new() { Fields = [] };

        public ThingState Settler(int slot) =>
            new() { Fields = slot < settlers.Count ? [("state", settlers[slot].Activity.Code)] : [] };

        public string GoodText(Good good) => good.Name;
    }

    sealed class GeneratorDraws(RandomGenerator start) : IRandomDraws
    {
        public RandomGenerator Generator { get; private set; } = start;

        public ushort Draw()
        {
            Generator = Generator.Step();

            return Generator.W0;
        }
    }

    sealed class NoDigest : IGameDigest
    {
        public ulong Of(Game game) => throw new NotSupportedException("the bench's game has no digest");
    }

    // A map of the first offered size whose every node is grass at height 1, as a layout's flat grass map is, with the generator a
    // layout of the seed 1234,5678,1234 starts from.
    public static Map Flat()
    {
        var map = new Map(MapSize.Offered[0])
        {
            Generator = new RandomGenerator
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x1234,
            }
                .Step()
                .Step(),
        };
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Height = 1;
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        return map;
    }

    // The game on the map with the tables, at tick 0 with no castle.
    public static Game Create(
        RuleTables tables,
        Map map,
        bool everyPassTurns = false,
        bool handsOutGoods = false,
        bool settlerSupply = false,
        int settlerRate = 0
    )
    {
        var clock = new GameClock();
        var castle = new CastleStock(tables.Catalog);
        var flags = new FlagTable();
        var network = new RoadNetwork();
        var buildings = new BuildingTable();
        var settlers = new SettlerTable { Capacity = SettlerTable.SizeOf(map.Size) };
        var behaviours = new ActivityRegistry();
        var draws = new GeneratorDraws(map.Generator);
        Game? game = null;
        var history = new GameHistory(new StateSnapshots(settlers), () => game!.TickCount);
        var supply = settlerSupply ? new SettlerSupply(settlerRate, clock, castle, settlers, history) : null;
        var movement = new SettlerMovement(map, buildings, tables, () => clock.Now, activity => activity.Residue);
        int Route(int from, int to) => network.Route(flags.Count, from, to);

        var goods = new GoodsTransport(movement, flags, network, history, buildings);
        var walkHome = new WalkHomeBehavior(movement, buildings, flags, network, history, draws, Route);
        var causes = new Causes(history, buildings, walkHome);
        var hut = new HutWork(
            movement,
            settlers,
            flags,
            history,
            draws,
            walkHome,
            (flag, place, good) => goods.LayGood(flag, place, good),
            () => causes.InLayout
        );
        var roster = new CastleRoster(
            movement,
            settlers,
            buildings,
            flags,
            network,
            history,
            causes,
            draws,
            clock,
            castle,
            tables
        );
        var roadStep = new RoadStep(movement, settlers, history, causes);
        var carriers = new CarrierBehavior(
            movement,
            settlers,
            buildings,
            flags,
            network,
            history,
            goods,
            roster,
            roadStep.Step,
            castle.Add,
            () => causes.InLayout
        );
        var removal = new Removal(
            movement,
            settlers,
            buildings,
            flags,
            network,
            history,
            carriers,
            goods,
            walkHome.Begin,
            () => castle.Castle is not null,
            id => causes.ForgetPlacement(id)
        );
        var modules = JobModules.Build(
            new JobParts
            {
                Movement = movement,
                Buildings = buildings,
                Flags = flags,
                History = history,
                Draws = draws,
                LayGood = (flag, place, good) => goods.LayGood(flag, place, good),
                Hut = hut,
            }
        );
        foreach (var module in modules)
        {
            hut.RegisterModule(module);
            module.Register(behaviours);
        }

        var walks = new WalkBehavior(
            movement,
            buildings,
            flags,
            network,
            history,
            causes,
            clock,
            walkHome,
            carriers,
            roadStep,
            roster,
            settlers,
            modules
        );
        roster.Register(behaviours);
        walks.Register(behaviours);
        removal.Register(behaviours);
        carriers.Register(behaviours);
        hut.Register(behaviours);
        walkHome.Register(behaviours);
        var pass = new MainPass(
            clock,
            tables,
            map,
            settlers,
            buildings,
            history,
            causes,
            new TablePass(settlers, buildings, behaviours),
            carriers,
            hut,
            removal,
            roster,
            new SitePass(map, flags, tables, modules, roster.AskForWorker),
            modules,
            handsOutGoods
                ? new StorageDistribution(tables, castle, flags, network, buildings, goods, draws, history)
                : null,
            supply
        )
        {
            EveryPassTurns = everyPassTurns,
        };
        var placement = new BuildingPlacement(tables, map, castle, flags, buildings, history, causes, removal);
        var planner = new RoadPlanner(tables, map, castle);
        // simplicity: the castle's stock is the row 0 of the tables as it stands; StartStock in the adapters also takes away what the
        // castle's construction and first settlers use. A test that needs a stock names it in its castle command (Command.Stock).
        var startStock = tables.StartStock[0].ToDictionary(entry => entry.Key, entry => (int)entry.Value);
        game = new Game(
            new GameParts
            {
                Tables = tables,
                Map = map,
                CastleStock = castle,
                Flags = flags,
                Network = network,
                Buildings = buildings,
                Settlers = settlers,
                History = history,
                Causes = causes,
                Clock = clock,
                Draws = draws,
                Movement = movement,
                Behaviours = behaviours,
                Pass = pass,
                Roster = roster,
                Carriers = carriers,
                Goods = goods,
                Removal = removal,
                WalkHome = walkHome,
                Walks = walks,
                Hut = hut,
                Placement = placement,
                RoadPlanner = planner,
                Queue = new CommandQueue(tables.Catalog),
                Handlers =
                [
                    new PlaceCastleHandler(
                        tables,
                        map,
                        castle,
                        startStock,
                        flags,
                        settlers,
                        roster,
                        history,
                        causes,
                        supply
                    ),
                    new PlaceFlagHandler(tables, map, castle, flags, history, causes, removal, buildings),
                    new BuildRoadHandler(planner, network, carriers, history, causes),
                    new PlaceBuildingHandler(placement),
                    new RemoveFlagHandler(removal),
                    new RemoveBuildingHandler(removal),
                    new RemoveRoadHandler(removal),
                ],
                Supply = supply,
                Digest = new NoDigest(),
            }
        );

        return game;
    }

    // Applies a command of player 0 at the game's tick and ticks once.
    public static void Do(
        Game game,
        int kind,
        int x,
        int y,
        int[]? directions = null,
        IReadOnlyList<Job>? settlers = null
    )
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
                Settlers = settlers,
            }
        );
        game.Tick();
    }

    // Applies a command of player 0 at once, with no tick, as a layout's entry does (Layout.Apply), and tells whether the game
    // applied it.
    public static bool Apply(Game game, int kind, int x, int y, int[]? directions = null)
    {
        var revision = game.Revision;
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
            }
        );
        game.ApplyCommands();

        return game.Revision > revision;
    }

    public static void Run(Game game, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            game.Tick();
        }
    }
}
