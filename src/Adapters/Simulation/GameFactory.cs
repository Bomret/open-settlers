namespace OpenSettlers.Simulation;

/// <summary>
/// Builds a <see cref="Game"/>: it makes every part and wires the graph by hand, the rules in the application layer and the
/// original's encodings (the digest, the residue of a settler's item) from the adapters.
/// </summary>
public static class GameFactory
{
    /// <summary>
    /// A game on <paramref name="map"/> with the rule tables of <paramref name="tables"/> (read from the install's program), at
    /// tick 0 with no command queued and no castle. The castle will hold the stock of <paramref name="stockLevel"/>
    /// (<see cref="StartStock.At"/>). The game changes the map it is given: placing the castle writes the castle, its flag,
    /// the heights, and the territory into the map. With <paramref name="everyPassTurns"/> each pass gives the settlers in
    /// motion one more turn (<see cref="MainPass.EveryPassTurns"/>). <paramref name="digest"/> is the game's digest port,
    /// <see cref="GameDigest"/> by default. With <paramref name="handsOutGoods"/> the pass of the last phase of each cycle runs the
    /// original's phase routine, which draws once from the generator and hands the castle's goods out to the requests of building
    /// sites (<see cref="StorageDistribution"/>); the scenarios that compare a game with an emulator verb that skips the phase routine
    /// leave it off.
    /// </summary>
    public static Game Create(
        RuleTables tables,
        Map map,
        int stockLevel = 0,
        bool everyPassTurns = false,
        IGameDigest? digest = null,
        bool handsOutGoods = false
    )
    {
        var clock = new GameClock();
        var castle = new CastleStock();
        var flags = new FlagTable();
        var network = new RoadNetwork();
        var buildings = new BuildingTable();
        var settlers = new SettlerTable();
        var behaviours = new ActivityRegistry();
        var draws = new GeneratorDraws(map.Generator);
        // The history reads the tick count of the game, which exists once the parts are wired.
        Game? game = null;
        var history = new GameHistory(new SnapshotExporter(settlers, buildings, flags), () => game!.TickCount);
        var movement = new SettlerMovement(map, buildings, tables, () => clock.Now, SettlerEncoder.ResidueOf);
        int Route(int from, int to) => network.Route(flags.Count, from, to);

        var goods = new GoodsTransport(movement, flags, network, history, buildings);
        var walkHome = new WalkHomeBehavior(movement, buildings, flags, network, history, draws, Route);
        var causes = new Causes(history, buildings, walkHome);
        var woodcutter = new WoodcutterBehavior(
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
            settlers
        );
        woodcutter.RegisterStart(Job.Stonecutter, woodcutter.StartStonecutter);
        woodcutter.RegisterStart(Job.Forester, woodcutter.StartForester);
        // The job rules modules (ADR 0016): the generated registry builds each from the shared parts.
        var modules = JobModules.Build(
            new JobParts(
                movement,
                buildings,
                flags,
                history,
                draws,
                (flag, place, good) => goods.LayGood(flag, place, good),
                woodcutter.EnterHut,
                woodcutter.StepOut,
                woodcutter.ArriveHome
            )
        );
        foreach (var module in modules)
        {
            woodcutter.RegisterModule(module);
            module.Register(behaviours);
        }

        roster.Register(behaviours);
        walks.Register(behaviours);
        removal.Register(behaviours);
        carriers.Register(behaviours);
        woodcutter.Register(behaviours);
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
            woodcutter,
            removal,
            roster,
            new SitePass(map, flags, tables, roster.AskForWorker),
            modules,
            handsOutGoods
                ? new StorageDistribution(tables, castle, flags, network, buildings, goods, draws, history)
                : null
        )
        {
            EveryPassTurns = everyPassTurns,
        };
        var placement = new BuildingPlacement(tables, map, castle, flags, buildings, history, causes, removal);
        var planner = new RoadPlanner(tables, map, castle);
        var startStock = StartStock.At(tables, stockLevel);
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
                Woodcutter = woodcutter,
                Placement = placement,
                RoadPlanner = planner,
                Queue = new CommandQueue(),
                Handlers =
                [
                    new PlaceCastleHandler(tables, map, castle, startStock, flags, settlers, roster, history, causes),
                    new PlaceFlagHandler(tables, map, castle, flags, history, causes, removal, buildings),
                    new BuildRoadHandler(planner, network, carriers, history, causes),
                    new PlaceBuildingHandler(placement),
                    new RemoveFlagHandler(removal),
                    new RemoveBuildingHandler(removal),
                    new RemoveRoadHandler(removal),
                ],
                Digest = digest ?? new GameDigest(),
            }
        );

        return game;
    }
}
