namespace OpenSettlers.Domain;

/// <summary>What the engine names about an object code of a node. Every other code is <see cref="Other"/>.</summary>
public enum MapObjectKind
{
    /// <summary>Code 0: nothing stands on the node.</summary>
    None,

    /// <summary>Code 1: a flag (<c>docs/rules/flags.md</c>).</summary>
    Flag,

    /// <summary>Code 2: the node of a small building the player placed, a building site or a finished building.</summary>
    Hut,

    /// <summary>Code 3: the node of a large building the player placed (<c>docs/rules/build-sites.md</c>).</summary>
    LargeHut,

    /// <summary>Code 4: the castle (<c>docs/rules/castle.md</c>).</summary>
    Castle,

    /// <summary>Codes 8 to 23: a tree a woodcutter can fell (<c>docs/rules/woodcutting.md</c>).</summary>
    Tree,

    /// <summary>Codes 0x48 to 0x4f: a heap of stone a stonecutter can cut, the largest at 0x48 (<c>docs/rules/stonecutting.md</c>).</summary>
    StoneHeap,

    /// <summary>Code 0x53: the stump of a felled tree, which the object pass lets vanish by chance.</summary>
    Stump,

    /// <summary>Codes 0x5d to 0x66: a tree falling under the axe, in the steps of its cut.</summary>
    FallingTree,

    /// <summary>Codes 0x67 and 0x68: a small sapling a forester plants, which the object pass grows into a tree (<c>docs/rules/forestry.md</c>).</summary>
    Sapling,

    /// <summary>
    /// Codes 0x69 to 0x6e (a field growing), 0x6f (a harvested field), and 0x79 to 0x7e (a ripe field): a field a farmer sows and
    /// harvests, which the object pass grows and lets decay (<c>docs/rules/farming.md</c>).
    /// </summary>
    Field,

    /// <summary>A code the engine has no name for: a stone, a plant, or a code not recovered yet.</summary>
    Other,
}

/// <summary>
/// The object that stands on a node: a tree, a stone, a plant, a flag, a building, or another kind of the original's
/// object codes. The meaning of most codes is not established yet (<c>docs/rules/map-form.md</c>), so the raw code is
/// always kept, and a code with no name survives import and export unchanged.
/// </summary>
public readonly record struct MapObject
{
    /// <summary>The original's object code, 0 for none to 127.</summary>
    public required byte Code { get; init; }

    /// <summary>The first object code of a tree.</summary>
    public const int FirstTreeCode = 8;

    /// <summary>The object code after the last tree.</summary>
    public const int EndTreeCode = 0x18;

    /// <summary>The first object code of a stone heap, the heap with the most stones.</summary>
    public const int FirstHeapCode = 0x48;

    /// <summary>The last object code of a stone heap, the heap with one stone left; the cut after it removes the heap.</summary>
    public const int LastHeapCode = 0x4f;

    /// <summary>The object code of a stump.</summary>
    public const int StumpCode = 0x53;

    /// <summary>The first object code of a falling tree.</summary>
    public const int FirstFallingCode = 0x5d;

    /// <summary>The object code after the last falling tree.</summary>
    public const int EndFallingCode = 0x67;

    /// <summary>The object code of the first sapling, the one that grows into the trees 0x10 to 0x17 (<c>0x4290d5</c>).</summary>
    public const int SaplingCode = 0x67;

    /// <summary>The object code of the second sapling, the one that grows into the trees 8 to 0xf (<c>0x42910f</c>).</summary>
    public const int SecondSaplingCode = 0x68;

    /// <summary>The object code of a field just sown, the first of the growing codes 0x69 to 0x6e (<c>0x433b0e</c>).</summary>
    public const int SownFieldCode = 0x69;

    /// <summary>The object code of a fully grown field, which the object pass makes ripe (<c>0x429151</c>).</summary>
    public const int GrownFieldCode = 0x6e;

    /// <summary>The object code of a harvested field, which the object pass clears (<c>0x429184</c>).</summary>
    public const int HarvestedFieldCode = 0x6f;

    /// <summary>The object code of a field just ripe, the first of the ripe codes 0x79 to 0x7e.</summary>
    public const int RipeFieldCode = 0x79;

    /// <summary>The object code of the last ripe field, which the object pass makes the harvested field (<c>0x429184</c>).</summary>
    public const int LastRipeFieldCode = 0x7e;

    /// <summary>The object code after the last ripe field.</summary>
    public const int EndRipeFieldCode = LastRipeFieldCode + 1;

    /// <summary>The first object code the object pass clears while its decay counter is 0 (<c>0x42918f</c>): the signs 0x70 to 0x78.</summary>
    public const int FirstDecayingCode = 0x70;

    /// <summary>The object code after the last one the object pass clears while its decay counter is 0.</summary>
    public const int EndDecayingCode = 0x79;

    /// <summary>The first sapling.</summary>
    public static MapObject Sapling => new() { Code = SaplingCode };

    /// <summary>
    /// The tree a sapling grows into where the object pass's draw allows it (<c>0x4290d5</c>, <c>0x42910f</c>): the sapling's own
    /// range of trees, 0x10 or 8, plus the draw's low three bits.
    /// </summary>
    public static MapObject Grown(MapObject sapling, int draw) =>
        new() { Code = (byte)((sapling.Code == SaplingCode ? 0x10 : 8) + (draw & 7)) };

    /// <summary>
    /// True for a field a farmer may harvest: the fully grown code 0x6e and the ripe codes 0x79 to 0x7e (<c>0x4383cd</c> to
    /// <c>0x4383dd</c>).
    /// </summary>
    public bool IsHarvestable => Code is GrownFieldCode or (>= RipeFieldCode and < EndRipeFieldCode);

    /// <summary>
    /// The field after a harvest (<c>0x433aa0</c>): the code one up, where 0x6f becomes the first ripe code and 0x70 or 0x7f become the
    /// harvested field.
    /// </summary>
    public MapObject Harvested() =>
        new()
        {
            Code = (byte)(
                (Code + 1) switch
                {
                    HarvestedFieldCode => RipeFieldCode,
                    FirstDecayingCode or EndRipeFieldCode => HarvestedFieldCode,
                    var next => next,
                }
            ),
        };

    /// <summary>
    /// The field's code after one visit of the object pass (<c>0x429146</c> to <c>0x429184</c>), or null for a code the pass leaves:
    /// the growing and ripe codes go one up, the fully grown code becomes the first ripe code, the last ripe code becomes the harvested
    /// field, and the harvested field is cleared.
    /// </summary>
    public MapObject? AfterObjectPass =>
        Code switch
        {
            >= SownFieldCode and < GrownFieldCode or (>= RipeFieldCode and < LastRipeFieldCode) => new()
            {
                Code = (byte)(Code + 1),
            },
            GrownFieldCode => new() { Code = RipeFieldCode },
            LastRipeFieldCode => new() { Code = HarvestedFieldCode },
            HarvestedFieldCode => None,
            _ => null,
        };

    /// <summary>No object on the node.</summary>
    public static MapObject None => default;

    /// <summary>The stump of a felled tree.</summary>
    public static MapObject Stump => new() { Code = StumpCode };

    /// <summary>A flag.</summary>
    public static MapObject Flag => new() { Code = 1 };

    /// <summary>The node of a small building.</summary>
    public static MapObject Hut => new() { Code = 2 };

    /// <summary>The node of a large building.</summary>
    public static MapObject LargeHut => new() { Code = 3 };

    /// <summary>The castle.</summary>
    public static MapObject Castle => new() { Code = 4 };

    /// <summary>True when an object stands on the node.</summary>
    public bool Exists => Code != 0;

    /// <summary>
    /// True for the codes 1 to 4, the flag and the buildings of the player (<c>0x449f58</c> skips such a node when it moves
    /// a deposit).
    /// </summary>
    public bool IsStructure => Code is >= 1 and <= 4;

    /// <summary>
    /// True for the trees of the codes 8 to 15, which take the second variant of the cut (the animation with the original's
    /// byte 13 set).
    /// </summary>
    public bool HasSecondCutVariant => Code is >= FirstTreeCode and < 0x10;

    /// <summary>
    /// The tree after a step of its cut, 1 to 5 (<c>0x438a9e</c>): the codes 0x5d to 0x61, or 0x62 to 0x66 for the second
    /// variant.
    /// </summary>
    public static MapObject Falling(int step, bool secondVariant) =>
        new() { Code = (byte)(step + 0x5c + (secondVariant ? 5 : 0)) };

    /// <summary>What the engine names about the code.</summary>
    public MapObjectKind Kind =>
        Code switch
        {
            0 => MapObjectKind.None,
            1 => MapObjectKind.Flag,
            2 => MapObjectKind.Hut,
            3 => MapObjectKind.LargeHut,
            4 => MapObjectKind.Castle,
            >= FirstTreeCode and < EndTreeCode => MapObjectKind.Tree,
            >= FirstHeapCode and <= LastHeapCode => MapObjectKind.StoneHeap,
            StumpCode => MapObjectKind.Stump,
            >= FirstFallingCode and < EndFallingCode => MapObjectKind.FallingTree,
            SaplingCode or SecondSaplingCode => MapObjectKind.Sapling,
            >= SownFieldCode and <= HarvestedFieldCode => MapObjectKind.Field,
            >= RipeFieldCode and < EndRipeFieldCode => MapObjectKind.Field,
            _ => MapObjectKind.Other,
        };
}

/// <summary>A <see cref="MapObject"/> with the node it stands on.</summary>
public readonly record struct PlacedObject
{
    /// <summary>The node's column.</summary>
    public required int X { get; init; }

    /// <summary>The node's row.</summary>
    public required int Y { get; init; }

    /// <summary>The object.</summary>
    public required MapObject Object { get; init; }

    /// <summary>The object's raw code.</summary>
    public byte Code => Object.Code;
}
