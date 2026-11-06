using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Open Settlers' names for the original's bare codes: map object codes, settler jobs, settler states, and building
/// types (ADR 0010, INV-0006). Each entry cites its evidence beside it. A code with no entry has no name and shows its
/// raw value. A name several codes share (<c>tree</c>) resolves to all of them through <see cref="CodesNamed"/>. The
/// object names read the class table the engine loaded (<see cref="ProgramTables"/>): <c>stone</c> names a code the
/// docs call a stone only where that table says class 2. The job, state, and building names need no table and are
/// static.
/// </summary>
public sealed class NameTable
{
    private readonly ProgramTables tables;

    /// <summary>The names of the object codes as the class table <paramref name="tables"/> reads them.</summary>
    public NameTable(ProgramTables tables) => this.tables = tables;

    // The object codes the engine and docs/rules/map-object-codes.md name; Game.Woodcutting reads the ranges from here.
    internal const int FirstTree = 8;
    internal const int EndTree = 0x18;
    internal const int FirstStone = 0x48;
    internal const int EndStone = 0x50;
    internal const int Stump = 0x53;
    internal const int FirstCutObject = 0x5d;
    internal const int EndCutObject = 0x67;

    // The settler jobs and the building type the engine names; Game reads them from here.
    internal const int BuilderJob = 4;
    internal const int JoblessJob = 21;
    internal const int StorehouseType = 0xa;

    /// <summary>The class of the stones in the original's class table (<c>docs/rules/build-sites.md</c>).</summary>
    private const int StoneClass = 2;

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
        "docs/rules/map-object-codes.md (codes 0x48 to 0x4f are stone heaps); named only where class 2 of ProgramTables.Classes agrees (docs/rules/build-sites.md)";

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
        return name == "stone" && tables.Classes[code & 0x7f] != StoneClass ? null : name;
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
        AddOne(CodeKind.Object, 1, "flag", "FlagPlacement.FlagCode; class 3; docs/rules/flags.md");
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

        AddOne(CodeKind.Job, Settler.CarrierJob, "carrier", "Settler.CarrierJob; docs/rules/carriers.md");
        AddOne(CodeKind.Job, BuilderJob, "builder", "Game.Removal reads job 4 as the builder; docs/rules/settlers.md");
        AddOne(CodeKind.Job, Settler.WoodcutterJob, "woodcutter", "Settler.WoodcutterJob; docs/rules/woodcutting.md");
        AddOne(CodeKind.Job, JoblessJob, "jobless", "Settler.Job doc: 21 is a settler without a job yet");

        const string State = "Settler.State doc";
        AddOne(CodeKind.State, 1, "waits-in-castle", State);
        AddOne(CodeKind.State, 2, "walks", State);
        AddOne(CodeKind.State, 3, "walks-along-road", State);
        AddOne(CodeKind.State, 4, "enters-door", State);
        AddOne(CodeKind.State, 5, "waits-out-step", State);
        AddOne(CodeKind.State, 6, "enters-building", State);
        AddOne(CodeKind.State, 7, "steps-onto-flag", State);
        AddOne(CodeKind.State, 0xf, "leaves-castle", State);
        AddOne(CodeKind.State, 0x10, "walks-free", State);
        AddOne(CodeKind.State, 0x11, "cuts-tree", State);
        AddOne(CodeKind.State, 0x12, "looks-for-tree", State);
        AddOne(CodeKind.State, 0x19, "looks-for-flag-home", State);
        AddOne(CodeKind.State, 0x1c, "claims-node-home", State);
        AddOne(CodeKind.State, 0x42, "waits-at-flag", State);

        AddOne(CodeKind.Building, Building.CastleType, "castle", "Building.CastleType");
        AddOne(CodeKind.Building, Building.WoodcutterType, "woodcutter-hut", "Building.WoodcutterType");
        AddOne(CodeKind.Building, StorehouseType, "storehouse", "Game.Removal storage types: the castle and type 0xa");

        return entries;
    }
}
