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

/// <summary>
/// A building, by its type: the castle, a finished building of one of the four types of M4, or a building site. A site carries
/// the progress word of its building (<c>SiteProgress</c>, null for a finished building), which picks the drawing of its
/// construction stage: a source draws the stage a site that has not started shows (<c>docs/rules/build-sites.md</c>).
/// </summary>
public sealed record BuildingDraw(int Row, int X, int Y, BuildingType Type, ushort? SiteProgress = null)
    : ObjectTerm(Row, X, Y)
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

/// <summary>The three images a settler is drawn with, in the order they are drawn.</summary>
public enum SettlerPart
{
    /// <summary>The settler's figure, which its shadow comes before.</summary>
    Figure,

    /// <summary>The figure's colour overlay, filled with the player's colour.</summary>
    Overlay,

    /// <summary>The second figure, which some jobs draw over the first two.</summary>
    SecondFigure,
}

/// <summary>
/// The facts of the node a waiting carrier stands off: its column, whether it holds the lake mark, and the roads that
/// leave it. A source picks the carrier's figure and its offset by them.
/// </summary>
public sealed record WaitingNode(int Column, bool LakeMark, RoadSet Roads);

/// <summary>
/// One image of a settler, named in game terms (INV-0010): the settler's job, activity, heading (null for an activity with
/// none), the frame of its walk, the animation step (the time left of its step in clock units; for a waiting carrier, the
/// game's clock), its player, and the good it carries (none for none). <c>X</c> and <c>Y</c> are the settler's position
/// in map pixels, placed by the shared step offsets, and <c>Row</c> is the tile row that orders it. <c>Part</c> says which
/// of the settler's images this term asks for. <c>Node</c> is set for a carrier that waits off its node.
/// </summary>
public sealed record SettlerDraw(
    int Row,
    int X,
    int Y,
    SettlerPart Part,
    Job Job,
    Activity Activity,
    Heading? Heading,
    int Frame,
    int Step,
    int Player,
    Good Carried,
    WaitingNode? Node = null
) : ObjectTerm(Row, X, Y)
{
    /// <inheritdoc />
    public override ObjectDrawing? Ask(IGraphicsSource source) => source.Draw(this);
}
