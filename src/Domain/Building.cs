namespace OpenSettlers.Domain;

/// <summary>
/// A finished building: one item of the original's building table (<c>Table18</c>, <c>docs/rules/settlers.md</c>). The slot in
/// the <see cref="BuildingTable"/> is the building's number; the castle is slot 0.
/// </summary>
public sealed class Building
{
    /// <summary>The content of a slot no building holds.</summary>
    public static Building Free() => new(BuildingType.None, -1, -1, -1);

    /// <summary>A building of <paramref name="type"/> on the node, tied to the flag in slot <paramref name="flag"/>.</summary>
    public Building(BuildingType type, int x, int y, int flag, bool constructing = false)
    {
        Type = type;
        X = x;
        Y = y;
        Flag = flag;
        Constructing = constructing;
    }

    /// <summary>The building type.</summary>
    public BuildingType Type { get; }

    /// <summary>True while the building is built: the castle at the start, and every building site.</summary>
    public bool Constructing { get; set; }

    /// <summary>
    /// What a building site still needs and the height it is levelled to; null for a finished building and for the castle,
    /// which is no <c>Building_Place</c> building (<c>docs/rules/build-sites.md</c>).
    /// </summary>
    public BuildingSite? Site { get; set; }

    /// <summary>The node the building stands on, column.</summary>
    public int X { get; }

    /// <summary>The node the building stands on, row.</summary>
    public int Y { get; }

    /// <summary>The slot of the building's flag in the flag table.</summary>
    public int Flag { get; }

    /// <summary>Whether a request for a worker found none; the retry scan clears it, and while it is set the building asks for no worker.</summary>
    public bool RequestFailed { get; set; }

    /// <summary>Whether a settler is on the way to be the building's worker.</summary>
    public bool WorkerOnTheWay { get; set; }

    /// <summary>Whether the worker is inside the building.</summary>
    public bool WorkerInside { get; set; }

    /// <summary>Whether the building was taken down: it burns until its countdown ends (<see cref="BurnLeft"/>).</summary>
    public bool IsDemolishing { get; set; }

    /// <summary>The status bits with no recovered meaning (bits 0, 1, 3, and 4 of byte 5), kept so the exporters stay lossless.</summary>
    public int OtherStatus { get; set; }

    /// <summary>The slot of the worker inside; 0 for none.</summary>
    public int Worker { get; set; }

    /// <summary>The clock units left of a building that burns, from 0x7ff when it is taken down (<c>docs/rules/removal.md</c>).</summary>
    public ushort BurnLeft { get; set; }

    /// <summary>The clock (16 bits) at the last turn of a building that burns; 0 until it is taken down.</summary>
    public ushort Stamp { get; set; }

    /// <summary>
    /// The fill counter of the goods it takes in that the building's low byte holds (byte 8). A delivery adds 0x0f to the counter of
    /// its kind of good, and only a carry out of the counter counts the good into the building's storage (<c>docs/rules/goods.md</c>).
    /// </summary>
    public int FillLow { get; set; }

    /// <summary>The fill counter of the goods the building's high byte holds (byte 9).</summary>
    public int FillHigh { get; set; }

    /// <summary>The castle's build progress (word at 12): the builder's turns add to it until it wraps.</summary>
    public ushort Progress { get; set; }

    /// <summary>Whether the slot holds no building: a building that burned down left it.</summary>
    public bool IsFree => Type == BuildingType.None;

    /// <summary>The fill counter of a kind.</summary>
    public int FillOf(FillCounter counter) => counter == FillCounter.High ? FillHigh : FillLow;

    /// <summary>Sets the fill counter of a kind, wrapped to a byte.</summary>
    public void SetFill(FillCounter counter, int value)
    {
        if (counter == FillCounter.High)
        {
            FillHigh = value & 0xff;
        }
        else
        {
            FillLow = value & 0xff;
        }
    }
}
