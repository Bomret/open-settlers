namespace OpenSettlers.Domain;

/// <summary>
/// One node of the map: the corner of a tile, with the height there, the owner of its territory, the terrain of the
/// tile's two triangles, the object that stands on it, the roads that leave it, and its marks. The original keeps the
/// same facts in packed bytes (<c>docs/rules/map-form.md</c>); the importer and the exporter of the adapters translate.
/// </summary>
public struct Node
{
    /// <summary>The greatest height a node has.</summary>
    public const int MaxHeight = 31;

    /// <summary>The height of the node's corner, 0 to <see cref="MaxHeight"/>.</summary>
    public int Height { get; set; }

    /// <summary>Whose territory the node lies in.</summary>
    public Owner Owner { get; set; }

    /// <summary>The terrain of the tile's upper triangle.</summary>
    public Terrain Upper { get; set; }

    /// <summary>The terrain of the tile's lower triangle.</summary>
    public Terrain Lower { get; set; }

    /// <summary>The object that stands on the node.</summary>
    public MapObject Object { get; set; }

    /// <summary>
    /// The original's mark (bit 7 of the object byte) of a node whose upper or lower triangle is water, or that lies next
    /// to such a node.
    /// </summary>
    public bool WaterTriangle { get; set; }

    /// <summary>The directions in which a road leaves the node.</summary>
    public RoadSet Roads { get; set; }

    /// <summary>
    /// The original's mark (bit 6 of the flag byte) of the middle of a lake. A node that carries a large object, such as a
    /// building, has it too.
    /// </summary>
    public bool LakeMark { get; set; }

    /// <summary>The original's mark (bit 7 of the flag byte) of a node a flag stands on.</summary>
    public bool FlagMark { get; set; }

    /// <summary>
    /// The resource type of the node's deposit, 0 for none. The original keeps the lake detail of a lake's middle (a draw
    /// from 0 to 7) in the same byte, so a lake detail is a deposit of type 0 whose amount is the draw.
    /// </summary>
    public int DepositType { get; set; }

    /// <summary>The amount of the node's deposit, 0 to 31.</summary>
    public int DepositAmount { get; set; }

    /// <summary>
    /// The lake detail of a lake's middle: the number the original keeps in the byte of the deposit. The object pass moves
    /// it between lake nodes and counts it up as a plain number, so a detail of 32 or more reads as a deposit of another
    /// type.
    /// </summary>
    public int LakeDetail
    {
        readonly get => DepositType << 5 | DepositAmount;
        set
        {
            DepositType = value >> 5 & 7;
            DepositAmount = value & 0x1f;
        }
    }

    /// <summary>
    /// The slot, in the flag table or the building table, of the flag or building whose node this is; null when the node
    /// links to nothing. The link takes the byte that holds the deposit, so a node with a link holds no deposit.
    /// </summary>
    public int? Link { get; private set; }

    /// <summary>The slot the node links to, 0 where it links to nothing (the original reads the byte whatever it holds).</summary>
    public readonly int LinkedSlot => Link ?? 0;

    /// <summary>Links the node to a table slot, 0 to 255; the deposit the link displaces is dropped.</summary>
    public void LinkTo(int slot)
    {
        Link = slot;
        DepositType = 0;
        DepositAmount = 0;
    }

    /// <summary>Takes the link and the deposit away.</summary>
    public void ClearLink()
    {
        Link = null;
        DepositType = 0;
        DepositAmount = 0;
    }
}
