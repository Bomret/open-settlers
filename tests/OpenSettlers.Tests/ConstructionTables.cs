using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tables of made-up values for the rules of the materials that reach a building site (docs/rules/construction-materials.md): the classes,
// the request words, the order a storage serves in, and the rank the planks start from are chosen so that they differ from the program's
// own and from one another. The program's own values are in ConstructionMaterialsTests and ProgramTablesTests.
static class ConstructionTables
{
    // The made-up classes: the plank asks class 3 of the first word, the stone class 6 of the second, the log class 2 of the second;
    // every other good goes to storage at once.
    public static readonly GoodClass PlankClass = new() { Class = 3, Word = RequestWord.First };
    public static readonly GoodClass StoneClass = new() { Class = 6, Word = RequestWord.Second };
    public static readonly GoodClass LogClass = new() { Class = 2, Word = RequestWord.Second };

    // The made-up grain class: class 1 of the first word, which the mill's flag asks it in.
    public static readonly GoodClass GrainClass = new() { Class = 1, Word = RequestWord.First };

    public static GoodClass?[] Classes()
    {
        var table = new GoodClass?[RuleTables.GoodClassCount];
        table[Good.Plank.Code] = PlankClass;
        table[Good.Stone.Code] = StoneClass;
        table[Good.Log.Code] = LogClass;
        table[Good.Grain.Code] = GrainClass;

        return table;
    }

    // The made-up order: planks first, then stones (the program's tables hold twelve steps; a storage that holds nothing else
    // serves these two alike).
    public static DistributionStep[] PlanksFirst() =>
        [new() { Class = PlankClass, Goods = [Good.Plank] }, new() { Class = StoneClass, Goods = [Good.Stone] }];

    public static DistributionStep[] StonesFirst() =>
        [new() { Class = StoneClass, Goods = [Good.Stone] }, new() { Class = PlankClass, Goods = [Good.Plank] }];

    // Every draw picks the first table below 0 and 1, so the draw 2 picks PlanksFirst, the draw 0 StonesFirst.
    public static DistributionStep[][] Orders() => [PlanksFirst(), StonesFirst(), PlanksFirst()];

    // The made-up order of a castle that hands out grain too: the order of PlanksFirst with the grain step last.
    public static DistributionStep[][] GrainOrders() =>
        [.. Orders().Select(order => order.Append(new() { Class = GrainClass, Goods = [Good.Grain] }).ToArray())];

    // The made-up rank the mill's request for grain starts from, which differs from the program's own (0x7f).
    public const byte MillRankBase = 0x3f;

    // The sizes of the four types of M4: the sawmill (0x11) is large, the others small.
    public static byte[] Sizes()
    {
        var sizes = Enumerable.Repeat((byte)1, RuleTables.BuildingTypeCount).ToArray();
        sizes[BuildingType.Sawmill.Code] = 2;
        sizes[BuildingType.PigFarm.Code] = 2;

        return sizes;
    }

    // The made-up rank the pig farm's request for grain starts from. A request whose rank has bit 0 clear falls to 0 when it serves a good
    // (docs/rules/goods.md), so the made-up base keeps bit 0 set through every shift below 8, as the program's 0xff does.
    public const byte PigRankBase = 0xff;

    // The made-up growth table of the pigs a pig farm holds: no pig grows where none is held, and each pause of the work makes one more
    // where the draw is below 0x8000, which the first word of a draw is half of the time.
    public static PigFarmRates PigRates(ushort draw = 0x8000) =>
        new() { RankBase = PigRankBase, Growth = [0, draw, draw, draw, draw, draw, draw, draw] };

    // The made-up job data of a game with a pig farm: the table above for the pig farmer's job.
    public static (Job Job, object Data)[] PigData(ushort draw = 0x8000) => [(new PigFarmer(), PigRates(draw))];

    // The made-up planks the forester's site needs.
    public const int ForesterPlanks = 2;

    // The made-up needs, a planks byte and a stones byte for each type: the forester two planks and no stone, every other type none.
    public static byte[] Needs()
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * BuildingType.Forester.Code] = ForesterPlanks;

        return needs;
    }

    // The made-up tool goods: the forester's worker needs the good 17 from the stock, the builder the good 18.
    public static readonly Good ForesterTool = new() { Code = 17 };
    public static readonly Good BuilderTool = new() { Code = 18 };

    // The made-up digger's tool: the good 19.
    public static readonly Good DiggerTool = new() { Code = 19 };

    // The made-up sawmiller's tool: the good 20.
    public static readonly Good SawTool = new() { Code = 20 };

    // The made-up workers: the woodcutter's hut asks for the woodcutter's job with no tool, the forester's for job 8 with a tool, the
    // sawmill for the sawmiller's job with a tool, the mill for the miller's job with no tool.
    public static WorkerRequest?[] Workers()
    {
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        workers[BuildingType.Woodcutter.Code] = new WorkerRequest { Job = new Woodcutter(), Tool = Good.None };
        workers[BuildingType.Forester.Code] = new WorkerRequest { Job = Job.Of(8), Tool = ForesterTool };
        workers[BuildingType.Sawmill.Code] = new WorkerRequest { Job = new Sawmiller(), Tool = SawTool };
        workers[BuildingType.Mill.Code] = new WorkerRequest { Job = new Miller(), Tool = Good.None };
        workers[BuildingType.PigFarm.Code] = new WorkerRequest { Job = new PigFarmer(), Tool = Good.None };

        return workers;
    }

    // The made-up order of materials: every type takes planks only, except that the sawmill takes a stone third.
    public static ushort[] MaterialOrder()
    {
        var order = new ushort[RuleTables.BuildingTypeCount];
        order[BuildingType.Sawmill.Code] = 0b100;

        return order;
    }

    // The made-up steps of a blow: 0x1000 for the woodcutter and the forester before the progress word's top bit and after it, so
    // that sixteen blows build them; the sawmill's are 0x800 and 0x556.
    public static ushort[] BlowSteps()
    {
        var steps = new ushort[2 * RuleTables.BuildingTypeCount];
        foreach (var type in new[] { BuildingType.Woodcutter, BuildingType.Forester })
        {
            steps[2 * type.Code] = 0x1000;
            steps[2 * type.Code + 1] = 0x1000;
        }

        steps[2 * BuildingType.Sawmill.Code] = 0x800;
        steps[2 * BuildingType.Sawmill.Code + 1] = 0x556;

        return steps;
    }

    public static RuleTables Tables(
        byte plankRankBase = 0xff,
        ushort stepTime = 100,
        byte[]? needs = null,
        ushort[]? materialOrder = null,
        byte[]? sizes = null,
        WorkerRequest?[]? workers = null,
        ushort[]? blowSteps = null,
        DistributionStep[][]? distribution = null,
        byte[]? goodKinds = null,
        IEnumerable<(Job Job, object Data)>? jobData = null
    ) =>
        LayoutSettlersTests.MadeTables(
            stepTime: stepTime,
            workers: workers ?? Workers(),
            builderRequest: new WorkerRequest { Job = new Builder(), Tool = BuilderTool },
            materialOrder: materialOrder ?? MaterialOrder(),
            blowSteps: blowSteps ?? BlowSteps(),
            buildingClass: 4,
            // A made-up transport priority for each good (the good's own number): a carrier takes a waiting good only where its
            // priority is above 0.
            transportPriorities: [.. Enumerable.Range(1, RuleTables.Goods).Select(good => (byte)good)],
            buildingSizes: sizes ?? Sizes(),
            buildingNeeds: needs ?? Needs(),
            goodKinds: goodKinds,
            goodClasses: Classes(),
            distribution: distribution ?? Orders(),
            plankRankBase: plankRankBase,
            millRankBase: MillRankBase,
            diggerRequest: new WorkerRequest { Job = new Digger(), Tool = DiggerTool },
            jobData: jobData
        );
}
