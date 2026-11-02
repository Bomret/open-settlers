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
    /// The sprite entry of a code, or null where the view draws none (a code below 8 or above 127). <c>counter</c> is
    /// the program's frame counter (state <c>+0x206</c>); a still frame takes 0. Indexes 0 to 15 (codes 8 to 23)
    /// form two animations of 8 frames, indexes 16 to 23 (codes 24 to 31) two of 4 frames. The frame is the
    /// counter plus the index, shifted right by 4, so the low bits of the code offset each object's phase.
    /// </summary>
    public static int? Entry(int code, int counter = 0)
    {
        if (code is < FirstCode or > LastCode)
        {
            return null;
        }

        var index = code - FirstCode;
        if (index < 16)
        {
            var phase = ((counter + index) >> 4) & 7;
            index = (index & 0x78) + phase;
        }
        else if (index < 24)
        {
            var phase = ((counter + index) >> 4) & 3;
            index = (index & 0x7c) + phase;
        }

        return FirstEntry + index;
    }
}
