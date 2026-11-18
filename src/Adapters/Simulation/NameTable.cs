using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// Open Settlers' names for the original's bare codes: map object codes and settler states (ADR 0010, INV-0006). The goods, building
/// types, and jobs are entries of the content catalog, which name themselves (ADR 0020). Each entry cites its evidence
/// beside it. A code with no entry has no name and shows its raw value. A name several codes share (<c>tree</c>) resolves to all of
/// them through <see cref="CodesNamed"/>. The object names read the class table the engine loaded (<see cref="RuleTables"/>):
/// <c>stone</c> names a code the docs call a stone only where that table says class 2. The state names need no table and are
/// static. The names of the states a job owns come from the jobs' tables modules of the History
/// Edition source through <see cref="JobTableNames"/>, the one type of that source this assembly names (INV-0011); the
/// states that belong to no one job are stated here.
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
        State,
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
    internal const string StateEvidence = JobTableNames.StateEvidence;

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

    /// <summary>The name of the settler state, or null where the table has none.</summary>
    public static string? StateName(int state) => Find(CodeKind.State, state);

    /// <summary>The object codes the name stands for, in ascending order; empty for an unknown name.</summary>
    public IEnumerable<int> CodesNamed(string name) =>
        Entries
            .Where(entry => entry.Kind == CodeKind.Object && entry.Name == name)
            .Select(entry => entry.Code)
            .Where(code => ObjectName((byte)code) == name);

    /// <summary>Whether the table names an object or a state <paramref name="name"/>.</summary>
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

        // The names of the states a job owns come from the tables modules (JobTableNames); the states that
        // belong to no one job, the walks and the door among them, are here.
        foreach (var (code, name, evidence) in JobTableNames.States)
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
        Add(
            CodeKind.Object,
            MapObject.SownFieldCode,
            MapObject.GrownFieldCode + 1,
            "field",
            "the sowing 0x433b0e puts 0x69 on the node, and the object pass takes each code one up to 0x6e and then to the ripe 0x79 (0x429146, 0x429151); the farmer harvests 0x6e (0x4383cd); class 2; docs/rules/farming.md"
        );
        AddOne(
            CodeKind.Object,
            MapObject.HarvestedFieldCode,
            "harvested-field",
            "the harvest 0x433aa0 leaves 0x6f after the last ripe code, and the object pass clears it (0x429184); class 0; docs/rules/farming.md"
        );
        Add(
            CodeKind.Object,
            MapObject.RipeFieldCode,
            MapObject.EndRipeFieldCode,
            "ripe-field",
            "the object pass takes 0x6e to 0x79 and each ripe code one up to 0x7e (0x429151, 0x429165), which the farmer harvests (0x4383d3); class 2; docs/rules/farming.md"
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

        return entries;
    }
}
