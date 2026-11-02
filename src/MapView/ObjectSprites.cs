namespace OpenSettlers.MapView;

/// <summary>
/// Which sprite an object code draws (<c>docs/rules/map-objects.md</c>). The map drawing routine at <c>0x44d5b4</c>
/// subtracts 8 from the code and, below 24, picks a frame of an animation from a counter; the sprite entry is that
/// index plus 1250 (<c>Gfx_DrawObjectSprite</c>, <c>0x44da4d</c>).
/// </summary>
public static class ObjectSprites
{
    /// <summary>The first code this view draws; codes 1 to 7 draw through other routines (buildings and the like).</summary>
    public const int FirstCode = 8;

    /// <summary>The last object code (7 bits).</summary>
    public const int LastCode = 127;

    /// <summary>The entry of code 8.</summary>
    public const int FirstEntry = 1250;

    /// <summary>
    /// The sprite entry of a code, or null where the view draws none (a code below 8 or above 127). The view
    /// draws the frame of the program's frame counter 0 (state <c>+0x206</c>); the animation rule is in
    /// <c>docs/rules/map-objects.md</c>. At counter 0 indexes 0 to 15 (codes 8 to 23) take the first frame of
    /// their group of 8 and indexes 16 to 23 (codes 24 to 31) the second frame of their group of 4.
    /// </summary>
    public static int? Entry(int code)
    {
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
