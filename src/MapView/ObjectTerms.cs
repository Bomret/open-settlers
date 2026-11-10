using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>
/// One draw term of the map's objects, named in game terms (INV-0010): a map object, a building, or a good at a flag.
/// <c>Row</c> is the tile row that orders it, and <c>X</c> and <c>Y</c> the position in map pixels the source's image
/// offsets hang the image from. A term holds no entry number, archive, or palette index.
/// </summary>
public abstract record ObjectTerm(int Row, int X, int Y)
{
    /// <summary>Asks <paramref name="source"/> what it draws for this term.</summary>
    public abstract ObjectDrawing? Ask(IGraphicsSource source);
}

/// <summary>A map object: its kind and the animation step (the game's frame counter) it is drawn at.</summary>
public sealed record MapObjectDraw(int Row, int X, int Y, MapObject Object, ushort Step) : ObjectTerm(Row, X, Y)
{
    /// <inheritdoc />
    public override ObjectDrawing? Ask(IGraphicsSource source) => source.Draw(this);
}

/// <summary>A finished building: the castle or a woodcutter's hut, by its type.</summary>
public sealed record BuildingDraw(int Row, int X, int Y, BuildingType Type) : ObjectTerm(Row, X, Y)
{
    /// <inheritdoc />
    public override ObjectDrawing? Ask(IGraphicsSource source) => source.Draw(this);
}

/// <summary>A good lying at a place of a flag; <c>X</c> and <c>Y</c> already hold the place's offset from the flag.</summary>
public sealed record GoodAtFlag(int Row, int X, int Y, Good Good) : ObjectTerm(Row, X, Y)
{
    /// <inheritdoc />
    public override ObjectDrawing? Ask(IGraphicsSource source) => source.Draw(this);
}

/// <summary>
/// The ripple of a tile with a water triangle at an animation step. <c>Tile</c> is the tile's record index (row times
/// width plus column), which varies the ripple from tile to tile.
/// </summary>
public sealed record WaterRipple(int Row, int X, int Y, int Tile, ushort Step);
