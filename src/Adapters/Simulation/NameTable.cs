using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Open Settlers' names for the original's bare codes: map object codes, settler jobs, settler states, and building
/// types (ADR 0010, INV-0006). Each entry cites its evidence beside it. A code with no entry has no name and shows its
/// raw value. A name several codes share (<c>tree</c>) resolves to all of them through <see cref="CodesNamed"/>. The
/// object names read the class table the engine loaded (<see cref="RuleTables"/>): <c>stone</c> names a code the
/// docs call a stone only where that table says class 2. The job, state, and building names need no table and are
/// static.
/// </summary>
public sealed class NameTable
{
    private readonly RuleTables tables;

    /// <summary>The names of the object codes as the class table <paramref name="tables"/> reads them.</summary>
    public NameTable(RuleTables tables) => this.tables = tables;

    // The object codes the engine and docs/rules/map-object-codes.md name; Game.Woodcutting reads the ranges from here.
    internal const int FirstTree = MapObject.FirstTreeCode;
    internal const int EndTree = MapObject.EndTreeCode;
    internal const int FirstStone = 0x48;
    internal const int EndStone = 0x50;
    internal const int Stump = MapObject.StumpCode;
    internal const int FirstCutObject = MapObject.FirstFallingCode;
    internal const int EndCutObject = MapObject.EndFallingCode;

    /// <summary>What a code stands for.</summary>
    internal enum CodeKind
    {
        Object,
        Job,
        State,
        Building,
    }

    /// <summary>One entry: the code, its name, and the evidence for the name.</summary>
    internal readonly record struct Entry(CodeKind Kind, int Code, string Name, string Evidence);

    private const string TreeEvidence =
        "the program compares a node's code with 8 and 0x18 (0x438c11, 0x437f8f); docs/rules/woodcutting.md (codes 8 to 0x17); class 1";
    private const string StoneEvidence =
        "docs/rules/map-object-codes.md (codes 0x48 to 0x4f are stone heaps); named only where class 2 of RuleTables.Classes agrees (docs/rules/build-sites.md)";

    /// <summary>Every entry of the table. The <c>stone</c> entries hold only where the class table says class 2.</summary>
    internal static IReadOnlyList<Entry> Entries { get; } = Build();

    private static readonly Dictionary<(CodeKind, int), string> Names = Entries.ToDictionary(
        entry => (entry.Kind, entry.Code),
        entry => entry.Name
    );

    /// <summary>
    /// The name of the object code, or null where the table has none. The code's high bit is ignored, as the class
    /// lookup ignores it. A stone code the class table does not call class 2 has no name.
    /// </summary>
    public string? ObjectName(byte code)
    {
        var name = Find(CodeKind.Object, code & 0x7f);
        return name == "stone" && !tables.ClassOf(new MapObject((byte)(code & 0x7f))).IsStone ? null : name;
    }

    /// <summary>The name of the settler job, or null where the table has none.</summary>
    public static string? JobName(int job) => Find(CodeKind.Job, job);

    /// <summary>The name of the settler state, or null where the table has none.</summary>
    public static string? StateName(int state) => Find(CodeKind.State, state);

    /// <summary>The name of the building type, or null where the table has none.</summary>
    public static string? BuildingName(int type) => Find(CodeKind.Building, type);

    /// <summary>The object codes the name stands for, in ascending order; empty for an unknown name.</summary>
    public IEnumerable<int> CodesNamed(string name) =>
        Entries
            .Where(entry => entry.Kind == CodeKind.Object && entry.Name == name)
            .Select(entry => entry.Code)
            .Where(code => ObjectName((byte)code) == name);

    /// <summary>Whether the table names an object, job, state, or building type <paramref name="name"/>.</summary>
    public static bool IsName(string name) => Entries.Any(entry => entry.Name == name);

    /// <summary>Whether the table names an object code <paramref name="name"/>.</summary>
    public static bool IsObjectName(string name) =>
        Entries.Any(entry => entry.Kind == CodeKind.Object && entry.Name == name);

    private static string? Find(CodeKind kind, int code) => Names.GetValueOrDefault((kind, code));

    private static List<Entry> Build()
    {
        var entries = new List<Entry>();

        void Add(CodeKind kind, int first, int end, string name, string evidence)
        {
            for (var code = first; code < end; code++)
            {
                entries.Add(new Entry(kind, code, name, evidence));
            }
        }

        void AddOne(CodeKind kind, int code, string name, string evidence) => Add(kind, code, code + 1, name, evidence);

        const string Map = "docs/rules/map-object-codes.md";
        AddOne(CodeKind.Object, 1, "flag", "MapObject.Flag; class 3; docs/rules/flags.md");
        Add(
            CodeKind.Object,
            2,
            5,
            "building",
            "class 4 to 6; docs/rules/build-sites.md names the codes 2 to 4 buildings"
        );
        Add(CodeKind.Object, FirstTree, EndTree, "tree", TreeEvidence);
        Add(CodeKind.Object, EndTree, 0x1c, "palm", $"{Map}: sprite; class 1; terrain 10 only");
        Add(CodeKind.Object, FirstStone, EndStone, "stone", StoneEvidence);
        Add(CodeKind.Object, EndStone, 0x52, "rock", $"{Map}: class 2; sprite");
        AddOne(CodeKind.Object, 0x52, "cross", $"{Map}: sprite; MapGen_MarkPeaks puts it on peaks");
        AddOne(
            CodeKind.Object,
            Stump,
            "stump",
            "the object pass lets 0x53 vanish by chance (0x428eb8); docs/rules/woodcutting.md"
        );
        Add(CodeKind.Object, 0x5a, 0x5c, "cactus", $"{Map}: class 1; sprite; terrain 8 to 10");
        AddOne(CodeKind.Object, 0x5c, "dead-tree", $"{Map}: class 1; sprite; docs/rules/woodcutting.md");
        Add(
            CodeKind.Object,
            FirstCutObject,
            EndCutObject,
            "felled-tree",
            "docs/rules/woodcutting.md (the cut writes 0x5d to 0x66, the object pass turns each into 0x53, 0x428eb8)"
        );

        AddOne(CodeKind.Job, Job.Carrier.Code, "carrier", "Job.Carrier; docs/rules/carriers.md");
        AddOne(
            CodeKind.Job,
            Job.Builder.Code,
            "builder",
            "Job.Builder; the under-construction handler 0x42d55f asks for job 3 with the tool good 17 (request at 0x42d5fc) and the job's state 9 builds the site (0x43ec82); docs/rules/construction.md"
        );
        AddOne(
            CodeKind.Job,
            Job.CastleSettler.Code,
            "castle-settler",
            "Job.CastleSettler; the castle's first settler stands by while the castle is built (state 10, 0x43f17a) and no building asks for job 4; Game.Removal reads job 4 as a settler that goes home as a carrier; docs/rules/settlers.md"
        );
        AddOne(CodeKind.Job, Job.Woodcutter.Code, "woodcutter", "Job.Woodcutter; docs/rules/woodcutting.md");
        Add(
            CodeKind.Object,
            MapObject.SaplingCode,
            MapObject.SecondSaplingCode + 1,
            "sapling",
            "the planting 0x4389bf puts 0x67 or 0x68 on the node, and the object pass grows each into a tree of the codes 0x10 to 0x17 or 8 to 0xf (0x4290d5, 0x42910f); docs/rules/forestry.md"
        );
        AddOne(
            CodeKind.Job,
            Job.Forester.Code,
            "forester",
            "Job.Forester; the type 9 handler 0x42d9c9 asks for job 8 with no tool, and his work plants the saplings 0x67 and 0x68 (0x4389bf); docs/rules/forestry.md"
        );
        AddOne(
            CodeKind.Job,
            Job.Stonecutter.Code,
            "stonecutter",
            "Job.Stonecutter; his work puts good 10 (a stone) on the hut's flag (0x438182) and cuts the stone heaps 0x48 to 0x4f (0x435c5c); docs/rules/stonecutting.md"
        );
        AddOne(CodeKind.Job, Job.Jobless.Code, "jobless", "Job.Jobless: 21 is a settler without a job yet");

        const string State = "the code of the Activity record (src/Domain/Activity.cs)";
        AddOne(CodeKind.State, 1, "waits-in-castle", State);
        AddOne(CodeKind.State, 2, "walks", State);
        AddOne(CodeKind.State, 3, "walks-along-road", State);
        AddOne(CodeKind.State, 4, "enters-door", State);
        AddOne(CodeKind.State, 5, "waits-out-step", State);
        AddOne(CodeKind.State, 6, "enters-building", State);
        AddOne(CodeKind.State, 7, "steps-onto-flag", State);
        AddOne(CodeKind.State, 9, "builds", State);
        AddOne(CodeKind.State, 0xf, "leaves-castle", State);
        AddOne(CodeKind.State, 0x10, "walks-free", State);
        AddOne(CodeKind.State, 0x11, "cuts-tree", State);
        AddOne(CodeKind.State, 0x12, "looks-for-tree", State);
        AddOne(
            CodeKind.State,
            0x13,
            "looks-for-spot",
            "0x438c9f draws nodes of the spiral until a free node of lowland terrain comes up; docs/rules/forestry.md"
        );
        AddOne(
            CodeKind.State,
            0x14,
            "plants",
            "0x4389bf waits on the node and puts the sapling down; docs/rules/forestry.md"
        );
        AddOne(
            CodeKind.State,
            0x15,
            "looks-for-heap",
            "0x435b17 draws nodes of the spiral until a stone heap stands beside one; docs/rules/stonecutting.md"
        );
        AddOne(
            CodeKind.State,
            0x16,
            "walks-to-heap",
            "0x436a84 walks the vector out to the heap and stops where one stands north-west; docs/rules/stonecutting.md"
        );
        AddOne(
            CodeKind.State,
            0x17,
            "cuts-stone",
            "0x435c5c steps onto the heap's node, strikes, and takes a stone off it; docs/rules/stonecutting.md"
        );
        AddOne(CodeKind.State, 0x19, "looks-for-flag-home", State);
        AddOne(CodeKind.State, 0x1c, "claims-node-home", State);
        AddOne(CodeKind.State, 0x42, "waits-at-flag", State);
        AddOne(CodeKind.State, 0x4a, "leaves-site", State);

        AddOne(CodeKind.Building, BuildingType.Castle.Code, "castle", "BuildingType.Castle");
        AddOne(CodeKind.Building, BuildingType.Woodcutter.Code, "woodcutter-hut", "BuildingType.Woodcutter");
        AddOne(
            CodeKind.Building,
            BuildingType.Stonecutter.Code,
            "stonecutter-hut",
            "BuildingType.Stonecutter; handler 0x42d987 asks for job 7 (tool stock word 0x2e, good 23), whose work puts the stone (good 10) on the flag (0x438182); size and needs 0x42d307, 0x449f18; docs/rules/build-sites.md, docs/rules/stonecutting.md"
        );
        AddOne(
            CodeKind.Building,
            BuildingType.Forester.Code,
            "forester-hut",
            "BuildingType.Forester; handler 0x42d9c9 asks for job 8 (no tool), whose work plants the saplings 0x67 and 0x68 (0x4389bf); size and needs 0x42d307, 0x449f18; docs/rules/build-sites.md, docs/rules/forestry.md"
        );
        AddOne(
            CodeKind.Building,
            BuildingType.Sawmill.Code,
            "sawmill",
            "BuildingType.Sawmill; handler 0x42da0b asks for job 6 (tool stock word 0x2c, good 22); large, needs 3 planks and 2 stones (0x42d307, 0x449f18); inferred, docs/rules/build-sites.md"
        );
        AddOne(
            CodeKind.Building,
            BuildingType.Storehouse.Code,
            "storehouse",
            "BuildingType.Storehouse; Game.Removal storage types: the castle and type 0xa"
        );

        return entries;
    }
}
