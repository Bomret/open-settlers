using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Open Settlers' names for the original's bare codes: map object codes, settler jobs, settler states, and building
/// types (ADR 0010, INV-0006). Each entry cites its evidence beside it. A code with no entry has no name and shows its
/// raw value. A name several codes share (<c>tree</c>) resolves to all of them through <see cref="CodesNamed"/>.
/// </summary>
public static class NameTable
{
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
        "Game.Woodcutting EndTree/EndFirstTrees; docs/rules/woodcutting.md (codes 8 to 0x17); class 1";
    private const string StoneEvidence =
        "class 2 of ProgramTables.Classes (docs/rules/build-sites.md); docs/rules/map-object-codes.md (codes 0x48 to 0x4f)";

    /// <summary>Every entry of the table.</summary>
    internal static IReadOnlyList<Entry> Entries { get; } = Build();

    /// <summary>The name of the object code, or null where the table has none.</summary>
    public static string? ObjectName(byte code) => Find(CodeKind.Object, code);

    /// <summary>
    /// The name of the object code as the class table <paramref name="tables"/> reads it: a <c>stone</c> only where the
    /// table says class 2, null for a stone code the class table gives another class.
    /// </summary>
    public static string? ObjectName(byte code, ProgramTables tables)
    {
        var name = ObjectName(code);
        return name == "stone" && tables.Classes[code & 0x7f] != StoneClass ? null : name;
    }

    /// <summary>The name of the settler job, or null where the table has none.</summary>
    public static string? JobName(int job) => Find(CodeKind.Job, job);

    /// <summary>The name of the settler state, or null where the table has none.</summary>
    public static string? StateName(int state) => Find(CodeKind.State, state);

    /// <summary>The name of the building type, or null where the table has none.</summary>
    public static string? BuildingName(int type) => Find(CodeKind.Building, type);

    /// <summary>The object codes the name stands for, in ascending order; empty for an unknown name.</summary>
    public static IEnumerable<int> CodesNamed(string name) =>
        Entries.Where(entry => entry.Kind == CodeKind.Object && entry.Name == name).Select(entry => entry.Code);

    private static string? Find(CodeKind kind, int code)
    {
        foreach (var entry in Entries)
        {
            if (entry.Kind == kind && entry.Code == code)
            {
                return entry.Name;
            }
        }

        return null;
    }

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

        const string Map = "docs/rules/map-object-codes.md";
        Add(CodeKind.Object, 1, 2, "flag", "FlagPlacement.FlagCode; class 3; docs/rules/flags.md");
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
        Add(CodeKind.Object, 0x52, 0x53, "cross", $"{Map}: sprite; MapGen_MarkPeaks puts it on peaks");
        Add(
            CodeKind.Object,
            Stump,
            Stump + 1,
            "stump",
            "Game.Woodcutting FirstHandledObject; docs/rules/woodcutting.md"
        );
        Add(CodeKind.Object, 0x5a, 0x5c, "cactus", $"{Map}: class 1; sprite; terrain 8 to 10");
        Add(CodeKind.Object, 0x5c, 0x5d, "dead-tree", $"{Map}: class 1; sprite; docs/rules/woodcutting.md");
        Add(
            CodeKind.Object,
            FirstCutObject,
            EndCutObject,
            "felled-tree",
            "Game.Woodcutting FirstCutObject/EndCutObject; docs/rules/woodcutting.md (the cut writes 0x5d to 0x66)"
        );

        Add(
            CodeKind.Job,
            Settler.CarrierJob,
            Settler.CarrierJob + 1,
            "carrier",
            "Settler.CarrierJob; docs/rules/carriers.md"
        );
        Add(
            CodeKind.Job,
            BuilderJob,
            BuilderJob + 1,
            "builder",
            "Game.Removal reads job 4 as the builder; docs/rules/settlers.md"
        );
        Add(
            CodeKind.Job,
            Settler.WoodcutterJob,
            Settler.WoodcutterJob + 1,
            "woodcutter",
            "Settler.WoodcutterJob; docs/rules/woodcutting.md"
        );
        Add(CodeKind.Job, JoblessJob, JoblessJob + 1, "jobless", "Settler.Job doc: 21 is a settler without a job yet");

        const string State = "Settler.State doc";
        Add(CodeKind.State, 1, 2, "waits-in-castle", State);
        Add(CodeKind.State, 2, 3, "walks", State);
        Add(CodeKind.State, 3, 4, "walks-along-road", State);
        Add(CodeKind.State, 4, 5, "enters-door", State);
        Add(CodeKind.State, 5, 6, "waits-out-step", State);
        Add(CodeKind.State, 6, 7, "enters-building", State);
        Add(CodeKind.State, 7, 8, "steps-onto-flag", State);
        Add(CodeKind.State, 0xf, 0x10, "leaves-castle", State);
        Add(CodeKind.State, 0x10, 0x11, "walks-free", State);
        Add(CodeKind.State, 0x11, 0x12, "cuts-tree", State);
        Add(CodeKind.State, 0x12, 0x13, "looks-for-tree", State);
        Add(CodeKind.State, 0x19, 0x1a, "looks-for-flag-home", State);
        Add(CodeKind.State, 0x1c, 0x1d, "claims-node-home", State);
        Add(CodeKind.State, 0x42, 0x43, "waits-at-flag", State);

        Add(CodeKind.Building, Building.CastleType, Building.CastleType + 1, "castle", "Building.CastleType");
        Add(
            CodeKind.Building,
            Building.WoodcutterType,
            Building.WoodcutterType + 1,
            "woodcutter-hut",
            "Building.WoodcutterType"
        );
        Add(
            CodeKind.Building,
            StorehouseType,
            StorehouseType + 1,
            "storehouse",
            "Game.Removal storage types: the castle and type 0xa"
        );

        return entries;
    }
}
