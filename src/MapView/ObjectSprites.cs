using OpenSettlers.Domain;

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

    /// <summary>The number of object sprite entries: those of codes 8 to 127.</summary>
    public const int EntryCount = LastCode - FirstCode + 1;

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
    /// object number 0x80, plus the animation frame (state <c>+0x206</c> shifted right 3, 2 bits; see <see cref="Entry"/>) and the top
    /// two bits of the node's object byte shifted right 4, which are clear on a flag node, and <c>0x44da4d</c> adds
    /// the 1250. <b>Inferred:</b> the colour offset of the top bits is not drawn: a flag on a node whose object byte
    /// has bit 7 set (a water triangle) takes 8 more in the original.
    /// </summary>
    public const int FlagEntry = FirstEntry + 0x80;

    /// <summary>The number of frames a flag waves through: entries <see cref="FlagEntry"/> to <see cref="FlagEntry"/> plus 3.</summary>
    public const int FlagFrames = 4;

    /// <summary>
    /// The sprite entry of the finished building of type 2, the woodcutter's hut (<c>docs/rules/woodcutting.md</c>): the
    /// draw routine of the type (<c>0x44e329</c>, the jump table at <c>0x44e3aa</c>, the stub at <c>0x44e63b</c>)
    /// stores the object number 168, plus the 1250. The type 1 reaches the stub at <c>0x44e62b</c> with 167.
    /// </summary>
    public const int WoodcutterEntry = FirstEntry + 168;

    /// <summary>The entry of the sprite of good 0; a good lying on a flag draws entry 320 plus the good (<c>0x44de9a</c>).</summary>
    public const int GoodsFirstEntry = 320;

    /// <summary>The number of good sprites: the good's low 5 bits select one (<c>0x44db26</c> masks the byte with 0x1f).</summary>
    public const int GoodsCount = 32;

    /// <summary>The sprite entry of a good lying on a flag (<c>docs/rules/woodcutting.md</c>); a log is good 7, entry 327.
    /// <b>Inferred:</b> read from the decompile of <c>0x44de9a</c>; no emulator or debugger run of the draw confirms it.</summary>
    public static int GoodEntry(Good good) => GoodsFirstEntry + (good.Code & 0x1f);

    /// <summary>The first of the 16 water entries, drawn only over palette index 8 (<c>docs/rules/map-objects.md</c>).</summary>
    public const int WaterFirstEntry = 630;

    /// <summary>The number of water entries.</summary>
    public const int WaterCount = 16;

    /// <summary>
    /// The water entry of the tile with record index <paramref name="tile"/> (row times width plus column) at the frame
    /// counter <paramref name="counter"/>: <c>0x44ff59</c> takes the low word of the tile record's address masked with
    /// 0x3c, doubles it, flips it with 0xa8, adds the counter, shifts right 3 and keeps 4 bits. <b>Inferred:</b> the
    /// record table starts at an address whose bits 2 to 5 are clear, so the masked address is the tile index times 4.
    /// </summary>
    public static int WaterEntry(int tile, ushort counter) =>
        WaterFirstEntry + (((((tile * 4) & 0x3c) * 2 ^ 0xa8) + counter) >> 3 & 0xf);

    /// <summary>
    /// The sprite entry of a map object at the frame counter <paramref name="counter"/> (the game state's <c>+0x206</c>, the
    /// game clock in clock units), or null where the view draws none (a code of 2 to 7 or above 127; 0 is no object).
    /// Indexes 0 to 15 (codes 8 to 23) run through 8 frames and indexes 16 to 23 (codes 24 to 31) through 4, with the
    /// phase <c>(counter + index) &gt;&gt; 4</c> (<c>docs/rules/map-objects.md</c>). A flag waves through
    /// <see cref="FlagFrames"/> frames, <c>(counter &gt;&gt; 3) &amp; 3</c> (<c>0x44db26</c>).
    /// </summary>
    public static int? Entry(MapObject mapObject, ushort counter = 0)
    {
        var code = mapObject.Code;
        if (mapObject == MapObject.Flag)
        {
            return FlagEntry + ((counter >> 3) & (FlagFrames - 1));
        }

        if (code is < FirstCode or > LastCode)
        {
            return null;
        }

        var index = code - FirstCode;
        var phase = (counter + index) >> 4;
        index = index switch
        {
            < 16 => (index & 0x78) + (phase & 7),
            < 24 => (index & 0x7c) + (phase & 3),
            _ => index,
        };

        return FirstEntry + index;
    }
}
