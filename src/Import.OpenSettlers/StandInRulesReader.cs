using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Import.ContentSet;

/// <summary>
/// Reads the rule files of a content set's folder into <see cref="StandInRules"/>: the four JSONC files named by
/// <see cref="OpenSettlersRuleTables.Files"/>. A broken file is an <see cref="InvalidDataException"/> of the form
/// <c>&lt;file&gt;: &lt;key&gt;: &lt;reason&gt;</c>.
/// </summary>
static class StandInRulesReader
{
    static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // The set's goods by stable name, in the order of their numbers 1 to 26; the graphics source keys a good's image by the name.
    internal static readonly string[] GoodNames =
    [
        "fish",
        "pig",
        "meat",
        "grain",
        "flour",
        "bread",
        "log",
        "plank",
        "boat",
        "stone",
        "iron-ore",
        "iron",
        "coal",
        "gold-ore",
        "gold",
        "shovel",
        "hammer",
        "fishing-rod",
        "butcher-knife",
        "scythe",
        "hatchet",
        "saw",
        "pickaxe",
        "pliers",
        "sword",
        "shield",
    ];

    static readonly IReadOnlyDictionary<string, int> ClassNames = new Dictionary<string, int>
    {
        ["plant"] = 1,
        ["stone"] = 2,
        ["flag"] = 3,
        ["small-building"] = 4,
        ["large-building"] = 5,
        ["castle"] = 6,
    };

    static readonly IReadOnlyDictionary<string, Direction> DirectionTerms = new[]
    {
        "east",
        "south-east",
        "south",
        "west",
        "north-west",
        "north",
    }
        .Select((name, at) => (name, direction: new Direction { Value = at }))
        .ToDictionary(entry => entry.name, entry => entry.direction);

    static readonly IReadOnlyDictionary<string, Good> GoodTerms = GoodNames.ToDictionary(
        name => name,
        name => new Good { Name = name }
    );

    static readonly IReadOnlyDictionary<string, FillCounter> CounterTerms = new Dictionary<string, FillCounter>
    {
        ["low"] = FillCounter.Low,
        ["high"] = FillCounter.High,
        ["none"] = FillCounter.None,
    };

    // A handover fills the low or the high byte of a building's fill: a good counted in no byte is no handover.
    static readonly IReadOnlyDictionary<string, FillCounter> HandoverTerms = new Dictionary<string, FillCounter>
    {
        ["low"] = FillCounter.Low,
        ["high"] = FillCounter.High,
    };

    static readonly IReadOnlyDictionary<string, bool> Resource1Terms = new Dictionary<string, bool>
    {
        ["counts"] = true,
        ["none"] = false,
    };

    static readonly IReadOnlyDictionary<string, RequestWord> WordTerms = new Dictionary<string, RequestWord>
    {
        ["first"] = RequestWord.First,
        ["second"] = RequestWord.Second,
    };

    static readonly IReadOnlyDictionary<string, BuildingSize> SizeTerms = new Dictionary<string, BuildingSize>
    {
        ["mine"] = BuildingSize.Mine,
        ["small"] = BuildingSize.Small,
        ["large"] = BuildingSize.Large,
    };

    static readonly IReadOnlyDictionary<string, bool> SecondFigureTerms = new Dictionary<string, bool>
    {
        ["second-figure"] = true,
        ["one-figure"] = false,
    };

    static readonly IReadOnlyDictionary<string, bool> MaterialTerms = new Dictionary<string, bool>
    {
        ["plank"] = false,
        ["stone"] = true,
    };

    // The jobs a worker request names, by the name their manifest keys carry.
    static IReadOnlyDictionary<string, Job> JobTerms { get; } =
        OpenSettlersSource.Jobs.ToDictionary(look => look.Key, look => look.Job);

    /// <summary>Reads the rule files in <paramref name="folder"/>.</summary>
    public static StandInRules Read(string folder)
    {
        var world = Open(folder, OpenSettlersRuleTables.WorldFile)
            .Fields(["objectClasses", "stepTimes", "scanWindows", "frameRows", "roadCapacities", "walkRows"]);
        var goods = Open(folder, OpenSettlersRuleTables.GoodsFile)
            .Fields(["goods", "distribution", "startStock", "ranks"]);
        var buildings = Open(folder, OpenSettlersRuleTables.BuildingsFile).Fields(["buildings"]);
        var jobs = Open(folder, OpenSettlersRuleTables.JobsFile).Fields(["names", "jobs"]);
        var ranks = goods["ranks"].Fields(["plank", "mill"]);
        var (buildingRules, castleDoor) = Buildings(buildings["buildings"]);
        var jobData = jobs["jobs"].Fields(["builder", "digger", "pig-farmer"]);
        var pig = jobData["pig-farmer"].Fields(["rankBase", "growth"]);

        return new StandInRules
        {
            ObjectClasses = ObjectClasses(world["objectClasses"]),
            StepTimeByRise = StepTimes(world["stepTimes"]),
            ScanWindows = ScanWindows(world["scanWindows"]),
            FrameRows = FrameRows(world["frameRows"]),
            RoadCapacities = [.. world["roadCapacities"].Items(8).Select(item => (byte)item.Int(1, 255))],
            WalkRows =
            [
                .. world["walkRows"]
                    .Items(RuleTables.WalkRowCount)
                    .Select(row =>
                        row.Items(6).Select(item => item.Term(DirectionTerms, "direction")).ToImmutableArray()
                    ),
            ],
            Goods = Goods(goods["goods"], goods["startStock"]),
            Distribution =
            [
                .. goods["distribution"].Items(3).Select(order => order.Items().Select(ReadStep).ToImmutableArray()),
            ],
            PlankRank = (byte)ranks["plank"].Int(1, 255),
            MillRank = (byte)ranks["mill"].Int(1, 255),
            Buildings = buildingRules,
            CastleDoor = castleDoor,
            BuilderTool = Tool(jobData["builder"].Fields(["tool"])["tool"]),
            DiggerTool = Tool(jobData["digger"].Fields(["tool"])["tool"]),
            Jobs = JobsNamed(jobs["names"]),
            PigRank = (byte)pig["rankBase"].Int(1, 255),
            PigGrowthPercent = [.. pig["growth"].Items(8).Select(item => item.Int(0, 100))],
        };
    }

    // The jobs the jobs file lists by stable name. The rules of every job of the engine hang on its type, so the set has each of them: a
    // name that is no job's, and a job the list leaves out, refuse the file.
    static ImmutableArray<Job> JobsNamed(RuleNode node)
    {
        var listed = node.Items()
            .Select(item =>
                Job.Named(item.Text())
                ?? throw item.Fault(
                    $"'{item.Text()}' is no stable name of a job, use {string.Join(", ", Job.Ported.Select(job => job.Name).Order(StringComparer.Ordinal))}"
                )
            )
            .ToImmutableArray();
        var missing = Job.Ported.Where(job => !listed.Contains(job)).Select(job => job.Name).ToList();
        if (missing.Count > 0)
        {
            throw node.Fault($"the list leaves out {string.Join(", ", missing)}, which the set has");
        }

        return listed;
    }

    // The building types the buildings file names, with the name each has there.
    static IEnumerable<(BuildingType Type, string Name)> Named() =>
        OpenSettlersSource.FinishedKeys.Select(finished => (finished.Type, finished.Type.Name));

    // The root of a rule file, which the walk steps file shares with the rule files.
    internal static RuleNode Open(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{file}: $: missing (looked in {folder})");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), Options);

            return new RuleNode(file, "", document.RootElement.Clone());
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{file}: line {e.LineNumber + 1}: not valid JSONC: {e.Message}", e);
        }
    }

    static ImmutableArray<ObjectClass> ObjectClasses(RuleNode node)
    {
        var classes = new int[RuleTables.ClassCount];
        var listed = new bool[RuleTables.ClassCount];
        foreach (var (name, listing) in node.Members())
        {
            if (!ClassNames.TryGetValue(name, out var value))
            {
                throw listing.Fault($"unknown object class; the known ones are {string.Join(", ", ClassNames.Keys)}");
            }

            foreach (var item in listing.Items())
            {
                var (first, last) = item.Range(0, RuleTables.ClassCount - 1);
                for (var code = first; code <= last; code++)
                {
                    classes[code] = listed[code] ? throw item.Fault($"object code {code} is listed twice") : value;
                    listed[code] = true;
                }
            }
        }

        return [.. classes.Select(value => new ObjectClass { Value = value })];
    }

    static ImmutableArray<ushort> StepTimes(RuleNode node)
    {
        var fields = node.Fields(Enumerable.Range(-4, StandInRules.Rises).Select(rise => rise.ToString()));

        return
        [
            .. Enumerable
                .Range(-4, StandInRules.Rises)
                .Select(rise => (ushort)fields[rise.ToString()].Int(1, ushort.MaxValue)),
        ];
    }

    static IReadOnlyDictionary<int, ushort> ScanWindows(RuleNode node)
    {
        var fields = node.Fields(MapSize.Offered.Select(Name));

        return MapSize.Offered.ToDictionary(
            size => size.Value,
            size => (ushort)fields[Name(size)].Int(1, ushort.MaxValue)
        );

        static string Name(MapSize size) => $"{size.Width}x{size.Height}";
    }

    static ImmutableArray<sbyte> FrameRows(RuleNode node)
    {
        var fields = node.Fields(["default", "frames"]);
        var rows = Enumerable
            .Repeat((sbyte)fields["default"].Int(sbyte.MinValue, sbyte.MaxValue), RuleTables.FrameRowCount)
            .ToArray();
        foreach (var (name, row) in fields["frames"].Members())
        {
            if (
                !int.TryParse(name, out var frame)
                || frame.ToString() != name
                || frame < 0
                || frame >= RuleTables.FrameRowCount
            )
            {
                throw row.Fault($"not a frame number, 0 to {RuleTables.FrameRowCount - 1}");
            }

            rows[frame] = (sbyte)row.Int(sbyte.MinValue, sbyte.MaxValue);
        }

        return [.. rows];
    }

    static ImmutableArray<StandInGood> Goods(RuleNode goods, RuleNode startStock)
    {
        var entries = goods.Fields(GoodNames);
        var stock = startStock.Fields(GoodNames);
        var byRank = new Dictionary<int, string>();
        var result = new List<StandInGood>();
        foreach (var name in GoodNames)
        {
            var fields = entries[name].Fields(["counts", "handover", "resource1", "priority", "request"]);
            var priority = fields["priority"].Int(1, GoodNames.Length);
            if (byRank.TryGetValue(priority, out var other))
            {
                throw fields["priority"]
                    .Fault($"rank {priority} is also the priority of {other}; each rank is given once");
            }

            byRank[priority] = name;
            var request = fields["request"];
            result.Add(
                new StandInGood
                {
                    Good = GoodTerms[name],
                    Counts = fields["counts"].Term(CounterTerms, "counting"),
                    Handover = fields["handover"].Term(HandoverTerms, "handover counter"),
                    CountsAgainstResource1 = fields["resource1"].Term(Resource1Terms, "resource 1 counting"),
                    Priority = priority,
                    Request = request.IsText ? NoRequest(request) : Request(request),
                    StartStock =
                    [
                        .. stock[name].Items(RuleTables.StartStockRows).Select(item => (byte)item.Int(0, 255)),
                    ],
                }
            );
        }

        return [.. result];
    }

    // A good that goes to storage: the one text a request may be.
    static GoodClass? NoRequest(RuleNode node) =>
        node.Text() == "storage" ? null : throw node.Fault("a request is \"storage\" or { \"class\", \"word\" }");

    static GoodClass Request(RuleNode node)
    {
        var fields = node.Fields(["class", "word"]);

        return new GoodClass
        {
            Class = fields["class"].Int(0, 7),
            Word = fields["word"].Term(WordTerms, "request word"),
        };
    }

    static DistributionStep ReadStep(RuleNode node)
    {
        var fields = node.Fields(["request", "goods"]);
        var goods = fields["goods"].Items();
        if (goods.Count == 0)
        {
            throw fields["goods"].Fault("a step names at least one good");
        }

        return new DistributionStep
        {
            Class = Request(fields["request"]),
            Goods = [.. goods.Select(item => item.Term(GoodTerms, "good"))],
        };
    }

    static (
        IReadOnlyDictionary<BuildingType, StandInBuilding> Rules,
        (byte Standing, byte Constructing) CastleDoor
    ) Buildings(RuleNode node)
    {
        var entries = node.Fields(Named().Select(entry => entry.Name));
        var castleDoor = Door(entries["castle"].Fields(["door"])["door"]);
        var result = new Dictionary<BuildingType, StandInBuilding>();
        foreach (var (type, name) in Named().Where(entry => entry.Type != BuildingType.Castle))
        {
            var fields = entries[name]
                .Fields(["size", "needs", "door", "materials", "blows", "secondFigure"], ["worker"]);
            var needs = fields["needs"].Fields(["planks", "stones"]);
            var blows = fields["blows"].Fields(["firstHalf", "secondHalf"]);
            var (standing, constructing) = Door(fields["door"]);
            result[type] = new StandInBuilding
            {
                Size = fields["size"].Term(SizeTerms, "size"),
                Needs = new BuildingNeeds
                {
                    Planks = needs["planks"].Int(0, 255),
                    Stones = needs["stones"].Int(0, 255),
                },
                StandingDoor = standing,
                ConstructingDoor = constructing,
                Stones = [.. fields["materials"].Items(max: 16).Select(item => item.Term(MaterialTerms, "material"))],
                FirstHalfBlows = blows["firstHalf"].Int(1, 0x8000),
                SecondHalfBlows = blows["secondHalf"].Int(1, 0x8000),
                Worker = fields.TryGetValue("worker", out var worker) ? Worker(worker) : null,
                SecondFigure = fields["secondFigure"].Term(SecondFigureTerms, "second figure"),
            };
        }

        return (result, castleDoor);
    }

    static (byte Standing, byte Constructing) Door(RuleNode node)
    {
        var fields = node.Fields(["standing", "constructing"]);

        return ((byte)fields["standing"].Int(0, 31), (byte)fields["constructing"].Int(0, 31));
    }

    static WorkerRequest Worker(RuleNode node)
    {
        var fields = node.Fields(["job", "tool"]);

        return new WorkerRequest { Job = fields["job"].Term(JobTerms, "job"), Tool = Tool(fields["tool"]) };
    }

    // A tool is a good, or "none" for a job that needs none.
    static Good Tool(RuleNode node) => node.IsText && node.Text() == "none" ? Good.None : node.Term(GoodTerms, "good");
}
