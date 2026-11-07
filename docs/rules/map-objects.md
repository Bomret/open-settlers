# Map objects

How the program draws the objects of a map: which sprite an object code names, where the sprite sits on its
tile, and how a sprite's empty pixels are left. The codes come from the generation ([Map form](map-form.md)). The
sprites are entries of the graphics archive ([Graphics images](graphics-images.md)).

## The drawing routines

**Status:** confirmed for the routines and the arithmetic below; the pixel alignment of the sprite on its node is
**inferred** (below).

`Render_DrawMapView` (`0x44d38d`) runs `0x44d3ae` for each view. That routine walks the visible rows two by two and
calls `0x44d5b4` for each row, which draws the tiles of a row from west to east: the objects of a row paint over the
terrain of the same row, and a later row paints over an earlier one. `0x44d5b4` reads byte 3 of the tile's record A:
bit 7 marks a tile with a water triangle (and sends it first to `Gfx_DrawPhaseSprite`, `0x44ff59`, which draws a
water sprite and is no object), and the low 7 bits are the object code.

## Code to sprite

**Status:** confirmed.

- Code 0 draws nothing. Codes 1 to 7 take other routines: code 1 goes to `0x44db26`, which draws a building from a
  record table, and codes 2 to 4 go to `0x44e329`, which reads another record table; codes 5 to 7 draw nothing. The
  generation places none of them, and the engine's view draws none.
- A code from 8 on has the index `code - 8` (0 to 119). From index 24 on, the sprite entry is `1250 + index`: a
  still sprite (`Gfx_DrawObjectSprite`, `0x44da4d`, adds 1250).
- Indexes 0 to 15 (codes 8 to 23) animate. With the frame counter `c` at the game state block's `+0x206`, the phase
  is `(c + index) >> 4`. The entry is `1250 + (index & 0x78) + (phase & 7)`: codes 8 to 15 share the eight frames
  1250 to 1257, and codes 16 to 23 the eight frames 1258 to 1265. The low bits of the code only offset the phase.
- Indexes 16 to 23 (codes 24 to 31) animate with 4 frames: `1250 + (index & 0x7c) + (phase & 3)`, with the same
  phase. Codes 24 to 27 take 1266 to 1269 and codes 28 to 31 take 1270 to 1273.
- The routine also counts the objects it draws of indexes below 24 in the view (`+0x1b6`), which the engine leaves out.

The engine's view draws the frame of this rule (`ObjectSprites.Entry`). The counter is the game's clock in the game
view and a clock of the view's own, 8 units per tick at the tick rate, in the map view. The view rebuilds the object
meshes when the counter changes.

## Water

**Status:** confirmed for the entry and the position; **inferred** for the phase offset of a tile and for the screen
test.

A tile whose object byte has bit 7 set draws a water sprite before its object (`Gfx_DrawPhaseSprite`, `0x44ff59`).
The routine takes the low word of the address of the tile's record, keeps bits 2 to 5 (`& 0x3c`), doubles it, flips
it with `0xa8`, adds the counter at `+0x206`, shifts right 3, and keeps 4 bits: entry `630 + that`. A tile steps one
frame every 8 counter units, one tick, through 16 frames. The record of a tile is 4 bytes, so the masked address is
the tile index times 4 plus a constant of the table's start. The engine takes that constant as 0 (inferred): tiles
16 apart share a phase, and the start only shifts which ones.

The sprite sits at the tile's horizontal field times 8 and the row's top plus 8, with no height: 16 pixels west of
where an object of the tile at height 0 sits, the node's position less 16 in x in the engine's layout. It is drawn
only over pixels of palette index 8 ([Graphics images](graphics-images.md#sprites-drawn-over-color-8)). The engine's
view (`src/Viewer/Water.gdshader`) draws a pixel where the screen under it has the color of palette index 8, from a
copy of the screen taken after the terrain and the roads. Unlike the original, a water sprite does not see the
pixels another water sprite of the same pass changed (inferred).

## Where the sprite sits

**Status:** confirmed for the offsets; **inferred** for their alignment with the terrain nodes.

`Gfx_DrawObjectSprite` takes the draw position from the tile: the horizontal field is 4 per tile and the routine
scales it by 8 (32 pixels per tile) and adds 16, and the vertical field is the row's top minus 4 pixels for each
unit of the tile's height (the low 5 bits of byte 1 of record A) plus 8. The sprite's own header gives the signed
offsets of its left and top edges from that position (`Gfx_BlitSprite`, `0x405590`). In the engine's screen
layout a node lies at `x = 32 * column - 16 * row`, `y = 20 * row` minus 4 per unit of height, the point where the
terrain's triangles and the roads meet, and an object is drawn at that point, then moved by the sprite's offsets.
The program's 16 and 8 bring its tile field to the same point: screenshots of the original's running game, held outside the repository (INV-0001), show the roads meeting
at the foot of each flag's pole, which a draw 16 east and 8 south of the engine's node would miss by those pixels.
That alignment is **inferred** from the picture; no emulator or debugger run of the terrain or the road draw
confirms where the program's road pass puts a node. The castle's entry (1428) is emulated (`peek:44fd2f:4` prints
`66B8B200`, the stub loading the object number 178); its draw at its node is **inferred** from the objects' draw:
no emulator or debugger run of the draw confirms it for the castle.

Before the sprite, the routine draws the shadow mask of the entry plus 1500 through `Gfx_DrawShadow` (`0x4061a0`),
which stores one fixed palette index on the covered pixels. **Stand-in:** the engine draws no shadow.

## Transparency

**Status:** confirmed.

A sprite is a stream of runs (`Gfx_BlitSprite`): a pixel the runs do not cover is left as the surface holds it, and a
covered pixel is copied as its palette index, 0 included. The program has no color key. The engine keeps the
coverage of each pixel apart from its palette index, so an empty pixel shows the terrain beneath it and a drawn
pixel of index 0 shows color 0 of the palette.

## In the engine

`src/MapView/ObjectSprites.cs` (code to entry), `ObjectDraws.cs` (the tile and the position), `ObjectImage.cs`
(indices and coverage), `ObjectGraphics.cs` (the install's sprites as array-texture layers), and
`ObjectQuads.cs` (the quads, in bands of 16 rows). `src/Viewer/MapViewControl.cs` and `Objects.gdshader` draw them
over the terrain of all nine copies. Resources (`Map.Deposits`) are never drawn. The tile routine
`0x44d5b4` reads the flag, height, and object bytes of record A and, at the tile's record-B offset (the state
`+0x36`), byte 1, none of which is the resource byte (byte 0 of record B). Byte 1 marks a waiting carrier
([Carriers](carriers.md#drawing)). The engine departs from the program in three places: it
draws no shadow, it draws the objects of every row after the terrain, and it draws no object of code 2 to 7 from the map (a flag, code 1, is drawn: [Flags](flags.md#how-a-flag-is-drawn); the castle and the woodcutter's hut are drawn from the game's buildings, the hut with the entry 1417: [Settlers](settlers.md#drawing-the-walk-leads)).
