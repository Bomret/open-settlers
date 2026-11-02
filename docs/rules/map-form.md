# Map form and the height and terrain passes

This note records the map sizes, the layout of the map in the original's memory, the canonical form the
engine and the emulator harness both write for comparison, and the generation passes that fill the heights
and the terrain. It covers `Map_SetGeometry`, `MapGen_InitParams`, the seeding of `MapGen_Generate`, and the
passes `MapGen_ClearMap` to `MapGen_ScaleHeights`. The later passes (terrain detail, objects, deposits) are
not covered yet. Addresses and routine names are in the [Program index](program-index.md#map-generation).

## Map sizes

**Status:** confirmed for the sizes, inferred for the set.

A map has a size value. `StartScreen_StartGame` at `0x4685bc` takes the value from `Game_SetStartParams`
at `0x468717` and starts the map at 32 columns and 32 rows. For each step of the value above 1 it doubles
the columns, then the rows, then the columns again (`0x4685da` to `0x468603`). **Emulated:** the harness
ran those instructions: value 1 gives 32 by 32 tiles, value 2 gives 64 by 32, and value 3 gives 64 by 64.
A value of 0 would count down through 65535 steps, so it is no size.

`Game_SetStartParams` hands on 3 for a mission, and for any other start the free-game size value at state
`+0x362`. The program sets that value once, at start-up (`Game_InitSettings` at `0x424fca`): the low 4 bits
of the memory class at `+0x30c`, at most 3. The save loader lowers it to the memory class when the saved
value is higher (`0x4604ed`). **Inferred:** the original offers exactly the sizes 1, 2, and 3, because no
click handler of the free-game page writes `+0x362`, and the memory class of a normal start is at least 1.
The engine refuses any other value with a message and generates nothing.

## The original's layout

**Status:** confirmed by the emulated passes.

`Map_SetGeometry` at `0x42195c` derives masks and step sizes from the columns and rows. A tile is 4 bytes,
and the buffer holds two records for each tile: record A at the tile's column times 4 within its row, and
record B 4 times the columns further on in the same row. A row is therefore 8 times the columns long.
The offsets of the six neighbors are stored as masked values, so adding one to a tile offset and masking
wraps the column and the row on their own: the map wraps at its edges in both directions. The engine
keeps a column and a row and wraps them with the same effect.

The passes of this note use these bytes of a tile: byte 0 of record A (flags), byte 1 (height), byte 2
(terrain, two nibbles), byte 3 (a working mark of `MapGen_KeepMainLand`, zero before and after it), and byte
0 of record B (lake detail). Later passes use the others.

The six neighbors of a tile, in the order the passes visit them: east, south-east (one column east and one
row down), south (one row down), west, north-west (one column west and one row up), and north (one row up).

## The parameters

`MapGen_InitParams` at `0x421530` stores the water level 20 and the lake radius 14 first, then clears other
generator fields. **Inferred:** the passes of this note read only those two values.

## The canonical map form

**Status:** the engine's own form (a stand-in for comparison; the original has no such form).

The canonical form is a list of layers in a fixed order. A layer is one byte for each tile, in row-major
order: the tile in column `x` and row `y` is entry `y * columns + x`. The layers of this note:

| Number | Layer | Source in the original |
| --- | --- | --- |
| 0 | `flags` | byte 0 of record A; bit 6 marks the middle of a lake |
| 1 | `height` | byte 1 of record A |
| 2 | `terrain` | byte 2 of record A; the upper triangle in the high nibble, the lower in the low nibble |
| 3 | `lake` | byte 0 of record B; a draw from 0 to 7 where a lake has its middle |

The digest of a layer is the FNV-1a 64-bit hash of its bytes in that order. The digest line after a step
names the size value, the map seed words, the step, the three generator words, and the digests of the
layers in layer order. The engine's `Map.Digest` and the harness command `gen` compute the same digests
from the same layout. The steps are the seeding (`Seed`, which has the generator words only, because the
buffer holds nothing the generator wrote) and the passes `ClearMap`, `SeedHeights`, `SubdivideHeights`,
`LimitSlopes`, `FillLakes`, `LowerToWaterLevel`, `ClassifyTerrain`, `KeepMainLand`, and `ScaleHeights`.

**How later layers join.** A layer that a later pass adds, such as objects or resources, takes the next
layer number and its own digest. The layers above keep their numbers, their meaning, and their digests
after every step of this note, so the digests pinned for these steps stay true. A later pass that changes
a layer above changes that layer's digest after its own step only. The generator words after a step cover
the number of draws, so a pass's draw count is part of what a digest line checks.

## The passes

**Status:** confirmed; every pass is **emulated** for the three sizes and three seeds in
`tests/OpenSettlers.Tests/map-digests.txt`.

1. **Seeding.** `MapGen_Generate` copies the map seed into the random generator and steps it twice. A pass
   draws the new first word of the generator. Two draws.
2. **`MapGen_ClearMap`.** Zeroes every byte of the buffer. No draw.
3. **`MapGen_SeedHeights`.** Walks the tiles in row-major order and, on every 16th column of every 16th
   row, draws once and stores the low byte of the draw, at most 250, as the height. One draw per such
   tile: columns times rows divided by 256.
4. **`MapGen_SubdivideHeights`.** Draws once and takes an amplitude from the low 7 bits plus 128, and a
   spread of 0.6 times that (the amplitude times `0x9999`, shifted right by 16). Then four sweeps with a
   half distance of 8, 4, 2, and 1 tiles. A sweep visits the tiles at multiples of twice the half distance,
   in row-major order, and for each sets three midpoints from the tile's own height and the height of the
   tile one full distance away: east, south, and south-east. Each midpoint draws once; its height is the
   mean of the two heights plus the draw times the amplitude divided by 65536, minus the spread, kept from
   0 to 250. After a sweep the amplitude and spread halve. Draws: 1 plus 3 times columns times rows times
   (1/256 + 1/64 + 1/16 + 1/4).
   **Static and emulated:** before the first midpoint of the pass the original's second height still has
   the high byte of the amplitude draw in its upper half, so the first midpoint's mean includes it. The
   engine keeps that word. After a midpoint the word holds the new height.
5. **`MapGen_LimitSlopes`.** Repeats a scan until it changes nothing. The scan visits the tiles in row-major
   order and for each compares the tile's height, read before its three neighbors are corrected, with the
   south, south-east, and east neighbor, in that order. A neighbor that differs by more than 32 is set to
   32 above or below the tile's height. No draw.
6. **`MapGen_FillLakes`.** For each level from 0 to 20, visits the tiles in row-major order. A tile whose
   height equals the level starts a lake when none of its six neighbors is higher than 20; otherwise its
   height becomes 0. A lake marks the tile 255 and its six neighbors 254, then grows ring by ring up to
   radius 14: a ring tile whose six neighbors are all at most 20 or marked, and one of which is marked
   255, becomes 255 and marks its neighbors 254; a ring that adds no tile ends the growth. At the end every
   mark above 253 within the radius drops by 2, so the lake tiles read 253 and the edge tiles 252. The
   pass then visits all tiles once more: height 0 becomes 21, a tile at 253 draws once and gets the lake
   detail (the draw's low 3 bits), flag bit 6, and height 19, and a tile at 252 becomes 20. One draw for
   each lake middle.
7. **`MapGen_LowerToWaterLevel`.** Subtracts 19 from every height, so that lake tiles read 0 and shore
   tiles 1. No draw.
8. **`MapGen_ClassifyTerrain`.** Sets the terrain of each tile from two sums. The upper triangle uses the
   heights of the tile, its south-east, and its south neighbor; the lower triangle uses those of the tile,
   its east, and its south-east neighbor. A sum maps to a terrain code: below 3 gives 0, below 384 gives 5,
   below 416 gives 6, below 448 gives 11, below 480 gives 12, below 528 gives 13, below 560 gives 14, and
   any other sum gives 15. No draw.
9. **`MapGen_KeepMainLand`.** Clears byte 3. Scans for the first tile with a height above 0 and floods from
   it through the six neighbors of every tile that has land in a triangle that touches the neighbor. If
   the flooded tiles times 4 are at least the number of tiles, every unreached land tile gets height 0
   and terrain 0, and the triangles around it lose their terrain, and the pass clears byte 3 again. If
   not, the scan goes on from the next tile. No draw. **Stand-in:** where the scan runs through the map
   twice without such a mass, the original scans without end; the engine refuses with an error.
10. **`MapGen_ScaleHeights`.** Replaces every height by its value plus 6, divided by 8, rounded down. No
    draw.

## In the engine

`src/Simulation/MapSize.cs`, `Map.cs`, `MapLayer.cs`, `MapPass.cs`, and `MapGenerator.cs`. The engine
departs from the program in two places: it keeps columns and rows where the program keeps masked offsets,
and it throws where the program would scan without end.

The harness command `gen:N:S0,S1,S2` in `tools/GhidraDump/EmulateMapStart.java` runs `MapGen_Generate`
for the size value N and the map seed words from the original's size code through the end of
`MapGen_ScaleHeights`, and prints the digest line after the seeding and after each pass. It skips the
progress bar. It fills the buffer with a pattern first, so that `MapGen_ClearMap` is checked.
