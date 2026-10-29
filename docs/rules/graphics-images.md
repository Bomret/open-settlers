# Graphics images

This note covers the palette entries of the graphics archives, the 10-byte header that every graphics entry
starts with, and the raw images: the entries that the program draws as plain rows of palette indices. Sprites
with transparency and glyphs read the same header and are other notes; the masks are below. How the archives are loaded and
numbered is in [Graphics archives](graphics-archives.md).

## The palette

**Status:** confirmed for the reading, the 6-bit reduction, and color 0. **Inferred** for the times-4 display.
**Platform layer.**

`Palette_Load` at `0x4071c0` takes an entry number, finds the entry the way every reader does, and reads 256
colors of three bytes each (768 bytes) into the current palette at `0x6816d0`. It keeps the top 6 bits of each
component, so a component runs from 0 to 63. After the loop it sets color 0 to black, whatever the file holds.
The routine never looks at the entry's size: it reads 768 bytes from the entry's address.

The program loads three palette entries:

- Entry 3 is the game's palette. `0x407250` (which also sets a flag at `0x6812d8`) and `0x407400` load it; they
  are called from `0x4471f9`, `0x447200`, `0x4250a8`, and `0x45229d`.
- Entry 3997 is loaded at `0x451ee9`, in the routine that clears the surface and plays the title scene, and
  the scene's drawing follows the load.
- Entry 3998 is loaded in `Platform_RunGame` (the call at `0x4075ac`), before the first screens.

`Frame_Present` at `0x406880` hands the current palette to the routine at `0x4080e0`, which shifts each packed
color left by 2 bits, so a displayed component is the 6-bit value times 4 (0 to 252). **Inferred**: the routine
is presentation code and is not read in full. How the engine shows palette graphics in the game stays open.
The browser shows each palette component times 4.

The reference install holds each of the three entries at exactly 768 bytes in all three archives.

## The header

**Status:** confirmed for the width, the height, and the two offsets, which the routines at `0x405730`,
`0x405590`, `0x405880`, `0x405b80`, and `0x47b19c` read. **Inferred** that the first two bytes carry no
drawing mode: none of those routines reads them (`0x406610` reads them as offsets, see below).

Every graphics entry starts with a header of 10 bytes, all little-endian:

- Byte 0 and byte 1: two signed bytes. Only `0x406610` reads them, in an entry of 1850 to 2390, as the signed offsets of the
  second figure (**confirmed**, see Transparent sprites). Byte 0 is 1 in every one of
  the 432 raw images of the reference install (the same in all three archives), which fits a kind tag, and
  the second byte is 0 in each of them. Across all present graphics entries the pairs are not uniform: 1 and 0 in
  1697, 2 and 0 in 281, 0 and 0 in 157, 0 and -8 in 78, -1 and -8 in 58, and a few hundred more in other
  combinations. The status stays inferred: the byte may be a kind tag that the routines read so far do not use.
- Bytes 2 and 3: the width in pixels.
- Bytes 4 and 5: the height in pixels.
- Bytes 6 and 7: a signed offset from the draw position to the image's left edge.
- Bytes 8 and 9: a signed offset from the draw position to the image's top edge.

The pixel data starts at byte 10. Its layout depends on the routine.

## Raw images

**Status:** confirmed for the routine and for every range whose entry numbers the code fixes. **Inferred** for
a range where the code adds a variable index to a base (marked below); the range then ends where the archives'
entries end.

`Gfx_DrawSprite` at `0x4060b0` reads the entry number, the position, and the target surface from the EDI
block (see [Game code](game-code.md)), finds the entry, and calls `Gfx_BlitRaw` at `0x405730`. That routine
adds the entry's two offsets to the draw position, clips the rectangle to the surface's clip rectangle, and
copies the rows of palette indices to the surface, one row after the other from the top, each row as wide as
the image. It tests no pixel value: index 0 is drawn like any other, so a raw image is opaque. An entry of
this kind is exactly 10 bytes plus width x height bytes.

The call sites of `Gfx_DrawSprite` that give these entry numbers:

| Entries | Where | Palette in effect |
| --- | --- | --- |
| 1 | `0x452394` | 3997 |
| 5 to 11 | `0x452400`; a table of 0 to 6 plus 5. Inferred bounds. | 3997 |
| 15 to 21 | `0x452443`; a counter of 0 to 6 plus 15 | 3997 |
| 25 to 38 | `0x45f9ae`; a value from the game state plus 25. Inferred bounds. | 3 |
| 40 and 41 | `0x41ea7c`, `0x41eaaa` | 3998 |
| 42 to 57 | the list at `0x674824`, drawn at `0x41ee33` (3998); also `0x425926` as 42 plus an index, in the setup that loads palette 3 at `0x4250a8` (3) | 3998 and 3 |
| 600 to 603 | `0x42105f` to `0x4213e9` | 3 |
| 660 to 663 | `0x421412` to `0x42152a`, `0x45cf10` | 3 |
| 670 to 677 | `0x45cf57` to `0x45d02c`; 670 to 673, and the same plus 4 | 3 |
| 870 to 1187 | `0x44de86`, `0x467bd4`; a tile index plus 870. Inferred bounds. | 3 |
| 1750 to 1774 | `0x44d157`; a byte plus 1750. Inferred bounds. | 3 |
| 1780 to 1796 | the lists at `0x420d9f`, `0x420e19`, `0x420eab`, `0x420f25` drawn at `0x420d97`; 1782 to 1785 at `0x44d24f` and `0x44d2e8` | 3 |
| 1800 to 1805 | the same four lists | 3 |
| 3880 to 3882 | `0x421251` to `0x42136d` | 3 |

Entry 1 is 640 x 200 in the reference install. The palette column is the palette that the program has loaded
while it draws the entry (**inferred** from the order of loads and draws: the title scene follows the load of
3997, the first screens follow the load of 3998, and the game follows the load of 3). The engine draws an entry
in that palette. A range of the table is one kind in the engine: `RawImageKind` claims it by entry number, in
all three archives.

Entries 42 to 57 are drawn in both palettes. The setup at `0x4250a8` loads palette 3 and stores two index
tables in the game state; the draw at `0x425926` reads the second, whose 16 values are 0 to 15 in some order, and
adds 42, so the code fixes the bound 42 to 57 (confirmed). The six palette indices that these images use have
the same color in 3 and in 3998 in the reference install, so their preview is the same in either, and
`RawImageKind` uses 3998.

All entries of these ranges in the reference archives have exactly the size that their width and height give.

## Transparent sprites

**Status:** confirmed for the encoding and for the two routines. Confirmed for the ranges whose call site fixes a
base (marked below), with the upper bound inferred from the entries the archives hold.

`0x405590` draws a sprite and `0x405880` draws the same stream in one color. Both take the entry's address and
the position, add the entry's two offsets to the position, and clip against the clip rectangle like
`Gfx_BlitRaw`. The pixels after the header are the rows from the top, one after the other. A row is a series of
runs. A run is a skip byte, a count byte, and as many palette indices as the count. The skip moves the pen to
the right over pixels that stay as they are. A run with a count of 0 ends the row, and the program ignores its
skip. `0x405590` copies the indices of a run to the surface, so an index of 0 is drawn like any other, and
`0x405880` writes one color value in place of those indices and skips the indices in the stream. The program
reads the stream with no check against the width, the height, or the entry's size: it trusts the entry.

The two routines are reached through these callers (call sites in parentheses). `0x4062e0` draws a sprite with
`0x405590` (called at `0x4286b8`, `0x44dace`, `0x44dec8`, `0x44fd59`). `0x406350` (called at `0x44ff45`) and `0x406590`
(called at `0x44db12`) also draw one with `0x405590`; `0x406590` draws it at the position with the entry's offsets
left out. `0x406490` draws one with `0x405880` (called at `0x4512da` and `0x45130f`). `0x406610` (called at
`0x450189`) draws a settler: the figure from the base 1850 through `0x405590`, then the entry of the same index
plus 2500 through `0x405880` with the player's color (40 hex plus four times a player number; `add 0x9c4` at
`0x40669a`, the call at `0x4066bf`), then, where a second figure is set, an entry from the base 3150 through
`0x405590`. All of this is **confirmed** from the disassembly. The entries of 2500 and up are stencils: they
store only the indices 0 and 1, and the program draws every drawn pixel in the player's color. In every archive
entry 1850 + i is present exactly when 2500 + i is. `0x406610` reads the header bytes 0 and 1 of the entry
1850 + i as signed offsets (`0x4066d2`, `0x4066e4`) added to the position of the second figure (**confirmed**).

The entries of a transparent sprite, in the three archives alike. Each present entry of a range, and no other
one in it, decodes as a sprite of at least one drawn pixel. All present entries of the three archives are the
same.

| Entries | Where | Drawn |
| --- | --- | --- |
| 321 to 372 | `0x44de9a` and `0x44fd4a`: an index plus 320 | palette |
| 610 to 619 | `0x428528`: 610 to 612, those plus 3 or 6, and 619 | palette |
| 750 to 793 | `0x4511d5` through `0x406490`: a character's glyph, the character's code plus 749 | one color, index 31 |
| 810 to 853 | the same routine, the glyph plus 809, as the shadow of the text | one color, index 1 |
| 1250 to 1273 | `0x44da4d`: an index plus 1250 | palette |
| 1314 to 1362, 1378 to 1443 | `0x44da4d`, `0x406350`, `0x406590`: an index plus 1250 (64 to 112 and 128 to 193) | palette |
| 1850 to 2390 | `0x406610`: an index plus 1850, the figure | palette |
| 2500 to 2701, 2740 to 2965, 3000 to 3040 | `0x406610` through `0x405880`: an index plus 2500, the overlay of the figure 1850 + i | one color, the player's color |
| 3150 to 3450 | `0x406610`: an index plus 3150 | palette |
| 3456 to 3732, every sixth | `0x406610`: the first entry of each group of six that `Archive_FillAliases` fills | palette |
| 3738 to 3764 and 3768 to 3779 | `0x406610`: an index plus 3150 | palette |

The overlays of 2500 to 3040 include 2670 and 3010 to 3032, which have no drawn pixel (every row is empty): the
program reads them like the others, and they draw nothing. The preview shows an empty transparent image of their
size. The player's color is **stand-in**: the program picks the player at run time, and the preview uses the first
player's color, palette index 40 hex.

The text routines in `0x4511d5` fill the color from the game state: five sites (`0x450fde`, `0x451043`,
`0x4510a8`, `0x451111`, `0x451173`) set the text color, and each jumps to `0x4511d5` (none calls it). The sites at
`0x450fde`, `0x4510a8`, and `0x451111` set 31 and the site at `0x451043` sets 47, and each sets 1 as the shadow
color; the last site, `0x451173`, sets 31 and shadow 0, which draws no shadow. Palettes 3 and
3998 agree on colors 1, 31, and 47, so the preview uses palette 3 and index 31. The text color is **stand-in**:
the program picks it at run time.

The base of each range is confirmed by the routine named; the upper bound of a range is the last entry that
the archives hold and that decodes as a sprite: the game script code computes the index (the EDI block holds
the entry), and no direct number shows it. None of the 432 raw images decodes this way, so the shape tells the
two kinds apart. Eleven entries inside the ranges (1909, 1918, 1938, 2019, 2023, 2212, 2378, 3176,
3193, 3233, 3415) happen to have the size a raw image would have and decode as sprites: the neighbouring entries
are animation frames, and they are sprites.

The range 1314 to 1443 is based on 1250, not on 320, by its callers: `0x44da9e` and `0x44ff24` draw the entry of
the index plus 1500 (the shadow) with the same index just before `0x44dac8` and `0x44ff3f` draw the index plus
1250, and 1250 + i is present exactly when 1500 + i is (i = 0 to 249). The base 320 has no such pairing.

Entries 448 to 521 (including 490) and 3999 decode as sprites, and no reader was found for them: the search is
in [Graphics archives](graphics-archives.md#entries-no-routine-reads), and no sprite kind claims them. Entries 630
to 645 have a reader of their own, below. Entries 1597 and 1598 have all rows empty and lie inside the mask ranges.

### Sprites drawn over color 8

**Status:** confirmed for the routines and the entry range. The reading of "color 8" is the one the routine shows:
it is a palette index of the surface, and what it stands for in the picture is open.

Entries 630 to 645 hold the stream of a transparent sprite and are drawn by a routine of their own. `0x44ff59`,
called from `0x44d5b4`, takes a value of the object being drawn and the counter at `+0x206` of the game state
block, combines them into a number from 0 to 15, and adds 630 (the add is at `0x44ffc7`), so the sixteen entries
are sixteen phases of one picture. It calls `0x406420`, which sets the clip rectangle and calls `0x405d00`, which
calls `0x47b4f9`, the last routine of the game code band. That routine reads the same stream as `Gfx_BlitSprite`
(skip, count, indices, a count of 0 ending the row) and clips the same way, but writes a pixel only where the
surface pixel already has the palette index 8. A drawn pixel over any other index is left as it is.

The engine decodes them as the other sprites do (`SpriteFile.Read`, palette 3), in the kind `SpriteOverColor8Kind`.
The preview shows every pixel of the stream, because the surface under it is not known; the summary says so.

### Corrupt sprites

**Status:** stand-in. The program checks none of these. The engine reports each for its own entry.

- A sprite shorter than its 10-byte header is unreadable.
- A sprite of width 0 or height 0 is unreadable: nothing to draw.
- A row that has a run ending past the width is unreadable. The message names the row and the width.
- A stream that ends inside a run or before the pair that ends a row is unreadable. The message names the row.
- A height larger than half the stream (each row needs at least the pair that ends it) is unreadable, whatever
  the width: the check comes before anything is allocated. The message names the height.
- Bytes after the last row are ignored.

### Sprites in the engine

`SpriteFile.Read` in `src/Formats` decodes an entry into one index per pixel and a flag for each pixel the
program draws; `Sprite.ToRgba` lowers the alpha of the others to 0, or draws every drawn pixel in one color.
`SpriteKind` in `src/Assets` claims the ranges above, draws in palette 3, and gives the entries of 750 to 853
their color and the entries of 2500 to 3040 the first player's color. The offsets only appear in the summary: the preview draws the sprite alone at its own size.

## Masks

**Status:** confirmed for the stream layout and for the routines that read it. Each entry range below is the
numbers some site in the code reaches; the textured-fill ranges are **inferred** at their ends, because the code
reaches them through a height difference whose bound the code does not check (below). A mask is a graphic that holds only runs of skipped and covered pixels and
no pixel colors. The routines that read the stream are `Gfx_BlitShadow` at `0x405b80` and `Gfx_BlitTextured` at
`0x47b19c`. An entry that carries pixel colors in its runs is a sprite, even when the program draws it in one
color, and belongs to the sprite note.

The stream after the 10-byte header has one row after the other from the top, as many rows as the height. A row
is pairs of two bytes: a skip and a cover. The skip moves right from where the previous run ended (from the left
edge for the first run), the cover is the number of pixels to fill from there. A pair whose cover is 0 ends the
row and its skip byte is not used. Both routines clip against the clip rectangle and skip the rows above it by
walking pairs to each end pair. A row with no run is only its end pair.

How the program draws a mask:

- **Shadow.** `Gfx_DrawShadow` at `0x4061a0` hands the entry to `Gfx_BlitShadow`, which stores palette index
  `0x80` on every covered pixel. It blends nothing, so a shadow is a hard-edged shape in one color.
- **Textured fill.** `Gfx_DrawTexturedFill` at `0x406120` hands a mask and a raw image to `Gfx_BlitTextured`,
  which copies, on each covered pixel, the pixel of the texture at the same distance from the mask's position,
  repeating the texture down only: within a row the texture pointer advances with the destination and never
  wraps, and after the texture's height in rows it returns to the first row. The texture is a raw image (10 bytes
  plus width x height) and has its own header.
- **Player color.** The routine at `0x406610` draws one entry in the color `0x40` plus 4 times the player through
  `0x405880`. That routine skips cover bytes of pixel data in the stream, so the entries it draws are sprites
  (entries 2500 to 2701, 2740 to 2965, and 3000 to 3040, among them the empty 2670 and 3010 to 3032), and no mask of
  the reference install is drawn that way. The sprite note describes the routine and previews those entries in the
  first player's color.

Entry ranges, in all three archives, with the routine that reads them:

| Entries | Kind | Routine and how the number arises |
| --- | --- | --- |
| 4 | shadow | `0x45006a` stores 4 and the call at `0x45007d` reaches `Gfx_DrawShadow`. Confirmed |
| 1500 to 1612, 1619 | shadow | `0x44da4d` adds 1500 to an object's number and calls `Gfx_DrawShadow` at `0x44daa4`; the same number plus 1250 is the object's sprite. Objects 0 to 119 reach it from the call at `0x44d6e8`: the number is a byte of the map object, either kept as it is (24 and up) or combined with a phase (0 to 23). Confirmed. 1613 to 1618 are copies of 1602 (see [Graphics archives](graphics-archives.md)). |
| 1628 to 1645 | shadow | Objects 128 to 143 reach `0x44da4d` from the call at `0x44dcad` (128 plus 0, 4, 8, or 12 plus a phase of 0 to 3), object 144 from the jump at `0x44fa74`, and object 145 from the call at `0x44fc21`. Confirmed |
| 1652 to 1671, 1674 | shadow | Objects 152 to 171 and 174: the object routines in `0x44e62b` to `0x44f928` store each number and jump or call into `0x44da4d` (sites `0x44e636` to `0x44f928`). Confirmed. Objects 172 and 173 appear in none of them, so entries 1672 and 1673 are left unclaimed |
| 1675 to 1693 | shadow | Objects 175 to 177, 179 to 187, and 193 come from the 24-entry table that the call at `0x44fcac` indexes at `0x44fc87` (an index of 1 to 23 that the stores from `0x44f933` to `0x44fa5d` set); 178 and 192 are stored before the call at `0x44f928` and the jump at `0x44ec2a`; 188 to 191 are 188 plus 0 to 3 at `0x44f3a8` (jump at `0x44f3ae`). Confirmed. The same table values are also drawn through the second shadow routine at `0x406210`, which `0x44ff2a` calls with the number plus 1500. That call is reached for 152 to 171, 174, 188, and 192 from `0x44fcd0` through the table at `0x44fd16`, and for 178 from `0x44fd30` |
| 60 to 140 | textured fill | `Gfx_DrawTexturedFill`, called at `0x4279a9`: the mask is 60 plus an index from the heights of three neighbouring nodes, `(h1 + 4 - h2) + 9 x (h1 + 4 - h3)` (formed at `0x4278be`), which indexes the 81-entry tables of 9 x 9 combinations. The range assumes the height difference to a neighbour is at most 4 either way, which the 9 x 9 tables encode and the code does not check. Confirmed routine, inferred bound |
| 141 to 221 | textured fill | the same call and index at `0x427a25` (call at `0x427b0d`) with 141 added in place of 60 |
| 230 to 256 | textured fill | the three routines at `0x428008`, `0x428105`, and `0x4281cc` (called at `0x427dc7`, `0x427eb3`, and `0x427f98`) form a mask number from a base (234, 243, and 252) plus the difference of two 5-bit heights, and all three draw through the call at `0x428363`. With the same bound of 4 either way the three bases give 230 to 238, 239 to 247, and 248 to 256. Confirmed routine, inferred bound. Under the inferred bound of 4, entries 222 to 229 are reached by none of the five sites and are left unclaimed (the arithmetic alone reaches 203 to 265, so a larger real bound would reach them) |
| 260 to 292, 300 to 309 | texture of the fill | the texture number at the same call sites: 260 plus a table value of 0 to 32 at `0x4279a9` and `0x427b0d`, and 300 to 302 plus 0, 3, or 6, or 309, at `0x428363`. Confirmed |

The mask entries of the reference install all decode with this layout and consume exactly their size: no run
passes the width. Entries 1597 and 1598 are masks whose rows are all end pairs (48 x 11, nothing covered); they
lie in the shadow range. Entries 2670 and 3010 to 3032 are of the same shape but lie among sprites, and no mask
routine reaches them by number, so the mask kind does not claim them (they are the player-color overlays above).

The preview draws each mask alone in the one color the program stores for it: a shadow in index `0x80`.
A textured-fill mask has no color of its own (the program fills it with a texture); the preview draws it in
the first player's color, index `0x40`, as a **stand-in**. The texture belongs to the milestone that draws
terrain; the texture entries show as raw images. All previews use the game's palette, entry 3.

### Corrupt masks

**Status:** stand-in. The program checks none of these. Each is unreadable for its own entry, with its reason (and the
row, where there is one) in the message:

- A run whose end (the skip and the cover added to the end of the previous run) is past the width.
- A stream that ends before every row of the height has its end pair, including a half pair at the end.
- A width or height of 0 (nothing to draw), and an entry shorter than its header.
- A height that needs more rows than the entry holds (each row has at least its end pair, 2 bytes), and a width x
  height whose RGBA (four bytes a pixel) is beyond what an array holds. Both are refused before the pixel flags are
  allocated. The program has no such bound (it draws row by row and builds no RGBA), so this one is the engine's.

Bytes after the last row are ignored, as the program stops after the height's rows.

## Corrupt entries

**Status:** stand-in. The program checks none of these; the engine reports them, each for its own entry, and
the other entries stay readable.

- A palette entry whose size is not 768 bytes is unreadable.
- A graphics entry shorter than its 10-byte header is unreadable.
- A raw image whose size is not 10 plus width x height is unreadable. The message names the width, the height,
  and both sizes.
- A raw image of width 0 or height 0 is unreadable. The program draws nothing for it and takes no harm, and no
  raw image of the reference install is empty; the engine has nothing to preview for it.
- A raw image whose palette entry is missing or unreadable is unreadable, and the message names the palette.

## In the engine

`PaletteFile.Read` and `GraphicFile.Read` and `GraphicFile.ReadRaw` in `src/Formats` read the entries.
`IndexedImage.ToRgba` turns palette indices into RGBA on the CPU, with no Godot type, and the previews of
`PaletteKind` and `RawImageKind` in `src/Assets` hold the result. `MaskFile.Read` reads a mask; `MaskKind` previews
the masks and `FillTextureKind` the textures of the fill. How the game renders palette graphics is
decided with the renderer.
