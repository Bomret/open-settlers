namespace OpenSettlers.MapView;

/// <summary>
/// Which sprite an object code draws (<c>docs/rules/map-objects.md</c>). The map drawing routine at <c>0x44d5b4</c>
/// subtracts 8 from the code and, below 24, picks a frame of an animation from a counter; the sprite entry is that
/// index plus 1250 (<c>Gfx_DrawObjectSprite</c>, <c>0x44da4d</c>).
/// </summary>
public static class ObjectSprites
{
    /// <summary>The first code this view draws besides the flag; codes 2 to 7 draw through other routines (buildings).</summary>
    public const int FirstCode = 8;

    /// <summary>The last object code (7 bits).</summary>
    public const int LastCode = 127;

    /// <summary>The entry of code 8.</summary>
    public const int FirstEntry = 1250;

    /// <summary>
    /// The sprite entry of a finished castle (<c>docs/rules/castle.md</c>): the object number 178, which the draw
    /// routine of the building type <c>0xe0</c> (<c>0x44e329</c>, through the jump table at <c>0x44e3aa</c> and the
    /// stub at <c>0x44fd2f</c>) stores, plus the 1250 that <c>0x44da4d</c> adds to an object number.
    /// </summary>
    public const int CastleEntry = FirstEntry + 178;

    /// <summary>
    /// The sprite entry of a flag (object code 1, <c>docs/rules/flags.md</c>): <c>0x44db26</c> hands <c>0x44da4d</c> the
    /// object number 0x80, plus the animation frame (state <c>+0x206</c> shifted right 3, 2 bits; 0 here) and the top
    /// two bits of the node's object byte shifted right 4, which are clear on a flag node, and <c>0x44da4d</c> adds
    /// the 1250. <b>Inferred:</b> the colour offset of the top bits is not drawn: a flag on a node whose object byte
    /// has bit 7 set (a water triangle) takes 8 more in the original.
    /// </summary>
    public const int FlagEntry = FirstEntry + 0x80;

    /// <summary>The object code of a flag.</summary>
    public const int FlagCode = 1;

    /// <summary>
    /// The sprite entry of a code, or null where the view draws none (a code of 2 to 7 or above 127; 0 is no object). The view
    /// draws the frame of the program's frame counter 0 (state <c>+0x206</c>); the animation rule is in
    /// <c>docs/rules/map-objects.md</c>. At counter 0 indexes 0 to 15 (codes 8 to 23) take the first frame of
    /// their group of 8 and indexes 16 to 23 (codes 24 to 31) the second frame of their group of 4.
    /// </summary>
    public static int? Entry(int code)
    {
        if (code == FlagCode)
        {
            return FlagEntry;
        }

        if (code is < FirstCode or > LastCode)
        {
            return null;
        }

        var index = code - FirstCode;
        index = index switch
        {
            < 16 => index & 0x78,
            < 24 => (index & 0x7c) + 1,
            _ => index,
        };

        return FirstEntry + index;
    }
}
