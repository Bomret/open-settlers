namespace OpenSettlers.Domain;

/// <summary>What the engine names about an object code of a node. Every other code is <see cref="Other"/>.</summary>
public enum MapObjectKind
{
    /// <summary>Code 0: nothing stands on the node.</summary>
    None,

    /// <summary>Code 1: a flag (<c>docs/rules/flags.md</c>).</summary>
    Flag,

    /// <summary>Code 2: the hut of a building the player placed.</summary>
    Hut,

    /// <summary>Code 4: the castle (<c>docs/rules/castle.md</c>).</summary>
    Castle,

    /// <summary>Codes 8 to 23: a tree a woodcutter can fell (<c>docs/rules/woodcutting.md</c>).</summary>
    Tree,

    /// <summary>Code 0x53: the stump of a felled tree, which the object pass lets vanish by chance.</summary>
    Stump,

    /// <summary>Codes 0x5d to 0x66: a tree falling under the axe, in the steps of its cut.</summary>
    FallingTree,

    /// <summary>A code the engine has no name for: a stone, a plant, or a code not recovered yet.</summary>
    Other,
}

/// <summary>
/// The object that stands on a node: a tree, a stone, a plant, a flag, a building, or another kind of the original's
/// object codes. The meaning of most codes is not established yet (<c>docs/rules/map-form.md</c>), so the raw code is
/// always kept, and a code with no name survives import and export unchanged.
/// </summary>
/// <param name="Code">The original's object code, 0 for none to 127.</param>
public readonly record struct MapObject(byte Code)
{
    /// <summary>The first object code of a tree.</summary>
    public const int FirstTreeCode = 8;

    /// <summary>The object code after the last tree.</summary>
    public const int EndTreeCode = 0x18;

    /// <summary>The object code of a stump.</summary>
    public const int StumpCode = 0x53;

    /// <summary>The first object code of a falling tree.</summary>
    public const int FirstFallingCode = 0x5d;

    /// <summary>The object code after the last falling tree.</summary>
    public const int EndFallingCode = 0x67;

    /// <summary>No object on the node.</summary>
    public static MapObject None => default;

    /// <summary>The stump of a felled tree.</summary>
    public static MapObject Stump => new(StumpCode);

    /// <summary>A flag.</summary>
    public static MapObject Flag => new(1);

    /// <summary>The hut of a building.</summary>
    public static MapObject Hut => new(2);

    /// <summary>The castle.</summary>
    public static MapObject Castle => new(4);

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
    public static MapObject Falling(int step, bool secondVariant) => new((byte)(step + 0x5c + (secondVariant ? 5 : 0)));

    /// <summary>What the engine names about the code.</summary>
    public MapObjectKind Kind =>
        Code switch
        {
            0 => MapObjectKind.None,
            1 => MapObjectKind.Flag,
            2 => MapObjectKind.Hut,
            4 => MapObjectKind.Castle,
            >= FirstTreeCode and < EndTreeCode => MapObjectKind.Tree,
            StumpCode => MapObjectKind.Stump,
            >= FirstFallingCode and < EndFallingCode => MapObjectKind.FallingTree,
            _ => MapObjectKind.Other,
        };
}

/// <summary>A <see cref="MapObject"/> with the node it stands on.</summary>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
/// <param name="Object">The object.</param>
public readonly record struct PlacedObject(int X, int Y, MapObject Object)
{
    /// <summary>The object's raw code.</summary>
    public byte Code => Object.Code;
}
