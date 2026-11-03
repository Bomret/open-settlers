namespace OpenSettlers.Simulation;

/// <summary>
/// A finished building: one item of the original's building table (<c>Table18</c>, 18 bytes). The slot in
/// <see cref="Game.Buildings"/> is the building's number; the castle is slot 0 (<c>docs/rules/settlers.md</c>).
/// </summary>
public sealed class Building
{
    /// <summary>
    /// The type of the castle. Its type byte holds 0xe0 while the castle is built, the type 0x38 of the table of door
    /// speeds, and 0x60 when it stands: <see cref="Constructing"/> is bit 7 of the byte.
    /// </summary>
    public const int CastleType = 0x18;

    /// <summary>
    /// The type of the woodcutter's hut: the handler of the type 2 (<c>0x42d945</c>) asks for job 5, whose work is to cut
    /// trees (<c>docs/rules/woodcutting.md</c>), and its sprite is the object 0xa8 (<c>0x44e63b</c>). The type 1
    /// asks for job 11, whose work looks for a free node by the water.
    /// </summary>
    public const int WoodcutterType = 2;

    /// <summary>The bit of <see cref="Status"/> that marks a building taken down: it burns until its countdown ends.</summary>
    public const int Demolishing = 0x20;

    /// <summary>The content of a slot no building holds (type -1).</summary>
    internal static Building Free() => new(-1, -1, -1, -1);

    internal Building(int type, int x, int y, int flag, bool constructing = false)
    {
        Type = type;
        X = x;
        Y = y;
        Flag = flag;
        Constructing = constructing;
    }

    /// <summary>The building type (bits 2 to 6 of byte 4).</summary>
    public int Type { get; }

    /// <summary>True while the building is built (bit 7 of byte 4), which only the castle is at the start.</summary>
    public bool Constructing { get; internal set; }

    /// <summary>The type as the tables of the walk index it: the type, plus 0x20 while the building is built.</summary>
    public int TypeIndex => Type | (Constructing ? 0x20 : 0);

    /// <summary>The node the building stands on, column.</summary>
    public int X { get; }

    /// <summary>The node the building stands on, row.</summary>
    public int Y { get; }

    /// <summary>The slot of the building's flag in <see cref="Game.Flags"/> (word at 6).</summary>
    public int Flag { get; }

    /// <summary>
    /// The status bits (byte 5): bit 2 is set after a request that found no worker, until the retry scan clears it;
    /// bit 6 when the worker is inside, bit 7 while the worker is on the way.
    /// </summary>
    public int Status { get; internal set; }

    /// <summary>
    /// The slot of the worker inside (word at 10), 0 for none. A building taken down holds the countdown of its burning here
    /// (<c>0x7ff</c> clock units at the start, <c>docs/rules/removal.md</c>).
    /// </summary>
    public int Worker { get; internal set; }

    /// <summary>The clock (16 bits) at the last turn of a building that burns (word at 14); 0 until it is taken down.</summary>
    public ushort Stamp { get; internal set; }

    /// <summary>Whether the slot holds no building: a building that burned down left it.</summary>
    public bool IsFree => Type < 0;

    /// <summary>Whether the building was taken down and burns (<see cref="Demolishing"/>).</summary>
    public bool IsDemolishing => (Status & Demolishing) != 0;

    /// <summary>
    /// Bytes 8 and 9 of the building's item: the fill counters of the goods it takes in. A delivery adds 0x0f to the byte of its
    /// kind of good, and only a carry out of the byte counts the good into the building's storage (<c>docs/rules/goods.md</c>).
    /// </summary>
    public ushort Fill { get; internal set; }

    /// <summary>The castle's build progress (word at 12): the builder's turns add to it until it wraps.</summary>
    public ushort Progress { get; internal set; }
}
