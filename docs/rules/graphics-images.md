# Graphics images

This note covers the palette entries of the graphics archives, the 10-byte header that every graphics entry
starts with, and the raw images: the entries that the program draws as plain rows of palette indices. Sprites
with transparency, masks, and glyphs read the same header and are other notes. How the archives are loaded and
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
`PaletteKind` and `RawImageKind` in `src/Assets` hold the result. How the game renders palette graphics is
decided with the renderer.
