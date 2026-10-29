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
drawing mode: none of those routines reads them.

Every graphics entry starts with a header of 10 bytes, all little-endian:

- Byte 0 and byte 1: two signed bytes. No drawing routine read so far reads them. Byte 0 is 1 in every one of
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
| 1675 to 1693 | shadow | Objects 175 to 177, 179 to 187, and 193 come from the 24-entry table that the call at `0x44fcac` indexes at `0x44fc87` (an index of 1 to 23 that the stores from `0x44f933` to `0x44fa5d` set); 178 and 192 are stored before the call at `0x44f928` and the jump at `0x44ec2a`; 188 to 191 are 188 plus 0 to 3 at `0x44f3a8` (jump at `0x44f3ae`). Confirmed. The same table values are also drawn through the second shadow routine at `0x406210`, which `0x44ff2a` calls with the number plus 1500 |
| 60 to 140 | textured fill | `Gfx_DrawTexturedFill`, called at `0x4279a9`: the mask is 60 plus an index from the heights of three neighbouring nodes, `(h1 + 4 - h2) + 9 x (h1 + 4 - h3)` (formed at `0x4278be`), which indexes the 81-entry tables of 9 x 9 combinations. The range assumes the height difference to a neighbour is at most 4 either way, which the 9 x 9 tables encode and the code does not check. Confirmed routine, inferred bound |
| 141 to 221 | textured fill | the same call and index at `0x427a25` (call at `0x427b0d`) with 141 added in place of 60 |
| 230 to 256 | textured fill | the three routines at `0x428008`, `0x428105`, and `0x4281cc` (called at `0x427dc7`, `0x427eb3`, and `0x427f98`) form a mask number from a base (234, 243, and 252) plus the difference of two 5-bit heights, and all three draw through the call at `0x428363`. With the same bound of 4 either way the three bases give 230 to 238, 239 to 247, and 248 to 256. Confirmed routine, inferred bound. Entries 222 to 229 are reached by none of the five sites and are left unclaimed |
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
  height beyond what an array holds. Both are refused before the pixel flags are allocated.

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
