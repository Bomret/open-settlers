using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Simulation;

// The states NameTable names itself (Build); NameTableTests checks the list against them, the generator against the modules.
[assembly: CoreStateCodes(1, 2, 3, 4, 5, 6, 7, 0xf, 0x10, 0x19, 0x1c, 0x42, 0x4a)]

namespace OpenSettlers.Simulation;

/// <summary>
/// Open Settlers' names for the original's bare codes: map object codes, settler jobs, settler states, and building
/// types (ADR 0010, INV-0006). Each entry cites its evidence beside it. A code with no entry has no name and shows its
/// raw value. A name several codes share (<c>tree</c>) resolves to all of them through <see cref="CodesNamed"/>. The
/// object names read the class table the engine loaded (<see cref="RuleTables"/>): <c>stone</c> names a code the
/// docs call a stone only where that table says class 2. The job, state, and building names need no table and are
/// static. The names of the jobs, and of the states a job owns, come from the jobs' tables modules
/// (<see cref="JobTables{TJob}"/>); the states that belong to no one job are stated here.
/// </summary>
/// <remarks>The names of the object codes as the class table <paramref name="tables"/> reads them.</remarks>
/// <param name="tables">The rule tables whose class table the names are read from.</param>
public sealed class NameTable(RuleTables tables)
{
    readonly RuleTables _tables = tables;

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
    internal readonly record struct Entry
    {
        public required CodeKind Kind { get; init; }

        public required int Code { get; init; }

        public required string Name { get; init; }

        public required string Evidence { get; init; }
    }

    /// <summary>The evidence of a state whose name rests on the code of the Activity record alone.</summary>
    internal const string StateEvidence = "the code of the Activity record (src/Domain/Activity.cs)";

    const string TreeEvidence =
        "the program compares a node's code with 8 and 0x18 (0x438c11, 0x437f8f); docs/rules/woodcutting.md (codes 8 to 0x17); class 1";
    const string StoneEvidence =
        "docs/rules/map-object-codes.md (codes 0x48 to 0x4f are stone heaps); named only where class 2 of RuleTables.Classes agrees (docs/rules/build-sites.md)";

    /// <summary>Every entry of the table. The <c>stone</c> entries hold only where the class table says class 2.</summary>
    internal static IReadOnlyList<Entry> Entries { get; } = Build();

    static readonly Dictionary<(CodeKind, int), string> Names = Entries.ToDictionary(
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
        return name == "stone" && !_tables.ClassOf(new MapObject { Code = (byte)(code & 0x7f) }).IsStone ? null : name;
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

    static string? Find(CodeKind kind, int code) => Names.GetValueOrDefault((kind, code));

    static List<Entry> Build()
    {
        var entries = new List<Entry>();

        void Add(CodeKind kind, int first, int end, string name, string evidence)
        {
            for (var code = first; code < end; code++)
            {
                entries.Add(
                    new Entry
                    {
                        Kind = kind,
                        Code = code,
                        Name = name,
                        Evidence = evidence,
                    }
                );
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

        // The names of the jobs and of the states a job owns come from the tables modules (JobTablesRegistry); the states that
        // belong to no one job, the walks and the door among them, are here.
        foreach (var (code, name, evidence) in JobTablesRegistry.JobNames)
        {
            AddOne(CodeKind.Job, code, name, evidence);
        }

        foreach (var (code, name, evidence) in JobTablesRegistry.StateNames)
        {
            AddOne(CodeKind.State, code, name, evidence);
        }

        Add(
            CodeKind.Object,
            MapObject.SaplingCode,
            MapObject.SecondSaplingCode + 1,
            "sapling",
            "the planting 0x4389bf puts 0x67 or 0x68 on the node, and the object pass grows each into a tree of the codes 0x10 to 0x17 or 8 to 0xf (0x4290d5, 0x42910f); docs/rules/forestry.md"
        );
        AddOne(CodeKind.State, 1, "waits-in-castle", StateEvidence);
        AddOne(CodeKind.State, 2, "walks", StateEvidence);
        AddOne(CodeKind.State, 3, "walks-along-road", StateEvidence);
        AddOne(CodeKind.State, 4, "enters-door", StateEvidence);
        AddOne(CodeKind.State, 5, "waits-out-step", StateEvidence);
        AddOne(CodeKind.State, 6, "enters-building", StateEvidence);
        AddOne(CodeKind.State, 7, "steps-onto-flag", StateEvidence);
        AddOne(CodeKind.State, 0xf, "leaves-castle", StateEvidence);
        AddOne(CodeKind.State, 0x10, "walks-free", StateEvidence);
        AddOne(CodeKind.State, 0x19, "looks-for-flag-home", StateEvidence);
        AddOne(CodeKind.State, 0x1c, "claims-node-home", StateEvidence);
        AddOne(CodeKind.State, 0x42, "waits-at-flag", StateEvidence);
        AddOne(CodeKind.State, 0x4a, "leaves-site", StateEvidence);

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
            "BuildingType.Sawmill; handler 0x42da0b asks for job 6 (tool stock word 0x2c, good 22), whose work turns the logs it takes in into planks (good 8, 0x43c6aa); large, needs 3 planks and 2 stones (0x42d307, 0x449f18); docs/rules/build-sites.md, docs/rules/sawmilling.md"
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
